using System;
using NUnit.Framework;
using RingToss.Core;
using RingToss.Application;

namespace RingToss.Tests
{
    public sealed class StageTransactionTests
    {
        private static StageSession Open()
        {
            var session = StageSession.CreatePrototype("test-run");
            Assert.That(session.OpenStage("open").Success, Is.True); return session;
        }
        private static void Throw(StageSession session, double speed, string tx, double angle = 45)
        {
            Assert.That(session.Launch(new ThrowInput(angle, speed), tx).Success, Is.True);
            for (int i = 0; i < 721 && session.Phase == StagePhase.Flying; i++) session.Step();
            Assert.That(session.Phase, Is.Not.EqualTo(StagePhase.Flying));
        }
        private static void CashHit(StageSession session, double speed, string tx)
        {
            Throw(session, speed, "throw-" + tx);
            Assert.That(session.Phase, Is.EqualTo(StagePhase.AwaitingDisposition));
            Assert.That(session.Cash("cash-" + tx).Success, Is.True);
        }

        [Test]
        public void FirstStallFreezesActualFreshValueAndStartsWithSeparateAccounts()
        {
            var session = StageSession.CreatePrototype("test-run");
            Assert.That(session.Target, Is.EqualTo(105)); // 3*30 + 3*40, alpha 0.5.
            Assert.That(session.RingsRemaining, Is.EqualTo(8));
            Assert.That(session.Wallet, Is.Zero); Assert.That(session.StageReceipts, Is.Zero);
            Assert.That(session.Launch(new ThrowInput(45, 4.58), "early").Success, Is.False);
            Assert.That(session.OpenStage("open").Success, Is.True);
            Assert.That(session.OpenStage("open").AlreadyApplied, Is.True);
        }

        [Test]
        public void CashAndRetainAreExclusiveAndDuplicateCommandCannotPayTwice()
        {
            var session = Open(); Throw(session, 4.58, "throw");
            string itemId = session.Slots[0].Id;
            CommandResult cash = session.Cash("cash");
            Assert.That(cash.Success, Is.True); Assert.That(cash.Event.ItemId, Is.EqualTo(itemId));
            Assert.That(session.Wallet, Is.EqualTo(30)); Assert.That(session.StageReceipts, Is.EqualTo(30));
            Assert.That(session.Slots[0], Is.Null);
            Assert.That(session.Cash("cash").AlreadyApplied, Is.True);
            Assert.That(session.Cash("cash").Event, Is.Null, "Already-applied commands must not replay reward events.");
            Assert.That(session.Cash("cash-again").Success, Is.False);
            Assert.That(session.Retain("retain-too").Success, Is.False);
            Assert.That(session.Wallet, Is.EqualTo(30)); Assert.That(session.StageReceipts, Is.EqualTo(30));
        }

        [Test]
        public void RetainOccupiesOriginalSlotAndCurrentStageSalvagePaysHalfExactlyOnce()
        {
            var session = Open(); Throw(session, 4.58, "throw");
            Assert.That(session.Retain("retain").Success, Is.True);
            Assert.That(session.StageReceipts, Is.Zero); Assert.That(session.Wallet, Is.Zero);
            Assert.That(session.Slots[0].Occupancy, Is.EqualTo(Occupancy.Mechanism));
            Assert.That(session.Slots[0].Durability, Is.EqualTo(4));
            Assert.That(session.AdjustmentsRemaining, Is.EqualTo(2));
            Assert.That(session.Cash("cash-too").Success, Is.False);
            Assert.That(session.Salvage(0, "salvage").Success, Is.True);
            Assert.That(session.Salvage(0, "salvage").AlreadyApplied, Is.True);
            Assert.That(session.Salvage(0, "salvage-again").Success, Is.False);
            Assert.That(session.StageReceipts, Is.EqualTo(15)); Assert.That(session.Wallet, Is.EqualTo(15));
            Assert.That(session.Target, Is.EqualTo(105));
        }

        [Test]
        public void OldSnapshotSalvageChangesWalletAndCannotPayFrozenTarget()
        {
            string stage = "new-stage"; var scene = new SlotObject[6];
            for (int i = 0; i < 6; i++)
            {
                ObjectKind kind = i % 2 == 0 ? ObjectKind.Fan : ObjectKind.Board;
                scene[i] = new SlotObject("item-" + i, i, kind, Occupancy.Prize, 0,
                    kind == ObjectKind.Fan ? 0 : 45, false, Rules.Price(kind), stage, true);
            }
            scene[0] = new SlotObject("old-item", 0, ObjectKind.Fan, Occupancy.Mechanism, 1, 0, true, 30, "old-stage", true);
            var session = new StageSession(stage, scene, 100); session.OpenStage("open");
            Assert.That(session.Target, Is.EqualTo(90));
            Assert.That(session.EndStage("end").Success, Is.True);
            Assert.That(session.IsSuccess, Is.False, "Old wallet cannot satisfy this stage's receipts.");
            var second = new StageSession(stage, scene, 100); second.OpenStage("open");
            Assert.That(second.Salvage(0, "salvage-old").Success, Is.True);
            Assert.That(second.Wallet, Is.EqualTo(115)); Assert.That(second.StageReceipts, Is.Zero);
            Assert.That(second.Target, Is.EqualTo(90));
        }

        [Test]
        public void InvalidAdjustmentCostsNothingAndValidSwitchesUseExactlyTwoActions()
        {
            var session = Open(); Throw(session, 4.58, "throw"); session.Retain("retain");
            Assert.That(session.Move(0, 1, "blocked").Success, Is.False);
            Assert.That(session.Rotate(0, 17, "bad-angle").Success, Is.False);
            Assert.That(session.Rotate(0, 360, "same-angle").Success, Is.False);
            Assert.That(session.AdjustmentsRemaining, Is.EqualTo(2));
            Assert.That(session.Toggle(0, "off").Success, Is.True);
            Assert.That(session.Toggle(0, "off").AlreadyApplied, Is.True);
            Assert.That(session.AdjustmentsRemaining, Is.EqualTo(1));
            Assert.That(session.Toggle(0, "on").Success, Is.True);
            Assert.That(session.Toggle(0, "third").Success, Is.False);
            Assert.That(session.AdjustmentsRemaining, Is.Zero);
        }

        [Test]
        public void RelocationCanSetDirectionForOneActionAndNeverMovesPrize()
        {
            var session = Open(); CashHit(session, 4.58, "first");
            Throw(session, 5.98, "second"); session.Retain("retain-board");
            string id = session.Slots[1].Id;
            Assert.That(session.Move(1, 0, 90, "move").Success, Is.True);
            Assert.That(session.Slots[1], Is.Null);
            Assert.That(session.Slots[0].Id, Is.EqualTo(id));
            Assert.That(session.Slots[0].AngleDegrees, Is.EqualTo(90));
            Assert.That(session.AdjustmentsRemaining, Is.EqualTo(1));
            Assert.That(session.Move(2, 1, "move-prize").Success, Is.False);
            Assert.That(session.AdjustmentsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void FinalRingMustFinishDispositionBeforeSuccessOrFailureCheck()
        {
            var session = Open(); CashHit(session, 4.58, "one"); CashHit(session, 5.98, "two"); CashHit(session, 7.16, "three");
            for (int i = 0; i < 4; i++) Throw(session, 4, "miss-" + i, 20);
            Assert.That(session.RingsRemaining, Is.EqualTo(1)); Assert.That(session.StageReceipts, Is.EqualTo(100));
            Throw(session, 8.18, "final");
            Assert.That(session.RingsRemaining, Is.Zero); Assert.That(session.Phase, Is.EqualTo(StagePhase.AwaitingDisposition));
            Assert.That(session.EndStage("early-end").Success, Is.False);
            Assert.That(session.Cash("final-cash").Success, Is.True);
            Assert.That(session.EndStage("end").Success, Is.True);
            Assert.That(session.IsSuccess, Is.True); Assert.That(session.Wallet, Is.EqualTo(140));
            Assert.That(session.Launch(new ThrowInput(45, 9.08), "after-end").Success, Is.False);
        }

        [Test]
        public void RescueIsOncePerStallWalletOnlyAndAvailableAfterFinalMiss()
        {
            var session = Open(); CashHit(session, 4.58, "one");
            for (int i = 0; i < 7; i++) Throw(session, 4, "miss-" + i, 20);
            Assert.That(session.RingsRemaining, Is.Zero); Assert.That(session.CanBuyRescue, Is.True);
            Assert.That(session.BuyRescue("rescue").Success, Is.True);
            Assert.That(session.BuyRescue("rescue").AlreadyApplied, Is.True);
            Assert.That(session.BuyRescue("rescue-two").Success, Is.False);
            Assert.That(session.RingsRemaining, Is.EqualTo(2)); Assert.That(session.Wallet, Is.EqualTo(12));
            Assert.That(session.StageReceipts, Is.EqualTo(30)); Assert.That(session.Target, Is.EqualTo(105));
            Assert.That(session.EndStage("decline").Success, Is.True); Assert.That(session.IsSuccess, Is.False);
        }

        [Test]
        public void ReplayUsesBeforeThrowSnapshotWithoutMutatingFormalDurabilityOrAccounts()
        {
            var session = Open(); Throw(session, 4.58, "first"); session.Retain("retain");
            Throw(session, 8.18, "second");
            int durability = session.Slots[0].Durability, wallet = session.Wallet, receipts = session.StageReceipts, rings = session.RingsRemaining;
            StagePhase phase = session.Phase; Flight original = session.LastFlight, replay = session.CreateReplayLastThrow();
            for (int i = 0; i < 721 && replay.State == FlightState.Flying; i++) replay.Step();
            Assert.That(replay.State, Is.EqualTo(original.State)); Assert.That(replay.Events.Count, Is.EqualTo(original.Events.Count));
            for (int i = 0; i < replay.Events.Count; i++)
            {
                Assert.That(replay.Events[i].Kind, Is.EqualTo(original.Events[i].Kind));
                Assert.That(replay.Events[i].Time, Is.EqualTo(original.Events[i].Time).Within(1e-10));
            }
            Assert.That(session.Slots[0].Durability, Is.EqualTo(durability));
            Assert.That(replay.Slots[0].Durability, Is.EqualTo(durability));
            Assert.That(session.Wallet, Is.EqualTo(wallet)); Assert.That(session.StageReceipts, Is.EqualTo(receipts));
            Assert.That(session.RingsRemaining, Is.EqualTo(rings)); Assert.That(session.Phase, Is.EqualTo(phase));
        }

        [Test]
        public void PreparationAdjustmentsAreFreeUntilExplicitOpenStageBoundary()
        {
            var scene = new SlotObject[6];
            for (int i = 0; i < 6; i++)
            {
                ObjectKind kind = i % 2 == 0 ? ObjectKind.Fan : ObjectKind.Board;
                scene[i] = new SlotObject("fresh-" + i, i, kind, Occupancy.Prize, 0,
                    kind == ObjectKind.Fan ? 0 : 45, false, Rules.Price(kind), "stage", true);
            }
            scene[0] = new SlotObject("old-fan", 0, ObjectKind.Fan, Occupancy.Mechanism, 3, 0, true, 30, "old", false);
            scene[1] = new SlotObject("old-board", 1, ObjectKind.Board, Occupancy.Mechanism, 4, 45, true, 40, "old", false);
            var session = new StageSession("stage", scene);
            Assert.That(session.Rotate(0, 30, "prepare-turn").Success, Is.True);
            Assert.That(session.Toggle(1, "prepare-off").Success, Is.True);
            Assert.That(session.Swap(0, 1, "prepare-swap").Success, Is.True);
            Assert.That(session.AdjustmentsRemaining, Is.EqualTo(2));
            Assert.That(session.Target, Is.EqualTo(70));
            session.OpenStage("open");
            Assert.That(session.Toggle(0, "playing-on").Success, Is.True);
            Assert.That(session.Rotate(1, 0, "playing-turn").Success, Is.True);
            Assert.That(session.AdjustmentsRemaining, Is.Zero);
            Assert.That(session.Toggle(0, "playing-third").Success, Is.False);
            Assert.That(session.Target, Is.EqualTo(70));
        }

        [Test]
        public void DepletedPreinstallAndMissingCandidatesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new StageSession("stage", new SlotObject[6]));
            var scene = new SlotObject[6];
            for (int i = 0; i < 6; i++) scene[i] = new SlotObject("prize-" + i, i, ObjectKind.Fan, Occupancy.Prize, 0, 0, false, 30, "stage", true);
            scene[0] = new SlotObject("empty-old", 0, ObjectKind.Fan, Occupancy.Mechanism, 0, 0, true, 30, "old-stage", true);
            Assert.Throws<ArgumentException>(() => new StageSession("stage", scene));
        }
    }
}
