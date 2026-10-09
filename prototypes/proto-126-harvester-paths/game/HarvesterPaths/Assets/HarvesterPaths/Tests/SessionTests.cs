using System;
using System.Linq;
using NUnit.Framework;

namespace HarvesterPaths.Tests
{
    public sealed class SessionTests
    {
        private static void Ok(CommandResult result) { Assert.That(result.Success,Is.True,result.Reason);Assert.That(result.AlreadyApplied,Is.False); }
        private static void Moves(Session s,int count) { for(int i=0;i<count;i++)Ok(s.MoveForward()); }
        private static void Turns(Session s,int amount,int count=1) { for(int i=0;i<count;i++)Ok(s.Turn(amount)); }
        private static void FirstReturn(Session s)
        {
            Ok(s.Harvest());Moves(s,1);Turns(s,-1);Ok(s.Harvest());Moves(s,1);Ok(s.Harvest());Turns(s,1,2);Moves(s,1);Turns(s,1);Moves(s,1);
        }
        private static void FirstRound(Session s) { FirstReturn(s);Ok(s.Deliver(3));Ok(s.UnloadStraw(3)); }
        private static void SecondRound(Session s)
        {
            Ok(s.ContinueRound(ModuleKind.None));Turns(s,1,2);Moves(s,2);Ok(s.Harvest());Moves(s,1);Turns(s,-1);Ok(s.Harvest());Moves(s,1);Ok(s.Harvest());
            Turns(s,1,2);Moves(s,1);Turns(s,1);Moves(s,3);Ok(s.Deliver(3));Ok(s.UnloadStraw(3));Ok(s.Refuel());
        }
        private static void ThirdReturn(Session s)
        {
            Ok(s.ContinueRound(ModuleKind.None));Turns(s,1,2);Moves(s,4);Ok(s.Harvest());Moves(s,1);Turns(s,-1);Ok(s.Harvest());Moves(s,1);Ok(s.Harvest());Moves(s,1);
            Ok(s.ConvertStraw());Ok(s.Harvest());Turns(s,1,2);Moves(s,2);Turns(s,1);Moves(s,5);
        }
        private static Session TwoCrops()
        {
            var s=Session.CreatePrototype();Ok(s.Harvest());Moves(s,1);Turns(s,-1);Ok(s.Harvest());return s;
        }
        private static void EqualCheckpoint(RoundSnapshot before,RoundSnapshot after)
        {
            Assert.That(after.ActionIndex,Is.EqualTo(before.ActionIndex));Assert.That(after.Commands.Length,Is.EqualTo(before.Commands.Length));
            Assert.That(after.Events.Length,Is.EqualTo(before.Events.Length));Assert.That(after.ActionsRemaining,Is.EqualTo(before.ActionsRemaining));
            Assert.That(after.Vehicle.Fuel,Is.EqualTo(before.Vehicle.Fuel));Assert.That(after.Vehicle.Grain,Is.EqualTo(before.Vehicle.Grain));Assert.That(after.Vehicle.Straw,Is.EqualTo(before.Vehicle.Straw));
            Assert.That(after.Wallet,Is.EqualTo(before.Wallet));Assert.That(after.HarvestedCrops,Is.EqualTo(before.HarvestedCrops));
        }
        [Test] public void NoneGoldenAllThreeRoundsClosesIndependentLedgers()
        {
            var s=Session.CreatePrototype();FirstRound(s);
            Assert.That(s.State.ActionsRemaining,Is.EqualTo(11));Assert.That(s.State.Vehicle.Fuel,Is.EqualTo(17));Assert.That(s.State.Wallet,Is.EqualTo(10));Assert.That(s.State.StoredStraw,Is.EqualTo(3));
            SecondRound(s);
            Assert.That(s.State.ActionsRemaining,Is.EqualTo(10));Assert.That(s.State.Vehicle.Fuel,Is.EqualTo(24));Assert.That(s.State.Wallet,Is.EqualTo(14));Assert.That(s.State.StoredStraw,Is.EqualTo(6));
            ThirdReturn(s);Ok(s.Deliver(4));RoundSnapshot end=s.State;
            Assert.That(end.Phase,Is.EqualTo(Phase.Finished));Assert.That(end.Outcome,Is.EqualTo(RoundOutcome.Completed));
            Assert.That(end.ActionsRemaining,Is.EqualTo(22));Assert.That(end.Vehicle.X,Is.EqualTo(1));Assert.That(end.Vehicle.Y,Is.EqualTo(1));Assert.That(end.Vehicle.Facing,Is.EqualTo(Direction.Left));
            Assert.That(end.Vehicle.Fuel,Is.EqualTo(4));Assert.That(end.Wallet,Is.EqualTo(22));Assert.That(end.DeliveredTotal,Is.EqualTo(10));
            Assert.That(end.Vehicle.Grain,Is.Zero);Assert.That(end.Vehicle.Straw,Is.EqualTo(2));Assert.That(end.StoredStraw,Is.EqualTo(6));Assert.That(end.ConvertedStraw,Is.EqualTo(2));Assert.That(end.PavedStraw,Is.Zero);
            Assert.That(end.HarvestedCrops,Is.EqualTo(10));Assert.That(end.FuelSpent,Is.EqualTo(48));Assert.That(end.FuelAdded,Is.EqualTo(28));
            Assert.That(end.Rounds.Select(r=>r.ActionsSpent),Is.EqualTo(new[]{13,22,30}));Assert.That(end.Rounds.Select(r=>r.Delivered),Is.EqualTo(new[]{3,3,4}));
            // 13 + 20 + 26 charged commands, plus two explicit zero-cost round transitions.
            Assert.That(end.Tiles.Count(t=>t.Kind==TileKind.Crop),Is.Zero);Assert.That(end.Commands.Length,Is.EqualTo(61));
        }
        [Test] public void HarvestChangesPassageAndCannotProduceTwice()
        {
            var s=Session.CreatePrototype();RoundSnapshot before=s.State;Assert.That(s.MoveForward().Success,Is.False);EqualCheckpoint(before,s.State);
            Ok(s.Harvest());Assert.That(s.State.Tiles[Rules.CellId(2,1)].Harvested,Is.True);Assert.That(s.State.Tiles[Rules.CellId(2,1)].Kind,Is.EqualTo(TileKind.Ground));
            Assert.That(s.State.Vehicle.Grain,Is.EqualTo(1));Assert.That(s.State.Vehicle.Straw,Is.EqualTo(1));before=s.State;Assert.That(s.Harvest().Success,Is.False);EqualCheckpoint(before,s.State);Ok(s.MoveForward());
        }
        [Test] public void WideHarvestMustFitEveryActualTargetWithoutPartialChanges()
        {
            var s=Session.CreatePrototype(ModuleKind.WideHead);Ok(s.Harvest());Ok(s.MoveForward());Turns(s,1,2);Moves(s,1);Turns(s,1,2);
            Turns(s,-1);Moves(s,1);Turns(s,1);Moves(s,2);
            HarvestPreview preview=s.PreviewHarvest();Assert.That(preview.CoverageCells.Length,Is.EqualTo(3));Assert.That(preview.TargetCells.Length,Is.EqualTo(3));
            Assert.That(preview.SpaceRequired,Is.EqualTo(6));Assert.That(preview.Legal,Is.False);RoundSnapshot before=s.State;Assert.That(s.Harvest().Success,Is.False);EqualCheckpoint(before,s.State);
            Assert.That(s.State.Tiles[Rules.CellId(4,1)].Kind,Is.EqualTo(TileKind.Crop));Assert.That(s.State.Tiles[Rules.CellId(4,3)].Kind,Is.EqualTo(TileKind.Crop));
        }
        [Test] public void NarrowFullCargoRejectsWholeHarvest()
        {
            var s=Session.CreatePrototype();Ok(s.Harvest());Moves(s,1);Turns(s,-1);Ok(s.Harvest());Moves(s,1);Ok(s.Harvest());Moves(s,1);Turns(s,1);Moves(s,1);
            RoundSnapshot before=s.State;Assert.That(s.PreviewHarvest().Legal,Is.False);Assert.That(s.Harvest().Success,Is.False);EqualCheckpoint(before,s.State);Assert.That(s.State.Tiles[Rules.CellId(4,3)].Kind,Is.EqualTo(TileKind.Crop));
        }
        [Test] public void SameStrawBranchesConvertOrPaveAndCannotPayBoth()
        {
            Session convert=TwoCrops(),pave=TwoCrops();RoundSnapshot checkpoint=convert.State;
            Ok(convert.ConvertStraw());Assert.That(convert.State.ConvertedStraw,Is.EqualTo(2));Assert.That(convert.State.Vehicle.Fuel,Is.EqualTo(24));Assert.That(convert.State.FuelAdded,Is.EqualTo(3));
            Assert.That(convert.State.Vehicle.Grain,Is.EqualTo(2));Assert.That(convert.Pave(3,1).Success,Is.False);
            Ok(pave.Pave(3,1));Assert.That(pave.State.PavedStraw,Is.EqualTo(2));Assert.That(pave.State.Vehicle.Fuel,Is.EqualTo(checkpoint.Vehicle.Fuel));Assert.That(pave.State.Tiles[Rules.CellId(3,1)].Paved,Is.True);
            Assert.That(pave.State.Vehicle.Straw,Is.Zero);Assert.That(pave.ConvertStraw().Success,Is.False);Assert.That(pave.Pave(3,1).Success,Is.False);
            Assert.That(pave.State.ActionsRemaining,Is.EqualTo(checkpoint.ActionsRemaining-1));
        }
        [Test] public void PavingChangesActualDriveCostAndPersistsNextRound()
        {
            var s=TwoCrops();Ok(s.Pave(3,1));Turns(s,1);ActionPreview paved=s.PreviewMoveForward();Assert.That(paved.ActionCost,Is.EqualTo(1));Assert.That(paved.FuelCost,Is.EqualTo(1));
            // Return along the cleared first column and finish with one straw already consumed per paving unit.
            Turns(s,-1);Moves(s,1);Ok(s.Harvest());Turns(s,1,2);Moves(s,1);Turns(s,1);Moves(s,1);Ok(s.Deliver(3));Ok(s.ContinueRound(ModuleKind.None));
            Assert.That(s.State.Tiles[Rules.CellId(3,1)].Kind,Is.EqualTo(TileKind.Ground));Assert.That(s.State.Tiles[Rules.CellId(3,1)].Paved,Is.True);Assert.That(s.State.PavedStraw,Is.EqualTo(2));
        }
        [TestCase(-1,0)] [TestCase(10,0)] [TestCase(2,1)] [TestCase(8,8)]
        public void InvalidPaveTargetsDoNotConsumeCargoOrBudget(int x,int y)
        {
            var s=TwoCrops();RoundSnapshot before=s.State;Assert.That(s.PreviewPave(x,y).Legal,Is.False);Assert.That(s.Pave(x,y).Success,Is.False);EqualCheckpoint(before,s.State);
        }
        [Test] public void PartialDeliverAndRefuelHaveSeparateCostsNoFreeDeadline()
        {
            var s=Session.CreatePrototype();Ok(s.Harvest());RoundSnapshot before=s.State;Ok(s.Deliver(1));
            Assert.That(s.State.Vehicle.Grain,Is.Zero);Assert.That(s.State.DeliveredThisRound,Is.EqualTo(1));Assert.That(s.State.Wallet,Is.EqualTo(6));Assert.That(s.State.ActionsRemaining,Is.EqualTo(before.ActionsRemaining-1));
            before=s.State;Assert.That(s.Deliver(1).Success,Is.False);EqualCheckpoint(before,s.State);Ok(s.Refuel());
            Assert.That(s.State.ActionsRemaining,Is.EqualTo(before.ActionsRemaining-1));Assert.That(s.State.Vehicle.Fuel,Is.EqualTo(24));Assert.That(s.State.Wallet,Is.EqualTo(4));
            before=s.State;Assert.That(s.Refuel().Success,Is.False);EqualCheckpoint(before,s.State);Ok(s.UnloadStraw(1));Assert.That(s.State.StoredStraw,Is.EqualTo(1));Assert.That(s.State.Vehicle.Straw,Is.Zero);
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(4)]
        public void DeliveryAmountOutsideActualCargoOrOrderRejects(int amount)
        {
            var s=Session.CreatePrototype();Ok(s.Harvest());RoundSnapshot before=s.State;Assert.That(s.Deliver(amount).Success,Is.False);EqualCheckpoint(before,s.State);
        }
        [Test] public void DepotCommandsCannotRunRemotely()
        {
            var s=Session.CreatePrototype();Ok(s.Harvest());Moves(s,1);RoundSnapshot before=s.State;
            Assert.That(s.Deliver(1).Success,Is.False);Assert.That(s.UnloadStraw(1).Success,Is.False);Assert.That(s.Refuel().Success,Is.False);EqualCheckpoint(before,s.State);
        }
        [TestCase(ModuleKind.None,6,2,3)] [TestCase(ModuleKind.Roller,4,1,1)] [TestCase(ModuleKind.WideHead,6,2,3)] [TestCase(ModuleKind.CargoBox,10,2,4)]
        public void ModulesExposeCapacityAndMudCostsFromSameInitialField(ModuleKind module,int capacity,int mudActions,int mudFuel)
        {
            var s=Session.CreatePrototype(module);Ok(s.Harvest());Moves(s,1);ActionPreview p=s.PreviewMoveForward();
            Assert.That(s.State.Vehicle.Capacity,Is.EqualTo(capacity));Assert.That(p.Legal,Is.True);Assert.That(p.ActionCost,Is.EqualTo(mudActions));Assert.That(p.FuelCost,Is.EqualTo(mudFuel));
            RoundSnapshot before=s.State;Ok(s.MoveForward());Assert.That(s.State.ActionsRemaining,Is.EqualTo(before.ActionsRemaining-mudActions));Assert.That(s.State.Vehicle.Fuel,Is.EqualTo(before.Vehicle.Fuel-mudFuel));
        }
        [Test] public void WideHeadPaysForWiderHarvestAndTurnCannotSwitchToNarrow()
        {
            var s=Session.CreatePrototype(ModuleKind.WideHead);HarvestPreview p=s.PreviewHarvest();Assert.That(p.TargetCells.Length,Is.EqualTo(2));Assert.That(p.GrainProduced,Is.EqualTo(2));Assert.That(p.StrawProduced,Is.EqualTo(2));
            Ok(s.Harvest());Assert.That(s.State.Vehicle.CargoUsed,Is.EqualTo(4));Assert.That(s.State.ActionsRemaining,Is.EqualTo(22));Assert.That(s.State.Vehicle.Fuel,Is.EqualTo(22));
            Ok(s.Turn(-1));Assert.That(s.State.ActionsRemaining,Is.EqualTo(20));Assert.That(s.State.Vehicle.Fuel,Is.EqualTo(21));Assert.That(s.State.Vehicle.Module,Is.EqualTo(ModuleKind.WideHead));
        }
        [Test] public void ZeroFuelAllowsZeroFuelTurnUntilFixedActionDeadline()
        {
            var s=Session.CreatePrototype(ModuleKind.CargoBox);Ok(s.Harvest());Moves(s,1);Turns(s,-1);Ok(s.Harvest());Moves(s,1);
            for(int i=0;i<9;i++){Turns(s,-1);Moves(s,1);}
            Assert.That(s.State.Vehicle.Fuel,Is.Zero);Assert.That(s.State.Phase,Is.EqualTo(Phase.Active));
            RoundSnapshot before=s.State;Assert.That(s.MoveForward().Success,Is.False);EqualCheckpoint(before,s.State);
            Ok(s.Turn(1));Assert.That(s.State.ActionsRemaining,Is.EqualTo(before.ActionsRemaining-1));Assert.That(s.State.Outcome,Is.EqualTo(RoundOutcome.Failed));
        }
        [Test] public void LastActionDeliveryCompletesRoundBeforeDeadlineAndContinuePreservesCargo()
        {
            var s=Session.CreatePrototype();FirstReturn(s);Turns(s,1,12);Assert.That(s.State.ActionsRemaining,Is.EqualTo(1));Ok(s.Deliver(3));
            Assert.That(s.State.ActionsRemaining,Is.Zero);Assert.That(s.State.Phase,Is.EqualTo(Phase.RoundComplete));Assert.That(s.State.Outcome,Is.EqualTo(RoundOutcome.None));Assert.That(s.UnloadStraw(3).Success,Is.False);
            Ok(s.ContinueRound(ModuleKind.Roller));Assert.That(s.State.RoundIndex,Is.EqualTo(2));Assert.That(s.State.ActionsRemaining,Is.EqualTo(32));Assert.That(s.State.Vehicle.Straw,Is.EqualTo(3));Assert.That(s.State.Vehicle.Fuel,Is.EqualTo(17));Assert.That(s.State.Wallet,Is.EqualTo(10));
        }
        [Test] public void FinalActionDeliveryCompletesAllThreeAtZeroBudget()
        {
            var s=Session.CreatePrototype();FirstRound(s);SecondRound(s);ThirdReturn(s);Turns(s,1,22);Assert.That(s.State.ActionsRemaining,Is.EqualTo(1));Ok(s.Deliver(4));
            Assert.That(s.State.ActionsRemaining,Is.Zero);Assert.That(s.State.Phase,Is.EqualTo(Phase.Finished));Assert.That(s.State.Outcome,Is.EqualTo(RoundOutcome.Completed));
            Assert.That(s.MoveForward().Success,Is.False);Assert.That(s.ContinueRound(ModuleKind.None).Success,Is.False);
        }
        [Test] public void BudgetExhaustionFailsAndRetryResetsWholeRun()
        {
            var s=Session.CreatePrototype();Turns(s,1,24);Assert.That(s.State.Outcome,Is.EqualTo(RoundOutcome.Failed));Assert.That(s.State.Vehicle.Fuel,Is.EqualTo(24));Assert.That(s.State.DeliveredTotal,Is.Zero);
            Assert.That(s.Harvest().Success,Is.False);Session retry=s.Retry();Assert.That(retry.State.Phase,Is.EqualTo(Phase.Active));Assert.That(retry.State.Wallet,Is.EqualTo(4));Assert.That(retry.State.ActionsRemaining,Is.EqualTo(24));Assert.That(retry.State.Commands.Length,Is.Zero);
        }
        [Test] public void CompletedRoundOnlyAllowsOldBudgetMaintenanceAndCapacityCheckedContinue()
        {
            var s=Session.CreatePrototype(ModuleKind.CargoBox);
            Ok(s.Harvest());Moves(s,1);Turns(s,-1);Ok(s.Harvest());Moves(s,1);Ok(s.Harvest());Turns(s,1);Moves(s,1);Ok(s.Harvest());
            Turns(s,1,2);Moves(s,2);Turns(s,-1);Moves(s,1);Ok(s.Deliver(3));
            Assert.That(s.State.Vehicle.Grain,Is.EqualTo(1));Assert.That(s.State.Vehicle.Straw,Is.EqualTo(4));RoundSnapshot before=s.State;
            Assert.That(s.ContinueRound(ModuleKind.Roller).Success,Is.False);EqualCheckpoint(before,s.State);Assert.That(s.PreviewContinueRound(ModuleKind.None).Legal,Is.True);
            Assert.That(s.MoveForward().Success,Is.False);Assert.That(s.Turn(1).Success,Is.False);Assert.That(s.Harvest().Success,Is.False);Assert.That(s.Deliver(1).Success,Is.False);Assert.That(s.ConvertStraw().Success,Is.False);Assert.That(s.Pave(2,1).Success,Is.False);
            Ok(s.UnloadStraw(4));Assert.That(s.State.ActionsRemaining,Is.EqualTo(before.ActionsRemaining-1));Ok(s.ContinueRound(ModuleKind.None));
            Assert.That(s.State.Vehicle.Grain,Is.EqualTo(1));Assert.That(s.State.DeliveredThisRound,Is.Zero);Assert.That(s.State.DeliveredTotal,Is.EqualTo(3));Assert.That(s.State.StoredStraw,Is.EqualTo(4));Assert.That(s.State.Vehicle.Module,Is.EqualTo(ModuleKind.None));
        }
        [Test] public void StateAndPreviewAreDetachedAndRepeatedCommandIdHasNoReward()
        {
            var s=Session.CreatePrototype();HarvestPreview p=s.PreviewHarvest();p.TargetCells[0].X=99;RoundSnapshot copy=s.State;copy.Vehicle.Fuel=999;copy.Tiles[0].Kind=TileKind.Crop;
            Ok(s.Harvest("harvest-one"));RoundSnapshot before=s.State;CommandResult duplicate=s.Harvest("harvest-one");Assert.That(duplicate.Success,Is.True);Assert.That(duplicate.AlreadyApplied,Is.True);Assert.That(duplicate.Events.Length,Is.Zero);EqualCheckpoint(before,s.State);
            GameEvent[] returned=s.LastEvents;Assert.That(returned.Length,Is.Zero);Assert.That(s.State.Tiles[0].Kind,Is.EqualTo(TileKind.Ground));
        }
        [Test] public void FullReplayRestoreAtRoundBoundaryAndActiveRunContinuesIdentically()
        {
            var s=Session.CreatePrototype();FirstRound(s);Session restored;string reason;Assert.That(Session.TryRestore(s.ExportSnapshot(),out restored,out reason),Is.True,reason);Assert.That(restored.LastEvents.Length,Is.Zero);
            SecondRound(s);SecondRound(restored);ThirdReturn(s);ThirdReturn(restored);Ok(s.Deliver(4));Ok(restored.Deliver(4));
            Assert.That(restored.State.Wallet,Is.EqualTo(22));Assert.That(restored.State.Vehicle.Fuel,Is.EqualTo(4));Assert.That(restored.State.Events.Select(e=>e.Reason),Is.EqualTo(s.State.Events.Select(e=>e.Reason)));
            Assert.That(Session.ValidateSnapshot(restored.State,out reason),Is.True,reason);
        }
        [TestCase("fuel")] [TestCase("phase")] [TestCase("tile")] [TestCase("wallet")] [TestCase("argument")] [TestCase("round")]
        public void RestoreRejectsForgedButOtherwisePlausibleState(string field)
        {
            var s=Session.CreatePrototype();Ok(s.Harvest());RoundSnapshot snapshot=s.ExportSnapshot();
            switch(field){case "fuel":snapshot.Vehicle.Fuel++;snapshot.FuelAdded++;break;case "phase":snapshot.Phase=Phase.Finished;snapshot.Outcome=RoundOutcome.Completed;break;
                case "tile":snapshot.Tiles[0].Paved=true;break;case "wallet":snapshot.Wallet++;break;case "argument":snapshot.Commands[0].A=1;break;case "round":snapshot.Rounds[0].ActionsSpent++;break;}
            Session restored;string reason;Assert.That(Session.TryRestore(snapshot,out restored,out reason),Is.False);Assert.That(restored,Is.Null);
        }
        [Test] public void UnknownVersionAndMalformedArraysAreRejectedWithoutThrowing()
        {
            RoundSnapshot snapshot=Session.CreatePrototype().State;string reason;snapshot.RulesVersion="future";Assert.That(Session.ValidateSnapshot(snapshot,out reason),Is.False);
            snapshot=Session.CreatePrototype().State;snapshot.Tiles[0]=null;Assert.That(Session.ValidateSnapshot(snapshot,out reason),Is.False);
            snapshot=Session.CreatePrototype().State;snapshot.Vehicle=null;Assert.That(Session.ValidateSnapshot(snapshot,out reason),Is.False);
        }
        [Test] public void FailedRunRestoreIsSafeAndRestoredDuplicateCommandDoesNotConsumeAgain()
        {
            var s=Session.CreatePrototype();Ok(s.Turn(1,"first-turn"));Turns(s,1,23);Session restored;string reason;
            Assert.That(Session.TryRestore(s.State,out restored,out reason),Is.True,reason);Assert.That(restored.State.Outcome,Is.EqualTo(RoundOutcome.Failed));Assert.That(restored.LastEvents.Length,Is.Zero);
            RoundSnapshot before=restored.State;CommandResult duplicate=restored.Turn(1,"first-turn");Assert.That(duplicate.Success,Is.True);Assert.That(duplicate.AlreadyApplied,Is.True);EqualCheckpoint(before,restored.State);
            Assert.That(restored.Turn(1).Success,Is.False);
        }
    }
}
