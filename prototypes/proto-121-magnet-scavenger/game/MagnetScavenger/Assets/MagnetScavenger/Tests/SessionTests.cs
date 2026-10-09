using System.Linq;
using NUnit.Framework;

namespace MagnetScavenger.Tests
{
    public sealed class SessionTests
    {
        private static void Ok(CommandResult result) { Assert.IsTrue(result.Success,result.Reason); }
        private static void Move(Session s,Direction direction,int count=1) { for(int i=0;i<count;i++)Ok(s.Move(direction)); }
        private static void Turn(Session s,int quarters,int count=1) { for(int i=0;i<count;i++)Ok(s.Rotate(quarters)); }
        private static void AssertOwners(Session s)
        {
            RoundSnapshot a=s.State;int[] owners=a.ChainIds.Concat(a.StorageIds).Concat(a.SoldIds).Concat(a.PendingItemId>0?new[]{a.PendingItemId}:new int[0]).Concat(a.Items.Where(i=>i.Location==ItemLocation.Crate).Select(i=>i.Id)).ToArray();
            CollectionAssert.AreEquivalent(Enumerable.Range(1,5),owners);Assert.AreEqual(owners.Length,owners.Distinct().Count());
            string reason;Assert.IsTrue(Session.ValidateSnapshot(a,out reason),reason);
        }
        private static Session RodTool(OrderKind order=OrderKind.Parts,bool clearShort=false)
        {
            Session s=Session.CreatePrototype(order);if(clearShort){Ok(s.Attract());Ok(s.Sell(1));}
            Move(s,Direction.Up,2);Ok(s.Attract());Assert.AreEqual(2,s.State.PendingItemId);Ok(s.Retain());return s;
        }
        private static Session PartsChain()
        {
            Session s=RodTool();Ok(s.Attract());Assert.AreEqual(3,s.State.PendingItemId);Ok(s.Retain());return s;
        }
        private static void ReachHeavyPending(Session s)
        {
            Move(s,Direction.Right,5);Move(s,Direction.Up);Move(s,Direction.Right,2);Move(s,Direction.Up,2);Turn(s,-1,2);
            AttractPreview p=s.PreviewAttract();Assert.IsTrue(p.Legal,p.Reason);Assert.AreEqual(4,p.ItemId);Ok(s.Attract());
        }
        private static void ReturnHeavyToDepot(Session s)
        {
            Move(s,Direction.Left,5);Turn(s,-1);Move(s,Direction.Down,4);Turn(s,1);Move(s,Direction.Left);
        }
        [Test] public void PartsChainGoldenHasIndependent12EnergyAnd15Wallet()
        {
            Session s=PartsChain();Ok(s.Detach(0));Ok(s.Sell(3));Ok(s.Sell(2));
            Assert.AreEqual(48,s.State.Energy);Assert.AreEqual(15,s.State.Wallet);Assert.AreEqual(9,s.State.ActionIndex);
            Assert.AreEqual(RoundOutcome.Completed,s.State.Outcome);Assert.AreEqual(2,s.State.Order.Delivered);
            Assert.AreEqual(0,s.State.TotalWeight);Assert.IsEmpty(s.State.ChainIds);Assert.IsEmpty(s.State.StorageIds);Assert.AreEqual(0,s.State.PendingItemId);CollectionAssert.AreEqual(new[]{2,3},s.State.SoldIds);AssertOwners(s);
        }
        [Test] public void PartsEmptyHeadGoldenDoesNotRequireRetainedTool()
        {
            Session s=Session.CreatePrototype();Move(s,Direction.Up,2);Ok(s.Attract());Ok(s.Sell(2));Move(s,Direction.Right,3);Ok(s.Attract());Turn(s,-1);Move(s,Direction.Left,2);Ok(s.Sell(3));
            Assert.AreEqual(45,s.State.Energy);Assert.AreEqual(15,s.State.Wallet);Assert.AreEqual(12,s.State.ActionIndex);Assert.AreEqual(RoundOutcome.Completed,s.State.Outcome);Assert.IsEmpty(s.State.ChainIds);AssertOwners(s);
        }
        [Test] public void HeavyEmptyHeadGoldenHasIndependent39EnergyAnd22Wallet()
        {
            Session s=Session.CreatePrototype(OrderKind.Heavy);Ok(s.Attract());Ok(s.Sell(1));Move(s,Direction.Up,2);Ok(s.Attract());Ok(s.Sell(2));ReachHeavyPending(s);
            CollectionAssert.AreEquivalent(new[]{"7,6","6,6","7,5","6,5"},s.State.Items[3].Cells.Select(c=>c.X+","+c.Y));
            ReturnHeavyToDepot(s);Ok(s.Sell(4));Assert.AreEqual(21,s.State.Energy);Assert.AreEqual(22,s.State.Wallet);Assert.AreEqual(32,s.State.ActionIndex);
            Assert.AreEqual(RoundOutcome.Completed,s.State.Outcome);CollectionAssert.AreEqual(new[]{1,2,4},s.State.SoldIds);Assert.AreEqual(2,s.State.RootX);Assert.AreEqual(2,s.State.RootY);AssertOwners(s);
        }
        [Test] public void A01RetainedRodChangesUniqueEndAndActualReachAtSameRoot()
        {
            Session s=Session.CreatePrototype();Move(s,Direction.Up,2);AttractPreview empty=s.PreviewAttract();Assert.AreEqual(2,empty.ItemId);Assert.AreEqual(3,empty.Distance);
            Ok(s.Attract());Assert.AreEqual(1,s.State.EndX);Assert.AreEqual(3,s.State.EndY);Assert.AreEqual(2,s.State.TotalWeight);Assert.IsFalse(s.PreviewAttract().Legal);
            Ok(s.Retain());Assert.AreEqual(4,s.State.EndX);Assert.AreEqual(3,s.State.EndY);Assert.AreEqual(Direction.Right,s.State.EndDirection);
            AttractPreview extended=s.PreviewAttract();Assert.IsTrue(extended.Legal,extended.Reason);Assert.AreEqual(3,extended.ItemId);Assert.AreEqual(4,extended.Distance);
            CollectionAssert.AreEquivalent(new[]{"5,3","6,3","6,4"},extended.CandidateCells.Select(c=>c.X+","+c.Y));
            Ok(s.Attract());CollectionAssert.AreEquivalent(extended.CandidateCells.Select(c=>c.X+","+c.Y),s.State.Items[2].Cells.Select(c=>c.X+","+c.Y));AssertOwners(s);
        }
        [Test] public void ElbowChangesExitDirectionAndNextConnectorGeometry()
        {
            Session s=PartsChain();Assert.AreEqual(6,s.State.EndX);Assert.AreEqual(4,s.State.EndY);Assert.AreEqual(Direction.Up,s.State.EndDirection);
            Ok(s.Detach(0));Ok(s.Attach(3));Assert.AreEqual(3,s.State.EndX);Assert.AreEqual(4,s.State.EndY);Assert.AreEqual(Direction.Up,s.State.EndDirection);
            Ok(s.Attach(2));Assert.AreEqual(3,s.State.EndX);Assert.AreEqual(7,s.State.EndY);Assert.AreEqual(Direction.Up,s.State.EndDirection);
            CollectionAssert.AreEquivalent(new[]{"3,5","3,6","3,7"},s.State.Items[1].Cells.Select(c=>c.X+","+c.Y));CollectionAssert.AreEqual(new[]{3,2},s.State.ChainIds);AssertOwners(s);
        }
        [Test] public void RootRotationMovesEntireLockedRodAndRootMovementPreservesRelativeShape()
        {
            Session s=RodTool();Turn(s,-1);CollectionAssert.AreEquivalent(new[]{"1,4","1,5","1,6"},s.State.Items[1].Cells.Select(c=>c.X+","+c.Y));
            Assert.AreEqual(Direction.Up,s.State.Items[1].Facing);Assert.AreEqual(1,s.State.EndX);Assert.AreEqual(6,s.State.EndY);
            Move(s,Direction.Right);CollectionAssert.AreEquivalent(new[]{"2,4","2,5","2,6"},s.State.Items[1].Cells.Select(c=>c.X+","+c.Y));AssertOwners(s);
        }
        [Test] public void A02OverweightFirstHeavyNeverJumpsToShortBehind()
        {
            Session s=RodTool();Move(s,Direction.Up,2);Move(s,Direction.Right);AttractPreview p=s.PreviewAttract();
            Assert.AreEqual(4,p.ItemId);Assert.AreEqual(1,p.Distance);Assert.AreEqual(7,p.AfterWeight);Assert.IsFalse(p.Legal);StringAssert.Contains("不跳过",p.Reason);
            Assert.AreEqual(6,p.FirstX);Assert.AreEqual(5,p.FirstY);RoundSnapshot before=s.State;Assert.IsFalse(s.Attract().Success);
            Assert.AreEqual(before.Energy,s.State.Energy);Assert.AreEqual(ItemLocation.Crate,s.State.Items[3].Location);Assert.AreEqual(ItemLocation.Crate,s.State.Items[4].Location);Assert.AreEqual(0,s.State.PendingItemId);AssertOwners(s);
        }
        [Test] public void WallStopsRayBeforeBehindItemAndNoEnergyIsCharged()
        {
            Session s=Session.CreatePrototype();Move(s,Direction.Up,2);Move(s,Direction.Right,2);Turn(s,1); // root3,3 Down, no crate at root.
            Move(s,Direction.Down,2);Move(s,Direction.Right,2);Turn(s,-1,2); // root5,1 Up, wall5,2 before rod5,3.
            AttractPreview p=s.PreviewAttract();Assert.IsFalse(p.Legal);Assert.AreEqual(0,p.ItemId);Assert.AreEqual(5,p.BlockX);Assert.AreEqual(2,p.BlockY);Assert.AreEqual(1,p.RayCells.Length);
            int energy=s.State.Energy;Assert.IsFalse(s.Attract().Success);Assert.AreEqual(energy,s.State.Energy);AssertOwners(s);
        }
        [Test] public void A03SellRemovedToolCannotExtendOrGiveRepeatedMoney()
        {
            Session s=Session.CreatePrototype();Ok(s.Attract());Ok(s.Retain());Assert.AreEqual(2,s.State.EndX);Assert.AreEqual(1,s.State.TotalWeight);Ok(s.Sell(1));
            Assert.AreEqual(3,s.State.Wallet);Assert.AreEqual(0,s.State.TotalWeight);Assert.AreEqual(1,s.State.EndX);Assert.AreEqual(1,s.State.EndY);Assert.AreEqual(0,s.State.Order.Delivered);
            int energy=s.State.Energy;Assert.IsFalse(s.Sell(1).Success);Assert.AreEqual(energy,s.State.Energy);Assert.AreEqual(3,s.State.Wallet);AssertOwners(s);
        }
        [Test] public void PendingMayMoveRotateButNeverExtendReAttractOrBypassDisposition()
        {
            Session s=Session.CreatePrototype();Ok(s.Attract());Assert.AreEqual(Phase.AwaitingDisposition,s.State.Phase);Assert.AreEqual(0,s.State.Wallet);
            Move(s,Direction.Up);Turn(s,-1);Assert.AreEqual(1,s.State.EndX);Assert.AreEqual(2,s.State.EndY);Assert.AreEqual(Direction.Up,s.State.EndDirection);
            Assert.AreEqual(1,s.State.Items[0].AnchorX);Assert.AreEqual(3,s.State.Items[0].AnchorY);int energy=s.State.Energy;
            Assert.IsFalse(s.Attract().Success);Assert.IsFalse(s.Detach(0).Success);Assert.IsFalse(s.Attach(2).Success);Assert.IsFalse(s.Sell(2).Success);Assert.AreEqual(energy,s.State.Energy);AssertOwners(s);
        }
        [Test] public void PendingSaleOutsideDepotRemainsOwnedWithoutMoneyOrOrderProgress()
        {
            Session s=Session.CreatePrototype();Ok(s.Attract());Move(s,Direction.Up);Move(s,Direction.Right,2);int energy=s.State.Energy;
            Assert.IsFalse(s.Sell(1).Success);Assert.AreEqual(1,s.State.PendingItemId);Assert.AreEqual(ItemLocation.Pending,s.State.Items[0].Location);
            Assert.AreEqual(0,s.State.Wallet);Assert.AreEqual(0,s.State.Order.Delivered);Assert.AreEqual(energy,s.State.Energy);Ok(s.Retain());AssertOwners(s);
        }
        [Test] public void A04MiddleDetachStoresWholeSuffixAndReattachHasRealCost()
        {
            Session s=PartsChain();ActionPreview p=s.PreviewDetach(0);Assert.IsTrue(p.Legal);CollectionAssert.AreEqual(new[]{2,3},p.AffectedItemIds);Assert.AreEqual(0,p.AfterWeight);
            int energy=s.State.Energy;Ok(s.Detach(0));Assert.AreEqual(energy-2,s.State.Energy);CollectionAssert.AreEqual(new[]{2,3},s.State.StorageIds);
            Assert.IsEmpty(s.State.ChainIds);Assert.AreEqual(1,s.State.EndX);Assert.AreEqual(3,s.State.EndY);Assert.AreEqual(0,s.State.TotalWeight);
            Ok(s.Attach(2));Assert.AreEqual(energy-3,s.State.Energy);CollectionAssert.AreEqual(new[]{2},s.State.ChainIds);CollectionAssert.AreEqual(new[]{3},s.State.StorageIds);AssertOwners(s);
        }
        [Test] public void MiddleSaleAndOutsideDepotDetachAreRefusedAsWholeTransactions()
        {
            Session s=PartsChain();int energy=s.State.Energy;Assert.IsFalse(s.Sell(2).Success);Assert.AreEqual(energy,s.State.Energy);CollectionAssert.AreEqual(new[]{2,3},s.State.ChainIds);
            Move(s,Direction.Right,2);energy=s.State.Energy;Assert.IsFalse(s.Detach(0).Success);Assert.IsFalse(s.Sell(3).Success);Assert.AreEqual(energy,s.State.Energy);AssertOwners(s);
        }
        [Test] public void A05NarrowGapRequiresActualShorteningBeforeMovingIntoWallRow()
        {
            Session s=RodTool();Move(s,Direction.Right);Ok(s.Attract());Ok(s.Retain());int energy=s.State.Energy;
            ActionPreview blocked=s.PreviewMove(Direction.Up);Assert.IsFalse(blocked.Legal);StringAssert.Contains("撞墙",blocked.Reason);Assert.IsFalse(s.Move(Direction.Up).Success);Assert.AreEqual(energy,s.State.Energy);
            Ok(s.Detach(0));Assert.IsTrue(s.PreviewMove(Direction.Up).Legal);Move(s,Direction.Up);Assert.AreEqual(2,s.State.RootX);Assert.AreEqual(4,s.State.RootY);Assert.IsEmpty(s.State.ChainIds);CollectionAssert.AreEqual(new[]{2,3},s.State.StorageIds);AssertOwners(s);
        }
        [Test] public void StorageAttachRechecksWeightAndRefusedAttemptKeepsBothOwners()
        {
            Session s=RodTool(OrderKind.Parts,true);Ok(s.Detach(0));ReachHeavyPending(s);ReturnHeavyToDepot(s);Ok(s.Retain());Ok(s.Detach(0));Turn(s,1);Ok(s.Attach(2));
            int energy=s.State.Energy;ActionPreview p=s.PreviewAttach(4);Assert.IsFalse(p.Legal);Assert.AreEqual(7,p.AfterWeight);Assert.IsFalse(s.Attach(4).Success);
            Assert.AreEqual(energy,s.State.Energy);CollectionAssert.AreEqual(new[]{2},s.State.ChainIds);CollectionAssert.AreEqual(new[]{4},s.State.StorageIds);AssertOwners(s);
        }
        [Test] public void InsufficientDetachRejectsWholeSuffixThenRealLastSaleCanFailOrder()
        {
            Session s=PartsChain();for(int i=0;i<25;i++){Move(s,Direction.Right);Move(s,Direction.Left);}Move(s,Direction.Right);Assert.AreEqual(1,s.State.Energy);
            Assert.IsFalse(s.Detach(0).Success);Assert.AreEqual(1,s.State.Energy);CollectionAssert.AreEqual(new[]{2,3},s.State.ChainIds);Assert.IsEmpty(s.State.StorageIds);
            Ok(s.Sell(3));Assert.AreEqual(0,s.State.Energy);Assert.AreEqual(RoundOutcome.Failed,s.State.Outcome);Assert.AreEqual(7,s.State.Wallet);Assert.IsFalse(s.Sell(2).Success);AssertOwners(s);
        }
        [Test] public void FinalEffectiveSaleCompletesOrderBeforeEnergyDeadline()
        {
            Session s=PartsChain();Ok(s.Sell(3));for(int i=0;i<25;i++){Move(s,Direction.Right);Move(s,Direction.Left);}Assert.AreEqual(1,s.State.Energy);Ok(s.Sell(2));
            Assert.AreEqual(0,s.State.Energy);Assert.AreEqual(RoundOutcome.Completed,s.State.Outcome);Assert.AreEqual(15,s.State.Wallet);AssertOwners(s);
        }
        [Test] public void BlockedRootAndUnknownCommandsLeaveWholeStateAndEnergyUnchanged()
        {
            Session s=Session.CreatePrototype();Assert.IsFalse(s.Move(Direction.Right).Success);Assert.IsFalse(s.Move((Direction)99).Success);Assert.IsFalse(s.Rotate(2).Success);Assert.IsFalse(s.Retain().Success);Assert.IsFalse(s.Attach(1).Success);Assert.IsFalse(s.Detach(-1).Success);Assert.IsFalse(s.Sell(5).Success);
            Assert.AreEqual(60,s.State.Energy);Assert.AreEqual(0,s.State.ActionIndex);Assert.AreEqual(1,s.State.RootX);Assert.AreEqual(0,s.State.Events.Length);AssertOwners(s);
        }
        [Test] public void CompletedSnapshotRestoreIsValidAndRetryStartsSameOrderWithFreshOwnership()
        {
            Session s=Session.CreatePrototype(OrderKind.Heavy);Ok(s.Attract());Ok(s.Sell(1));Move(s,Direction.Up,2);Ok(s.Attract());Ok(s.Sell(2));ReachHeavyPending(s);ReturnHeavyToDepot(s);Ok(s.Sell(4));
            Session restored;string reason;Assert.IsTrue(Session.TryRestore(s.ExportSnapshot(),out restored,out reason),reason);Assert.AreEqual(RoundOutcome.Completed,restored.State.Outcome);Assert.AreEqual(0,restored.LastEvents.Length);Assert.IsFalse(restored.Attract().Success);
            Session retry=restored.Retry();Assert.AreEqual(OrderKind.Heavy,retry.State.OrderKind);Assert.AreEqual(60,retry.State.Energy);Assert.AreEqual(0,retry.State.Wallet);Assert.IsEmpty(retry.State.SoldIds);Assert.AreEqual(5,retry.State.Items.Count(i=>i.Location==ItemLocation.Crate));AssertOwners(retry);
        }
        [Test] public void PendingRestoreAndDuplicateIdentityGiveNoNewMoneyOrEnergy()
        {
            Session s=RodTool();Ok(s.Attract("second-pickup"));RoundSnapshot snapshot=s.ExportSnapshot();Session restored;string reason;Assert.IsTrue(Session.TryRestore(snapshot,out restored,out reason),reason);
            Assert.AreEqual(0,restored.LastEvents.Length);Assert.AreEqual(3,restored.State.PendingItemId);Assert.AreEqual(snapshot.Energy,restored.State.Energy);Assert.AreEqual(0,restored.State.Wallet);
            CommandResult duplicate=restored.Attract("second-pickup");Assert.IsTrue(duplicate.Success);Assert.IsTrue(duplicate.AlreadyApplied);Assert.IsEmpty(duplicate.Events);Assert.AreEqual(snapshot.Energy,restored.State.Energy);
            Ok(restored.Retain());Ok(restored.Detach(0));Ok(restored.Sell(3));Ok(restored.Sell(2));Assert.AreEqual(48,restored.State.Energy);Assert.AreEqual(15,restored.State.Wallet);AssertOwners(restored);
        }
        [Test] public void SnapshotAndPreviewNestedArraysAreDeepCopies()
        {
            Session s=RodTool();RoundSnapshot snapshot=s.State;snapshot.Items[1].Cells[0].X=99;snapshot.ChainIds[0]=5;snapshot.Order.Requirements[0].Delivered=1;
            ActionPreview p=s.PreviewRotate(-1);p.Cells[0].X=99;Assert.AreEqual(2,s.State.Items[1].Cells[0].X);CollectionAssert.AreEqual(new[]{2},s.State.ChainIds);Assert.AreEqual(0,s.State.Order.Delivered);AssertOwners(s);
        }
        [TestCase("energy")] [TestCase("wallet")] [TestCase("ownership")] [TestCase("end")] [TestCase("order")] [TestCase("phase")] [TestCase("version")] [TestCase("cells")] [TestCase("args")]
        public void ForgedOrPartialSnapshotsAreRejectedByProductionReplay(string mutation)
        {
            Session s=PartsChain();RoundSnapshot bad=s.ExportSnapshot();
            switch(mutation){case "energy":bad.Energy++;break;case "wallet":bad.Wallet=100;break;case "ownership":bad.StorageIds=new[]{2};break;case "end":bad.EndX--;break;case "order":bad.Order.Requirements[0].Delivered=1;break;case "phase":bad.Phase=Phase.Finished;bad.Outcome=RoundOutcome.Completed;break;case "version":bad.RulesVersion="99";break;case "cells":bad.Items[2].Cells[0].X++;break;case "args":bad.Commands.First(c=>c.Kind==CommandKind.Attract).A=4;break;}
            string reason;Assert.IsFalse(Session.ValidateSnapshot(bad,out reason));
        }
    }
}
