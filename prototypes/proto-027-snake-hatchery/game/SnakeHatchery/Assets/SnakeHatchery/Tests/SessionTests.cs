using System.Linq;
using NUnit.Framework;

namespace SnakeHatchery.Tests
{
    public sealed class SessionTests
    {
        private static void Ok(CommandResult result) { Assert.IsTrue(result.Success,result.Reason); }
        private static void Walk(Session s,params Direction[] directions) { foreach(Direction d in directions){Ok(s.Move(d));Assert.AreEqual(Phase.Active,s.State.Phase,"unexpected collision");} }
        private static void AssertConserved(Session s)
        {
            RoundSnapshot a=s.State;
            Assert.AreEqual(8,a.Delivered+a.GroundCargo+a.CarriedCargo);
            Assert.AreEqual(7,a.Modules.Length);
            int[] owners=a.Main.Segments.Select(x=>x.ModuleId).Concat(a.Clones.SelectMany(c=>c.Segments).Select(x=>x.ModuleId)).Concat(a.InventoryModuleIds).Concat(a.Modules.Where(m=>m.Location==ModuleLocation.Ground).Select(m=>m.Id)).ToArray();
            CollectionAssert.AreEquivalent(Enumerable.Range(1,7),owners);
            string reason;Assert.IsTrue(Session.ValidateSnapshot(a,out reason),reason);
        }
        private static void ATrip(Session s,int collections=1,bool finish=false)
        {
            Walk(s,Direction.Up,Direction.Up,Direction.Right);
            for(int i=0;i<collections;i++)Ok(s.Collect());
            Walk(s,Direction.Down,Direction.Down,Direction.Left);Ok(s.Deliver());
            if(!finish)Assert.AreEqual(Phase.Active,s.State.Phase);
        }
        private static void BTrip(Session s,int collections=1)
        {
            Walk(s,Direction.Up,Direction.Up,Direction.Right,Direction.Right,Direction.Right,Direction.Right,Direction.Right);
            for(int i=0;i<collections;i++)Ok(s.Collect());
            Walk(s,Direction.Up,Direction.Left,Direction.Left,Direction.Down,Direction.Left,Direction.Left,Direction.Down,Direction.Down,Direction.Left);Ok(s.Deliver());
        }
        private static Session FourSegmentSnake()
        {
            Session s=Session.CreatePrototype();Ok(s.DropModule(4));
            Walk(s,Direction.Up,Direction.Up,Direction.Right,Direction.Down,Direction.Down,Direction.Left);
            Assert.AreEqual(4,s.State.Main.Length);return s;
        }
        [TestCase(PresetKind.Short)] [TestCase(PresetKind.Long)]
        public void SameInputNoCloneGoldenCompletesWithIndependent52ActionBudget(PresetKind preset)
        {
            Session s=Session.CreatePrototype(preset);ATrip(s);ATrip(s);BTrip(s);BTrip(s);
            Assert.AreEqual(28,s.State.ActionsRemaining);Assert.AreEqual(8,s.State.Delivered);
            Assert.AreEqual(0,s.State.Main.Cargo);Assert.AreEqual(0,s.State.GroundCargo);
            Assert.AreEqual(0,s.State.Clones.Length);Assert.AreEqual(RoundOutcome.Completed,s.State.Outcome);
            Assert.AreEqual(preset==PresetKind.Short?3:5,s.State.Main.Length);AssertConserved(s);
        }
        [Test] public void WarehouseCapacitySupportsIndependentlyDerived28ActionRoute()
        {
            Session s=Session.CreatePrototype(PresetKind.Long);ATrip(s,2);BTrip(s,2);
            Assert.AreEqual(52,s.State.ActionsRemaining);Assert.AreEqual(8,s.State.Delivered);Assert.AreEqual(RoundOutcome.Completed,s.State.Outcome);AssertConserved(s);
        }
        [Test] public void A01GrowthAppendsActualModuleAtOldTailAndCapacityChangesOnlyOnIngestion()
        {
            Session s=Session.CreatePrototype();Ok(s.DropModule(4));
            Assert.AreEqual(3,s.State.Main.Length);Assert.AreEqual(2,s.State.Main.Capacity);
            Walk(s,Direction.Up,Direction.Up,Direction.Right,Direction.Down,Direction.Down);
            SegmentState oldTail=s.State.Main.Segments.Last();MovePreview p=s.PreviewMove(Direction.Left);
            Assert.IsTrue(p.Legal);Assert.IsTrue(p.WillGrow);Ok(s.Move(Direction.Left));
            SegmentState grown=s.State.Main.Segments.Last();Assert.AreEqual(4,grown.ModuleId);
            Assert.AreEqual(oldTail.X,grown.X);Assert.AreEqual(oldTail.Y,grown.Y);
            Assert.AreEqual(4,s.State.Main.Capacity);Assert.AreEqual(ModuleLocation.Main,s.State.Modules[3].Location);AssertConserved(s);
        }
        [Test] public void OrdinaryMoveMayEnterOldTailButGrowthMoveCannot()
        {
            Session ordinary=FourSegmentSnake();Walk(ordinary,Direction.Up,Direction.Right,Direction.Down);
            Assert.IsTrue(ordinary.PreviewMove(Direction.Left).Legal);Ok(ordinary.Move(Direction.Left));Assert.AreEqual(Phase.Active,ordinary.State.Phase);
            Session growing=FourSegmentSnake();Ok(growing.DropModule(5));Walk(growing,Direction.Up,Direction.Right,Direction.Down);
            int before=growing.State.ActionsRemaining;MovePreview p=growing.PreviewMove(Direction.Left);Assert.IsFalse(p.Legal);Assert.IsTrue(p.WillGrow);
            StringAssert.Contains("旧尾格",p.Reason);CommandResult committed=growing.Move(Direction.Left);Assert.IsTrue(committed.Success);
            Assert.AreEqual(before-1,growing.State.ActionsRemaining);Assert.AreEqual(RoundOutcome.Failed,growing.State.Outcome);
            Assert.AreEqual(ModuleLocation.Ground,growing.State.Modules[4].Location);AssertConserved(growing);
        }
        [Test] public void A02SplitTransfersOnlyExistingTailAndNewCloneDoesNotMoveThatAction()
        {
            Session s=Session.CreatePrototype();GridPoint[] route=Rules.SuggestedRoute(0,0,CloneBehavior.PatrolCollect);
            SplitPreview p=s.PreviewSplit(2,CloneBehavior.PatrolCollect,route);Assert.IsTrue(p.Legal);Assert.AreEqual(1,p.LostCollectionRate);
            Ok(s.Split(2,CloneBehavior.PatrolCollect,route));RoundSnapshot a=s.State;
            Assert.AreEqual(79,a.ActionsRemaining);Assert.AreEqual(2,a.Main.Length);Assert.AreEqual(1,a.Main.CollectionRate);
            Assert.AreEqual(1,a.Clones.Single().Length);Assert.AreEqual(3,a.Clones[0].Segments[0].ModuleId);
            Assert.AreEqual(0,a.Clones[0].RouteIndex);Assert.AreEqual(0,a.Clones[0].HeadX);Assert.AreEqual(0,a.Clones[0].HeadY);
            Assert.IsFalse(s.LastEvents.Any(e=>e.Kind==EventKind.CloneMoved||e.Kind==EventKind.Collected));AssertConserved(s);
        }
        [Test] public void WarehouseSplitLosesCapacityAndTransfersOverflowToActualClone()
        {
            Session s=Session.CreatePrototype(PresetKind.Long);Walk(s,Direction.Up,Direction.Up,Direction.Right);Ok(s.Collect());Ok(s.Collect());
            var route=new[]{new GridPoint(1,2),new GridPoint(0,2)};SplitPreview p=s.PreviewSplit(2,CloneBehavior.PatrolCollect,route);
            Assert.IsTrue(p.Legal);Assert.AreEqual(4,p.LostCapacity);Assert.AreEqual(1,p.LostCollectionRate);Assert.AreEqual(2,p.MainCapacityAfter);Assert.AreEqual(6,p.CloneCapacity);Assert.AreEqual(2,p.TransferredCargo);Assert.AreEqual(0,p.DroppedCargo);
            Ok(s.Split(2,CloneBehavior.PatrolCollect,route));Assert.AreEqual(2,s.State.Main.Cargo);Assert.AreEqual(2,s.State.Main.Capacity);Assert.AreEqual(2,s.State.Clones[0].Cargo);CollectionAssert.AreEqual(new[]{3,4,5},s.State.Clones[0].Segments.Select(x=>x.ModuleId));AssertConserved(s);
        }
        [Test] public void A03ThirdCloneIsRejectedWithoutAdvancingAnyEntity()
        {
            Session s=Session.CreatePrototype(PresetKind.Long);
            Ok(s.Split(4,CloneBehavior.PatrolCollect,Rules.SuggestedRoute(0,2,CloneBehavior.PatrolCollect)));
            Ok(s.Split(3,CloneBehavior.PatrolCollect,Rules.SuggestedRoute(0,1,CloneBehavior.PatrolCollect)));
            Assert.AreEqual(3,s.State.Main.Length);Assert.AreEqual(2,s.State.Clones.Length);
            RoundSnapshot before=s.State;CommandResult denied=s.Split(2,CloneBehavior.PatrolCollect,Rules.SuggestedRoute(before.Main.Segments[2].X,before.Main.Segments[2].Y,CloneBehavior.PatrolCollect));
            Assert.IsFalse(denied.Success);StringAssert.Contains("两条",denied.Reason);
            Assert.AreEqual(before.ActionsRemaining,s.State.ActionsRemaining);CollectionAssert.AreEqual(before.Clones.Select(c=>c.RouteIndex),s.State.Clones.Select(c=>c.RouteIndex));AssertConserved(s);
        }
        [Test] public void MainOccupancyAndStableCloneOccupancyCauseVisibleWaitWithoutDamage()
        {
            Session s=Session.CreatePrototype();Ok(s.Split(2,CloneBehavior.PatrolCollect,Rules.SuggestedRoute(0,0,CloneBehavior.PatrolCollect)));
            Ok(s.Wait());Assert.AreEqual(0,s.State.Clones[0].RouteIndex);StringAssert.Contains("实体0",s.State.Clones[0].WaitingReason);Assert.AreEqual(Phase.Active,s.State.Phase);
            Walk(s,Direction.Right,Direction.Up);Assert.AreEqual(2,s.State.Clones[0].RouteIndex);
            Ok(s.Wait());StringAssert.Contains("实体0",s.State.Clones[0].WaitingReason);Assert.AreEqual(Phase.Active,s.State.Phase);AssertConserved(s);
        }
        [Test] public void OlderCloneBlockedByNewCloneWaitsAndUsesStableIds()
        {
            Session s=Session.CreatePrototype(PresetKind.Long);
            Ok(s.Split(4,CloneBehavior.PatrolCollect,Rules.SuggestedRoute(0,2,CloneBehavior.PatrolCollect)));
            Ok(s.Split(2,CloneBehavior.PatrolCollect,Rules.SuggestedRoute(0,0,CloneBehavior.PatrolCollect)));
            CollectionAssert.AreEqual(new[]{1,2},s.State.Clones.Select(c=>c.Id));
            Assert.AreEqual(0,s.State.Clones[0].RouteIndex);StringAssert.Contains("实体2",s.State.Clones[0].WaitingReason);
            Assert.AreEqual(0,s.State.Clones[1].RouteIndex);Assert.AreEqual(Phase.Active,s.State.Phase);AssertConserved(s);
        }
        [Test] public void PublicWallInPatrolRouteWaitsInsteadOfHarmingMain()
        {
            Session s=Session.CreatePrototype(PresetKind.Long);
            GridPoint[] route={new GridPoint(0,2),new GridPoint(1,2),new GridPoint(2,2),new GridPoint(3,2)};
            SplitPreview preview=s.PreviewSplit(4,CloneBehavior.PatrolCollect,route);Assert.IsTrue(preview.Legal);StringAssert.Contains("墙格",preview.Reason);
            Ok(s.Split(4,CloneBehavior.PatrolCollect,route));Walk(s,Direction.Up,Direction.Up,Direction.Right,Direction.Right,Direction.Right,Direction.Up);Ok(s.Wait());
            Assert.AreEqual(2,s.State.Clones[0].RouteIndex);StringAssert.Contains("墙格",s.State.Clones[0].WaitingReason);Assert.AreEqual(Phase.Active,s.State.Phase);AssertConserved(s);
        }
        private static Session LoadedPatrol()
        {
            Session s=Session.CreatePrototype();Ok(s.Split(2,CloneBehavior.PatrolCollect,Rules.SuggestedRoute(0,0,CloneBehavior.PatrolCollect)));
            Walk(s,Direction.Right,Direction.Up,Direction.Up,Direction.Left,Direction.Up,Direction.Right);
            for(int i=0;i<8;i++)Ok(s.Wait());
            Assert.IsTrue(s.State.Clones[0].TaskComplete);Assert.AreEqual(2,s.State.Clones[0].Cargo);return s;
        }
        [Test] public void A04PatrolReclaimDropsActualModuleWhenInventoryFullAndDoesNotReattach()
        {
            Session s=LoadedPatrol();Walk(s,Direction.Down,Direction.Left,Direction.Down,Direction.Down,Direction.Left);
            Assert.AreEqual(0,s.State.Main.HeadX);Assert.AreEqual(1,s.State.Main.HeadY);Ok(s.Reclaim(1));
            Assert.AreEqual(0,s.State.Clones.Length);Assert.AreEqual(2,s.State.Main.Length);Assert.AreEqual(2,s.State.Main.Cargo);
            ModuleState recovered=s.State.Modules[2];Assert.AreEqual(ModuleLocation.Ground,recovered.Location);Assert.AreEqual(0,recovered.X);Assert.AreEqual(0,recovered.Y);
            Assert.AreEqual(2,s.State.InventoryModuleIds.Length);AssertConserved(s);
            int remaining=s.State.ActionsRemaining;Assert.IsFalse(s.Reclaim(1).Success);Assert.AreEqual(remaining,s.State.ActionsRemaining);
        }
        [Test] public void ReclaimWithFullMainCargoLeavesOverflowVisibleAndUnique()
        {
            Session s=LoadedPatrol(); // main rate1 after split, clone has the first two units.
            Walk(s,Direction.Down);Ok(s.Collect());Ok(s.Collect());
            Walk(s,Direction.Down,Direction.Left,Direction.Down,Direction.Left);
            ReclaimPreview p=s.PreviewReclaim(1);Assert.IsTrue(p.Legal);Assert.AreEqual(1,p.Cost);Assert.AreEqual(0,p.TransferredCargo);Assert.AreEqual(2,p.DroppedCargo);Assert.AreEqual(0,p.ModulesToInventory);Assert.AreEqual(1,p.DroppedModules);Assert.AreEqual(0,p.DropX);Assert.AreEqual(0,p.DropY);
            int before=s.State.ActionsRemaining;Ok(s.Reclaim(1));Assert.AreEqual(before-1,s.State.ActionsRemaining);
            Assert.AreEqual(2,s.State.Main.Cargo);Assert.AreEqual(2,s.State.Cells[Rules.CellId(0,0)].Cargo);AssertConserved(s);
        }
        [Test] public void ReclaimWithEmptyInventoryKeepsActualModuleWithoutRestoringBodyCapacity()
        {
            Session s=Session.CreatePrototype(PresetKind.Long);Ok(s.Split(4,CloneBehavior.PatrolCollect,new[]{new GridPoint(0,2),new GridPoint(0,3)}));Walk(s,Direction.Up,Direction.Up);
            ReclaimPreview p=s.PreviewReclaim(1);Assert.IsTrue(p.Legal);Assert.AreEqual(1,p.ModulesToInventory);Assert.AreEqual(0,p.DroppedModules);
            Ok(s.Reclaim(1));CollectionAssert.AreEqual(new[]{5},s.State.InventoryModuleIds);Assert.AreEqual(ModuleLocation.Inventory,s.State.Modules[4].Location);Assert.AreEqual(4,s.State.Main.Length);Assert.AreEqual(4,s.State.Main.Capacity);AssertConserved(s);
        }
        [Test] public void ShuttleAutomaticallyDeliversOnlyActualCargoAtDock()
        {
            Session s=Session.CreatePrototype();Ok(s.Split(2,CloneBehavior.Shuttle,Rules.SuggestedRoute(0,0,CloneBehavior.Shuttle)));
            Walk(s,Direction.Right,Direction.Up,Direction.Up,Direction.Left,Direction.Up,Direction.Right);
            for(int i=0;i<10;i++)Ok(s.Wait());
            Assert.AreEqual(2,s.State.Delivered);Assert.IsFalse(s.State.Clones[0].TaskComplete);Assert.IsTrue(s.State.Events.Any(e=>e.EntityId==1&&e.Kind==EventKind.Delivered));AssertConserved(s);
        }
        [Test] public void PickupAndDropDoNotGivePassiveCapacityOrCopyModules()
        {
            Session s=Session.CreatePrototype(PresetKind.Long);Walk(s,Direction.Up,Direction.Up,Direction.Up);
            Ok(s.CollectModule(6));Assert.AreEqual(5,s.State.Main.Length);Assert.AreEqual(2,s.State.Main.CollectionRate);
            Assert.AreEqual(ModuleLocation.Inventory,s.State.Modules[5].Location);Ok(s.DropModule(6));Assert.AreEqual(5,s.State.Main.Length);Assert.AreEqual(ModuleLocation.Ground,s.State.Modules[5].Location);AssertConserved(s);
        }
        [Test] public void InvalidMenusAndEmptyCapacityActionsDoNotAdvanceClonesOrBudget()
        {
            Session s=Session.CreatePrototype();int remaining=s.State.ActionsRemaining;
            Assert.IsFalse(s.Collect().Success);Assert.IsFalse(s.Deliver().Success);Assert.IsFalse(s.Reclaim(99).Success);
            Assert.IsFalse(s.Split(1,CloneBehavior.PatrolCollect,new GridPoint[0]).Success);Assert.IsFalse(s.Split(2,CloneBehavior.Shuttle,new[]{new GridPoint(0,0),new GridPoint(0,1)}).Success);
            Assert.IsFalse(s.CollectModule(6).Success);Assert.IsFalse(s.Move((Direction)99).Success);Assert.AreEqual(remaining,s.State.ActionsRemaining);AssertConserved(s);
        }
        [Test] public void FullCargoLeavesUncollectedGroundAndRefusedCollectCostsNothing()
        {
            Session s=Session.CreatePrototype();Walk(s,Direction.Up,Direction.Up,Direction.Right);Ok(s.Collect());int before=s.State.ActionsRemaining;
            Assert.IsFalse(s.Collect().Success);Assert.AreEqual(before,s.State.ActionsRemaining);Assert.AreEqual(2,s.State.Cells[Rules.CellId(2,3)].Cargo);AssertConserved(s);
        }
        [Test] public void FinalEffectiveDeliveryWinsBeforeBudgetExpiry()
        {
            Session s=Session.CreatePrototype();ATrip(s);ATrip(s);BTrip(s);
            Walk(s,Direction.Up,Direction.Up,Direction.Right,Direction.Right,Direction.Right,Direction.Right,Direction.Right);Ok(s.Collect());
            Walk(s,Direction.Up,Direction.Left,Direction.Left,Direction.Down,Direction.Left,Direction.Left,Direction.Down,Direction.Down,Direction.Left);
            Assert.AreEqual(29,s.State.ActionsRemaining);for(int i=0;i<28;i++)Ok(s.Wait());Ok(s.Deliver());
            Assert.AreEqual(0,s.State.ActionsRemaining);Assert.AreEqual(RoundOutcome.Completed,s.State.Outcome);AssertConserved(s);
        }
        [Test] public void BudgetAndCollisionFailExplicitlyAndRetryUsesFreshSamePreset()
        {
            Session s=Session.CreatePrototype(PresetKind.Long);for(int i=0;i<80;i++)Ok(s.Wait());
            Assert.AreEqual(RoundOutcome.Failed,s.State.Outcome);Assert.IsFalse(s.Wait().Success);
            Session retry=s.Retry();Assert.AreEqual(80,retry.State.ActionsRemaining);Assert.AreEqual(5,retry.State.Main.Length);Assert.AreEqual(0,retry.State.Commands.Length);
            CommandResult collision=retry.Move(Direction.Down);Assert.IsTrue(collision.Success);Assert.AreEqual(RoundOutcome.Failed,retry.State.Outcome);Assert.AreEqual(79,retry.State.ActionsRemaining);AssertConserved(retry);
        }
        [Test] public void TwoSegmentMainStillCannotReverseThroughItsNeck()
        {
            Session s=Session.CreatePrototype();Ok(s.Split(2,CloneBehavior.PatrolCollect,Rules.SuggestedRoute(0,0,CloneBehavior.PatrolCollect)));
            MovePreview preview=s.PreviewMove(Direction.Down);Assert.IsFalse(preview.Legal);StringAssert.Contains("反向",preview.Reason);
            Ok(s.Move(Direction.Down));Assert.AreEqual(78,s.State.ActionsRemaining);Assert.AreEqual(RoundOutcome.Failed,s.State.Outcome);AssertConserved(s);
        }
        [Test] public void FullRestoreReplaysProductionCommandsButGivesNoNewEventsOrRewards()
        {
            Session s=LoadedPatrol();RoundSnapshot checkpoint=s.ExportSnapshot();Session restored;string reason;
            Assert.IsTrue(Session.TryRestore(checkpoint,out restored,out reason),reason);Assert.AreEqual(0,restored.LastEvents.Length);
            Assert.AreEqual(checkpoint.ActionsRemaining,restored.State.ActionsRemaining);Assert.AreEqual(checkpoint.Clones[0].Cargo,restored.State.Clones[0].Cargo);
            int actions=restored.State.ActionsRemaining;CommandResult duplicate=restored.Wait(checkpoint.Commands[0].Id);Assert.IsTrue(duplicate.AlreadyApplied);Assert.AreEqual(0,duplicate.Events.Length);Assert.AreEqual(actions,restored.State.ActionsRemaining);
            checkpoint.Main.Segments[0].X=7;Assert.AreNotEqual(7,restored.State.Main.HeadX);AssertConserved(restored);
        }
        [TestCase("budget")] [TestCase("cargo")] [TestCase("ownership")] [TestCase("version")] [TestCase("phase")] [TestCase("route")]
        public void ForgedOrPartialSnapshotsAreRejected(string mutation)
        {
            Session s=LoadedPatrol();RoundSnapshot snapshot=s.ExportSnapshot();
            switch(mutation){case "budget":snapshot.ActionsRemaining++;break;case "cargo":snapshot.Main.Cargo++;break;case "ownership":snapshot.InventoryModuleIds[0]=3;break;case "version":snapshot.RulesVersion="99";break;case "phase":snapshot.Phase=Phase.Finished;snapshot.Outcome=RoundOutcome.Completed;break;case "route":snapshot.Clones[0].Route[1].X=7;break;}
            string reason;Assert.IsFalse(Session.ValidateSnapshot(snapshot,out reason));
        }
    }
}
