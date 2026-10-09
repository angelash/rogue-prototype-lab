using System;
using System.Linq;
using NUnit.Framework;

namespace SushiWorkshop.Tests
{
    public sealed class SessionTests
    {
        private static void Accept(CommandResult result) { Assert.That(result.Success, Is.True, result.Reason); }

        // Expected steps and cash are derived in Core/README.md, not recorded from this implementation.
        private static void AdvanceTo(Session session, int target)
        {
            while (session.State.Step < target)
            {
                if (session.CanPrepare) Accept(session.StartOrResume());
                Accept(session.Step());
            }
        }

        private static Session SmallLayout(OrderKind order)
        {
            Session session = Session.CreatePrototype(order);
            Accept(session.MoveStation(3, 0));
            Accept(session.LoadIngredient());
            AdvanceTo(session, 18);
            return session;
        }

        private static Session WholeAtBoundary()
        {
            Session session = Session.CreatePrototype(OrderKind.WholeHeated);
            Accept(session.MoveStation(4, 0, "move-cut"));
            Accept(session.LoadIngredient("feed-1"));
            AdvanceTo(session, 6);
            return session;
        }

        [Test]
        public void SUS_A01_SameStationsExposeMutuallyExclusiveOrdersAndReplayIndependently()
        {
            Session quick = SmallLayout(OrderKind.QuickSmall);
            Session whole = SmallLayout(OrderKind.WholeHeated);
            Assert.That(quick.State.Stations, Is.EqualTo(whole.State.Stations));
            Assert.That(quick.State.Order.RequiredAmountUnits, Is.EqualTo(1));
            Assert.That(whole.State.Order.RequiredAmountUnits, Is.EqualTo(2));
            Assert.That(quick.State.Order.RequiredFood, Is.EqualTo(FoodState.SmallRoll));
            Assert.That(whole.State.Order.RequiredFood, Is.EqualTo(FoodState.HeatedRoll));
            Assert.That(quick.State.Order.DeliveredQuantity, Is.EqualTo(2));
            Assert.That(whole.State.Order.DeliveredQuantity, Is.Zero);
            Assert.That(quick.State.Outcome, Is.EqualTo(RoundOutcome.OrderCompleted));
            Assert.That(whole.State.Outcome, Is.EqualTo(RoundOutcome.Failed));
            Assert.That(quick.State.Wallet, Is.EqualTo(27)); // 18 - 2 move - 3 ingredient + 2*7.
            Assert.That(whole.State.Wallet, Is.EqualTo(13));
            Assert.That(quick.State.Events.Where(e => e.Kind == EventKind.OrderDelivered).Select(e => e.Step), Is.EqualTo(new[] { 5, 10 }));
            Session repeated = SmallLayout(OrderKind.QuickSmall);
            Assert.That(repeated.State.Events.Select(e => e.Kind + ":" + e.Step + ":" + e.PlateId + ":" + e.Amount),
                Is.EqualTo(quick.State.Events.Select(e => e.Kind + ":" + e.Step + ":" + e.PlateId + ":" + e.Amount)));
            Session restored;
            string reason;
            Assert.That(Session.TryRestore(whole.ExportSnapshot(), out restored, out reason), Is.True, reason);
            Assert.That(restored.State.Wallet, Is.EqualTo(13));
            Assert.That(restored.LastEvents, Is.Empty);
        }

        [Test]
        public void WholeOrderHasReachableTwoPortionProductionTrace()
        {
            Session session = WholeAtBoundary();
            Assert.That(session.State.Order.DeliveredQuantity, Is.EqualTo(1));
            Assert.That(session.State.Phase, Is.EqualTo(Phase.CircleBoundary));
            Accept(session.LoadIngredient("feed-2"));
            AdvanceTo(session, 18);
            Assert.That(session.State.Events.Where(e => e.Kind == EventKind.OrderDelivered).Select(e => e.Step), Is.EqualTo(new[] { 5, 11 }));
            Assert.That(session.State.Wallet, Is.EqualTo(30)); // 18 - 2 - 2*3 + 2*10.
            Assert.That(session.State.OrderReceipts, Is.EqualTo(20));
            Assert.That(session.State.DeliveredAmountUnits, Is.EqualTo(4));
            Assert.That(session.State.DirtyPlateCount, Is.EqualTo(2));
            Assert.That(session.State.Outcome, Is.EqualTo(RoundOutcome.OrderCompleted));
        }

        [Test]
        public void SUS_A02_CleanPlateSplitIsAtomicAndNewPlateWaitsUntilFollowingStep()
        {
            Session session = Session.CreatePrototype();
            Accept(session.LoadIngredient());
            AdvanceTo(session, 4);
            RoundSnapshot atSplit = session.State;
            Assert.That(atSplit.Plates[0].Food, Is.EqualTo(FoodState.SmallHeatedRoll));
            Assert.That(atSplit.Plates[0].Slot, Is.EqualTo(4));
            Assert.That(atSplit.Plates[0].AmountUnits, Is.EqualTo(1));
            Assert.That(atSplit.Plates[1].Location, Is.EqualTo(PlateLocation.Staged));
            Assert.That(atSplit.Plates[1].Slot, Is.EqualTo(-1));
            Assert.That(atSplit.Plates[1].AmountUnits, Is.EqualTo(1));
            Assert.That(atSplit.Plates[1].Deliverable, Is.True);
            Assert.That(atSplit.FoodUnitsInSystem, Is.EqualTo(2));
            Assert.That(atSplit.CleanPlateCount, Is.EqualTo(4));
            Assert.That(atSplit.Wallet, Is.EqualTo(15));
            Assert.That(atSplit.Events.Count(e => e.Step == 4 && e.PlateId == 1), Is.Zero);
            Accept(session.Step());
            Assert.That(session.State.Plates[1].Location, Is.EqualTo(PlateLocation.Ring));
            Assert.That(session.State.Plates[1].Slot, Is.Zero);
            Assert.That(session.LastEvents.Any(e => e.PlateId == 1 && (e.Kind == EventKind.FoodProcessed || e.Kind == EventKind.ProcessingSkipped)), Is.False);
        }

        [Test]
        public void SUS_A02_NoCleanPlateSplitPreservesOriginalFoodMoneyAndSixEntities()
        {
            Session session = Session.CreatePrototype();
            Accept(session.LoadIngredient());
            Accept(session.StartOrResume());
            for (int step = 1; step <= 4; step++)
            {
                Accept(session.Step());
                Accept(session.LoadIngredient()); // Five purchases; first cut consumes the sixth entity.
            }
            Assert.That(session.State.CleanPlateCount, Is.Zero);
            Assert.That(session.State.Plates[1].Food, Is.EqualTo(FoodState.HeatedRoll));
            Assert.That(session.State.Plates[1].AmountUnits, Is.EqualTo(2));
            Accept(session.Step());
            Assert.That(session.LastEvents.Any(e => e.Kind == EventKind.SplitBlocked && e.PlateId == 1), Is.True);
            Assert.That(session.State.Plates[1].Food, Is.EqualTo(FoodState.HeatedRoll));
            Assert.That(session.State.Plates[1].AmountUnits, Is.EqualTo(2));
            Assert.That(session.State.Wallet, Is.EqualTo(3));
            Assert.That(session.State.LoadedPortions, Is.EqualTo(5));
            Assert.That(session.State.FoodUnitsInSystem, Is.EqualTo(10));
            Assert.That(session.State.RingPlateCount, Is.EqualTo(6));
            Assert.That(session.State.Plates.Select(p => p.Id).Distinct().Count(), Is.EqualTo(6));
            Assert.That(session.State.Plates.Where(p => p.Location == PlateLocation.Ring).Select(p => p.Slot).Distinct().Count(), Is.EqualTo(6));
        }

        [Test]
        public void SUS_A03_RetainedMatchingPlateNeedsNextCircleMarkAndDirtyPlateNeedsPaidWash()
        {
            Session session = Session.CreatePrototype();
            Accept(session.SellStation(4)); // Remove cutting so the retained whole remains a whole.
            Accept(session.LoadIngredient());
            Accept(session.SetDeliverable(0, false));
            AdvanceTo(session, 5);
            Assert.That(session.State.Order.DeliveredQuantity, Is.Zero);
            Assert.That(session.State.Plates[0].Food, Is.EqualTo(FoodState.HeatedRoll));
            Assert.That(session.LastEvents.Any(e => e.Kind == EventKind.OrderRejected && e.Reason.Contains("保留")), Is.True);
            CommandResult premature = session.SetDeliverable(0, true);
            Assert.That(premature.Success, Is.False);
            AdvanceTo(session, 6);
            Accept(session.SetDeliverable(0, true));
            AdvanceTo(session, 11);
            Assert.That(session.State.Order.DeliveredQuantity, Is.EqualTo(1));
            Assert.That(session.State.Plates[0].Location, Is.EqualTo(PlateLocation.DirtyInventory));
            Accept(session.LoadIngredient());
            Assert.That(session.State.Plates[0].Location, Is.EqualTo(PlateLocation.DirtyInventory));
            Assert.That(session.State.Plates[1].Location, Is.EqualTo(PlateLocation.Ring));
            int beforeWash = session.State.Wallet;
            Accept(session.WashOne());
            Assert.That(session.State.Wallet, Is.EqualTo(beforeWash - 1));
            Assert.That(session.State.Plates[0].Location, Is.EqualTo(PlateLocation.CleanInventory));
            Assert.That(session.State.Plates[0].AmountUnits, Is.Zero);
        }

        [Test]
        public void SUS_A04_MissingRollingStationAndReducedFundsCanUseThreeExplicitRescues()
        {
            Session session = Session.CreatePrototype();
            Accept(session.SellStation(2));
            Accept(session.MoveStation(4, 0));
            Accept(session.MoveStation(0, 4));
            Accept(session.MoveStation(3, 0)); // Four changes used. No rolling remains; start cash now 15.
            Accept(session.AbandonOrder());
            Assert.That(session.State.Wallet, Is.EqualTo(13));
            Assert.That(session.State.Stations.Contains(StationKind.Rolling), Is.False);
            for (int circle = 0; circle < 3; circle++)
            {
                Accept(session.LoadIngredient());
                PlateState plate = session.State.Plates.Single(p => p.Location == PlateLocation.Ring && p.Slot == 0);
                Accept(session.QueueBaseSale(plate.Id, true));
                AdvanceTo(session, (circle + 1) * 6);
            }
            Assert.That(session.State.Order.Status, Is.EqualTo(OrderStatus.Abandoned));
            Assert.That(session.State.BaseReceipts, Is.EqualTo(6));
            Assert.That(session.State.StationSaleReceipts, Is.EqualTo(3));
            Assert.That(session.State.AbandonSpent, Is.EqualTo(2));
            Assert.That(session.State.Wallet, Is.EqualTo(10)); // 18+3+6-6 moves-9 ingredients-2 penalty.
            Assert.That(session.State.Outcome, Is.EqualTo(RoundOutcome.BaseTradeCompleted));
            Assert.That(session.State.Events.Any(e => e.Kind == EventKind.OrderAbandoned), Is.True);
            Assert.That(session.State.Events.Count(e => e.Kind == EventKind.BaseSold), Is.EqualTo(3));
        }

        [Test]
        public void SUS_A05_FinalStepDeliveryPrecedesTimeoutAndRoundEndsExactlyOnce()
        {
            Session session = WholeAtBoundary();
            AdvanceTo(session, 13);
            Accept(session.LoadIngredient());
            AdvanceTo(session, 18);
            GameEvent[] final = session.LastEvents;
            Assert.That(session.State.Order.Status, Is.EqualTo(OrderStatus.Fulfilled));
            Assert.That(session.State.Order.DeliveredQuantity, Is.EqualTo(2));
            Assert.That(final.Single(e => e.Kind == EventKind.OrderDelivered).Step, Is.EqualTo(18));
            Assert.That(Array.FindIndex(final, e => e.Kind == EventKind.OrderDelivered), Is.LessThan(Array.FindIndex(final, e => e.Kind == EventKind.RoundFinished)));
            Assert.That(final.Any(e => e.Kind == EventKind.OrderDeparted), Is.False);
            Assert.That(session.State.Outcome, Is.EqualTo(RoundOutcome.OrderCompleted));
            Assert.That(session.Step().Success, Is.False);
            Assert.That(session.State.Step, Is.EqualTo(18));
            Assert.That(session.LoadIngredient().Success, Is.False);
            Assert.That(session.WashOne().Success, Is.False);
        }

        [Test]
        public void SUS_A05_TimeoutProvidesCauseAndRetryIsFreshIndependentRound()
        {
            Session session = Session.CreatePrototype(OrderKind.QuickSmall);
            AdvanceTo(session, 18);
            Assert.That(session.State.Events.Single(e => e.Kind == EventKind.OrderDeparted).Step, Is.EqualTo(12));
            Assert.That(session.State.Events.Last().Kind, Is.EqualTo(EventKind.RoundFinished));
            Assert.That(session.State.Outcome, Is.EqualTo(RoundOutcome.Failed));
            Session retry = session.Retry();
            Assert.That(retry.State.Order.Kind, Is.EqualTo(OrderKind.QuickSmall));
            Assert.That(retry.State.Phase, Is.EqualTo(Phase.Preparation));
            Assert.That(retry.State.Step, Is.Zero);
            Assert.That(retry.State.Wallet, Is.EqualTo(18));
            Assert.That(retry.State.CleanPlateCount, Is.EqualTo(6));
            Assert.That(retry.State.Events, Is.Empty);
            Assert.That(session.State.Phase, Is.EqualTo(Phase.Finished));
        }

        [Test]
        public void InvalidActionsAndDuplicateCommandsNeverSpendTwiceOrDestroyStations()
        {
            Session session = Session.CreatePrototype();
            Assert.That(session.MoveStation(1, 1).Success, Is.False);
            Assert.That(session.BuyStation(0, StationKind.Rolling).Success, Is.False);
            Assert.That(session.State.Wallet, Is.EqualTo(18));
            Assert.That(session.State.AdjustmentsLeft, Is.EqualTo(4));
            Accept(session.MoveStation(1, 2));
            Assert.That(session.State.Stations[1], Is.EqualTo(StationKind.Rolling));
            Assert.That(session.State.Stations[2], Is.EqualTo(StationKind.Filling));
            Assert.That(session.State.Stations.Count(s => s != StationKind.None), Is.EqualTo(4));
            Accept(session.LoadIngredient("one-feed"));
            int wallet = session.State.Wallet;
            CommandResult duplicate = session.LoadIngredient("one-feed");
            Assert.That(duplicate.Success, Is.True);
            Assert.That(duplicate.AlreadyApplied, Is.True);
            Assert.That(duplicate.Events, Is.Empty);
            Assert.That(session.State.Wallet, Is.EqualTo(wallet));
            Assert.That(session.LoadIngredient().Success, Is.False); // Entry occupied.
            Accept(session.StartOrResume());
            Assert.That(session.MoveStation(1, 0).Success, Is.False);
            Assert.That(session.State.Wallet, Is.EqualTo(wallet));
        }

        [Test]
        public void InsufficientIngredientFundsLeaveAvailableCleanPlateAndMoneyUntouched()
        {
            Session session = Session.CreatePrototype();
            Accept(session.MoveStation(4, 0));
            Accept(session.SellStation(3));
            Accept(session.BuyStation(3, StationKind.Heating));
            Accept(session.MoveStation(3, 4)); // 18-2+3-6-2 = 11.
            Accept(session.LoadIngredient());
            Accept(session.StartOrResume());
            Accept(session.Step());
            Accept(session.LoadIngredient());
            Accept(session.Step());
            Accept(session.LoadIngredient());
            Accept(session.Step());
            RoundSnapshot before = session.State;
            Assert.That(before.Wallet, Is.EqualTo(2));
            Assert.That(before.CleanPlateCount, Is.EqualTo(3));
            Assert.That(session.LoadIngredient("failed-purchase").Success, Is.False);
            Assert.That(session.State.Wallet, Is.EqualTo(2));
            Assert.That(session.State.CleanPlateCount, Is.EqualTo(3));
            Assert.That(session.State.LoadedPortions, Is.EqualTo(3));
            Assert.That(session.State.Commands.Length, Is.EqualTo(before.Commands.Length));
        }

        [Test]
        public void ValidBoundaryAndFinishedSnapshotsRestoreWithoutRewardsOrFreeCleaning()
        {
            Session original = WholeAtBoundary();
            RoundSnapshot checkpoint = original.ExportSnapshot();
            Session restored;
            string reason;
            Assert.That(Session.TryRestore(checkpoint, out restored, out reason), Is.True, reason);
            Assert.That(restored.State.Phase, Is.EqualTo(Phase.CircleBoundary));
            Assert.That(restored.State.DirtyPlateCount, Is.EqualTo(1));
            Assert.That(restored.LastEvents, Is.Empty);
            CommandResult duplicate = restored.LoadIngredient("feed-1");
            Assert.That(duplicate.AlreadyApplied, Is.True);
            Assert.That(duplicate.Events, Is.Empty);
            Assert.That(restored.State.Wallet, Is.EqualTo(23));
            Accept(original.LoadIngredient("feed-2"));
            Accept(restored.LoadIngredient("feed-2"));
            AdvanceTo(original, 18);
            AdvanceTo(restored, 18);
            Assert.That(restored.State.Wallet, Is.EqualTo(original.State.Wallet));
            Assert.That(restored.State.Events.Select(e => e.Kind + ":" + e.Step + ":" + e.Amount), Is.EqualTo(original.State.Events.Select(e => e.Kind + ":" + e.Step + ":" + e.Amount)));
            Session finished;
            Assert.That(Session.TryRestore(restored.ExportSnapshot(), out finished, out reason), Is.True, reason);
            Assert.That(finished.State.Phase, Is.EqualTo(Phase.Finished));
            Assert.That(finished.LastEvents, Is.Empty);
            Assert.That(finished.Step().Success, Is.False);
            checkpoint.Plates[0].Location = PlateLocation.CleanInventory;
            checkpoint.Wallet += 100;
            Assert.That(original.State.Wallet, Is.EqualTo(30)); // Exported data cannot alias live state.
        }

        [TestCase("wallet")]
        [TestCase("balanced-wallet")]
        [TestCase("free-clean")]
        [TestCase("duplicate-plate")]
        [TestCase("boundary")]
        [TestCase("finished")]
        [TestCase("order")]
        [TestCase("wait")]
        [TestCase("event")]
        [TestCase("duplicate-command")]
        [TestCase("version")]
        public void RestoreRejectsStateThatCannotBeProducedByItsCommands(string corruption)
        {
            RoundSnapshot bad = WholeAtBoundary().ExportSnapshot();
            switch (corruption)
            {
                case "wallet": bad.Wallet++; break;
                case "balanced-wallet": bad.Wallet++; bad.StationSaleReceipts++; break;
                case "free-clean": bad.Plates[0].Location = PlateLocation.CleanInventory; break;
                case "duplicate-plate": bad.Plates[1].Id = 0; break;
                case "boundary": bad.Step = 5; break;
                case "finished": bad.Phase = Phase.Finished; break;
                case "order": bad.Order.RequiredQuantity = 1; break;
                case "wait": bad.Order.RemainingWait++; break;
                case "event": bad.Events[0].Amount++; break;
                case "duplicate-command": bad.Commands[1].Id = bad.Commands[0].Id; break;
                case "version": bad.RulesVersion = "future"; break;
            }
            Session restored;
            string reason;
            Assert.That(Session.TryRestore(bad, out restored, out reason), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(reason, Is.Not.Empty);
        }
    }
}
