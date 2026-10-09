using System.Linq;
using NUnit.Framework;

namespace RecyclingCleaners.Tests
{
    public sealed class SessionTests
    {
        private static void Accept(CommandResult result) { Assert.That(result.Success,Is.True,result.Reason); }
        private static void Move(Session s,int x,int y) { Accept(s.Move(x,y)); }
        // The 29-action trace and its expectations are derived independently in Core/README.md.
        private static Session GoldenPrefix(int capacity=Rules.SmallBucketCapacity)
        {
            Session s=Session.CreatePrototype(LayoutKind.Compact,capacity,false);
            Move(s,1,0);Accept(s.Vacuum(1,0));Accept(s.Vacuum(1,0));
            Move(s,0,1);Accept(s.Scrub(1,1));Move(s,1,0);Accept(s.Vacuum(2,1));
            Move(s,-1,0);Move(s,-1,0);Move(s,0,-1);Accept(s.Sell());
            Move(s,1,0);Move(s,1,0);Accept(s.Vacuum(2,0));Accept(s.Vacuum(2,0));
            Move(s,0,1);Accept(s.Vacuum(2,1));Move(s,-1,0);Move(s,-1,0);Move(s,0,-1);Accept(s.Sell());
            Move(s,1,0);Move(s,1,0);Move(s,1,0);Accept(s.Scrub(3,1));Accept(s.Scrub(3,1));Accept(s.Scrub(3,1));Move(s,0,1);
            return s;
        }
        [TestCase(6)]
        [TestCase(12)]
        public void CLN_A02_NoFilterBasicBrushHasCompleteIndependentGoldenTrace(int capacity)
        {
            Session s=GoldenPrefix(capacity);Accept(s.Scrub(3,2));
            Assert.That(s.State.Phase,Is.EqualTo(Phase.Finished));
            Assert.That(s.State.Outcome,Is.EqualTo(RoundOutcome.Completed));
            Assert.That(s.State.RemainingDirt,Is.Zero);
            Assert.That(s.State.CleanedUnits,Is.EqualTo(19));
            Assert.That(s.State.ActionsRemaining,Is.EqualTo(51));
            Assert.That(s.State.Durability,Is.EqualTo(35));
            Assert.That(s.State.Wallet,Is.EqualTo(22));
            Assert.That(s.State.SoldOrdinary,Is.EqualTo(8));
            Assert.That(s.State.SoldConvertible,Is.EqualTo(4));
            Assert.That(s.State.BucketUsed,Is.Zero);
            Assert.That(s.State.Detergent,Is.Zero);
            Assert.That(s.State.FilterChargesRemaining,Is.EqualTo(3));
        }
        [TestCase(6)]
        [TestCase(12)]
        public void SeparatedLayoutAlsoHasReachableNoFilterBasicRoute(int capacity)
        {
            Session s=Session.CreatePrototype(LayoutKind.Separated,capacity,false);
            for(int i=0;i<3;i++)Move(s,1,0);for(int i=0;i<5;i++)Move(s,0,1);
            Accept(s.Vacuum(4,5));Accept(s.Vacuum(4,5));Accept(s.Vacuum(3,5));
            Move(s,1,0);Accept(s.Scrub(4,4));Move(s,-1,0);
            for(int i=0;i<5;i++)Move(s,0,-1);for(int i=0;i<3;i++)Move(s,-1,0);Accept(s.Sell());
            for(int i=0;i<3;i++)Move(s,1,0);for(int i=0;i<4;i++)Move(s,0,1);
            Accept(s.Vacuum(3,4));Accept(s.Vacuum(3,4));Accept(s.Vacuum(3,5));
            Accept(s.Scrub(2,4));Accept(s.Scrub(2,4));Accept(s.Scrub(2,4));Move(s,-1,0);Accept(s.Scrub(2,3));
            Assert.That(s.State.Outcome,Is.EqualTo(RoundOutcome.Completed));
            Assert.That(s.State.ActionsRemaining,Is.EqualTo(42)); // 26 moves + 11 clean + 1 sale.
            Assert.That(s.State.Durability,Is.EqualTo(36));
            Assert.That(s.State.Wallet,Is.EqualTo(14));
            Assert.That(s.State.Ordinary,Is.EqualTo(4));Assert.That(s.State.Convertible,Is.EqualTo(2));
            Assert.That(s.State.SoldOrdinary,Is.EqualTo(4));Assert.That(s.State.SoldConvertible,Is.EqualTo(2));
            Assert.That(s.State.Detergent,Is.Zero);Assert.That(s.State.FilterChargesRemaining,Is.EqualTo(3));
        }
        [Test]
        public void CLN_A01_SmallBucketStopsAtActualCapacityWithoutDeletingResiduals()
        {
            Session small=Session.CreatePrototype(LayoutKind.Compact,6,false);
            Session large=Session.CreatePrototype(LayoutKind.Compact,12,false);
            foreach(Session s in new[]{small,large})
            {Move(s,1,0);Accept(s.Vacuum(1,0));Accept(s.Vacuum(1,0));Accept(s.Vacuum(2,0));}
            RoundSnapshot before=small.State;
            CommandResult blocked=small.Vacuum(2,0);
            Assert.That(blocked.Success,Is.False);Assert.That(blocked.Reason,Does.Contain("桶满"));
            Assert.That(small.State.Cells[Rules.CellId(2,0)].Remaining,Is.EqualTo(2));
            Assert.That(small.State.ActionsRemaining,Is.EqualTo(before.ActionsRemaining));
            Assert.That(small.State.Durability,Is.EqualTo(before.Durability));
            Accept(large.Vacuum(2,0));
            Assert.That(small.State.BucketUsed,Is.EqualTo(6));Assert.That(large.State.BucketUsed,Is.EqualTo(8));
            Assert.That(small.State.CleanedUnits,Is.EqualTo(6));Assert.That(large.State.CleanedUnits,Is.EqualTo(8));
        }
        [Test]
        public void CLN_A01_OneRemainingSlotTakesOneUnitAndLeavesTheRestOnTheFloor()
        {
            Session s=Session.CreatePrototype();
            Accept(s.Vacuum(1,0)); // Ordinary2.
            Accept(s.SetBrush(BrushKind.Wide));
            Accept(s.Vacuum(0,0)); // Only neighbouring ordinary1: bucket3.
            Accept(s.Vacuum(1,0)); // Last ordinary1 plus convertible1: bucket5.
            Accept(s.SetBrush(BrushKind.Narrow));Move(s,1,0);
            Accept(s.Vacuum(2,0)); // Power2 but free space1.
            Assert.That(s.State.BucketUsed,Is.EqualTo(6));
            Assert.That(s.State.Ordinary,Is.EqualTo(4));Assert.That(s.State.Convertible,Is.EqualTo(2));
            Assert.That(s.State.Cells[Rules.CellId(2,0)].Remaining,Is.EqualTo(2));
            Assert.That(s.LastEvents.Any(e=>e.Kind==EventKind.BucketBlocked),Is.True);
        }
        [Test]
        public void CLN_A02_AutomaticFilterUsesOldMaterialOnceAndFiniteDetergentWorksOnStubbornDirt()
        {
            Session s=Session.CreatePrototype(LayoutKind.Compact,6,true);Move(s,1,0);
            Accept(s.Vacuum(2,0));
            Assert.That(s.State.Conversions,Is.Zero); // Newly collected material cannot loop-convert this action.
            Accept(s.Vacuum(2,0));
            Assert.That(s.State.Conversions,Is.EqualTo(1));Assert.That(s.State.Convertible,Is.EqualTo(2));
            Assert.That(s.State.Detergent,Is.EqualTo(1));Assert.That(s.State.FilterChargesRemaining,Is.EqualTo(2));
            Assert.That(s.State.Durability,Is.EqualTo(45)); // Vacuum1 + vacuum1 + automatic conversion1.
            Assert.That(s.LastEvents.Select(e=>e.Kind).First(),Is.EqualTo(EventKind.Converted));
            Accept(s.Convert());
            Assert.That(s.State.Detergent,Is.EqualTo(2));Assert.That(s.Convert().Success,Is.False);
            Move(s,1,0);Move(s,1,0);Accept(s.UseDetergent(3,1));
            Assert.That(s.State.Cells[Rules.CellId(3,1)].Remaining,Is.Zero);
            Assert.That(s.State.Detergent,Is.EqualTo(1));Assert.That(s.State.DetergentUsed,Is.EqualTo(1));
            Assert.That(s.State.Conversions,Is.EqualTo(2));Assert.That(s.State.Convertible,Is.Zero);
        }
        [Test]
        public void CLN_A03_IdenticalConvertibleMaterialCanBeSoldOrConvertedWithoutDoubleReward()
        {
            Session sale=Session.CreatePrototype(LayoutKind.Compact,6,false);Move(sale,1,0);
            Accept(sale.Vacuum(2,0));Accept(sale.Vacuum(2,0));Move(sale,-1,0);Accept(sale.Sell());
            Assert.That(sale.State.Wallet,Is.EqualTo(14));Assert.That(sale.State.SoldConvertible,Is.EqualTo(4));
            Accept(sale.SetFilter(true));Assert.That(sale.Convert().Success,Is.False);
            Session convert=Session.CreatePrototype(LayoutKind.Compact,6,true);Move(convert,1,0);
            Accept(convert.Vacuum(2,0));Accept(convert.Vacuum(2,0));Accept(convert.Convert());Move(convert,-1,0);
            Assert.That(convert.Sell().Success,Is.False);
            Assert.That(convert.State.Wallet,Is.EqualTo(6));Assert.That(convert.State.Conversions,Is.EqualTo(2));
            Assert.That(convert.State.Detergent,Is.EqualTo(2));Assert.That(convert.State.SoldConvertible,Is.Zero);
        }
        [Test]
        public void CLN_A04_BrushesHaveDifferentCoverageMaterialAndExplicitActionAndDurabilityCosts()
        {
            Session narrow=Session.CreatePrototype();Accept(narrow.Vacuum(1,0));
            Session wide=Session.CreatePrototype();Accept(wide.SetBrush(BrushKind.Wide));
            RoundSnapshot before=wide.State;Accept(wide.Vacuum(1,0));
            Assert.That(narrow.State.Ordinary,Is.EqualTo(2));Assert.That(narrow.State.Convertible,Is.Zero);
            Assert.That(wide.State.Ordinary,Is.EqualTo(1));Assert.That(wide.State.Convertible,Is.EqualTo(1));
            Assert.That(before.ActionsRemaining-wide.State.ActionsRemaining,Is.EqualTo(2));
            Assert.That(before.Durability-wide.State.Durability,Is.EqualTo(2));
            Assert.That(wide.State.ActionsRemaining,Is.EqualTo(77));Assert.That(wide.State.Durability,Is.EqualTo(45));
            Assert.That(wide.LastEvents.Where(e=>e.Kind==EventKind.Vacuumed).Select(e=>Rules.CellId(e.X,e.Y)),Is.EqualTo(new[]{1,2}));
            Accept(wide.Discard(MaterialKind.Ordinary,1));
            Assert.That(wide.State.DiscardedOrdinary,Is.EqualTo(1));Assert.That(wide.State.Wallet,Is.EqualTo(6));
            Assert.That(wide.State.ActionsRemaining,Is.EqualTo(76));Assert.That(wide.State.Durability,Is.EqualTo(44));
        }
        [Test]
        public void CLN_A05_LastActionClearsBeforeBudgetExpiryAndRestoresWithoutExtraWork()
        {
            Session s=GoldenPrefix();
            Assert.That(s.State.ActionsRemaining,Is.EqualTo(52));
            for(int pair=0;pair<25;pair++){Move(s,1,0);Move(s,-1,0);}
            Accept(s.SetFilter(true));Assert.That(s.State.ActionsRemaining,Is.EqualTo(1));
            RoundSnapshot checkpoint=s.ExportSnapshot();Session restored;string reason;
            Assert.That(Session.TryRestore(checkpoint,out restored,out reason),Is.True,reason);
            Accept(s.Scrub(3,2));Accept(restored.Scrub(3,2));
            Assert.That(s.State.ActionsRemaining,Is.Zero);Assert.That(s.State.Outcome,Is.EqualTo(RoundOutcome.Completed));
            Assert.That(restored.State.Outcome,Is.EqualTo(RoundOutcome.Completed));
            Assert.That(restored.State.Wallet,Is.EqualTo(22));Assert.That(restored.State.Durability,Is.EqualTo(35));
            Assert.That(s.LastEvents.Last().Kind,Is.EqualTo(EventKind.RoundFinished));
            Assert.That(s.Scrub(3,2).Success,Is.False);
            Assert.That(Session.TryRestore(s.ExportSnapshot(),out restored,out reason),Is.True,reason);
            Assert.That(restored.LastEvents,Is.Empty);Assert.That(restored.Move(1,0).Success,Is.False);
        }
        [Test]
        public void AutomaticFilterWithoutExtraDurabilityFallsBackToOrdinaryVacuumAndCanRestock()
        {
            Session s=Session.CreatePrototype(LayoutKind.Compact,6,true);
            for(int i=0;i<46;i++)Accept(s.SetBrush(i%2==0?BrushKind.Wide:BrushKind.Narrow));
            Move(s,1,0);Accept(s.Vacuum(2,0));
            Assert.That(s.State.Durability,Is.EqualTo(1));Assert.That(s.State.Convertible,Is.EqualTo(2));
            Accept(s.Vacuum(1,0));
            Assert.That(s.State.Ordinary,Is.EqualTo(2));Assert.That(s.State.Convertible,Is.EqualTo(2));
            Assert.That(s.State.Detergent,Is.Zero);Assert.That(s.State.Conversions,Is.Zero);
            Assert.That(s.State.Durability,Is.Zero);Assert.That(s.State.Phase,Is.EqualTo(Phase.Active));
            Assert.That(s.LastEvents.Any(e=>e.Kind==EventKind.Converted),Is.False);
            Move(s,-1,0);int before=s.State.ActionsRemaining;Accept(s.Restock());
            Assert.That(s.State.ActionsRemaining,Is.EqualTo(before-2));Assert.That(s.State.Wallet,Is.EqualTo(3));
            Assert.That(s.State.Durability,Is.EqualTo(48));Assert.That(s.State.FilterChargesRemaining,Is.EqualTo(3));
        }
        [Test]
        public void InvalidAndDuplicateCommandsNeverCleanOrSpendAndSnapshotIsIndependent()
        {
            Session s=Session.CreatePrototype();
            Assert.That(s.Move(2,0).Success,Is.False);Assert.That(s.Vacuum(5,5).Success,Is.False);
            Assert.That(s.Scrub(0,0).Success,Is.False);Assert.That(s.Discard(MaterialKind.Ordinary,1).Success,Is.False);
            Assert.That(s.State.ActionsRemaining,Is.EqualTo(80));Assert.That(s.State.Durability,Is.EqualTo(48));
            Accept(s.Vacuum(1,0,"one-vacuum"));
            CommandResult duplicate=s.Vacuum(1,0,"one-vacuum");Assert.That(duplicate.AlreadyApplied,Is.True);Assert.That(duplicate.Events,Is.Empty);
            Assert.That(s.State.Ordinary,Is.EqualTo(2));Assert.That(s.State.ActionsRemaining,Is.EqualTo(79));
            RoundSnapshot copy=s.State;copy.Wallet+=100;copy.Cells[1].Remaining=0;
            Assert.That(s.State.Wallet,Is.EqualTo(6));Assert.That(s.State.Cells[1].Remaining,Is.EqualTo(2));
            Session restored;string reason;Assert.That(Session.TryRestore(s.ExportSnapshot(),out restored,out reason),Is.True,reason);
            Assert.That(restored.LastEvents,Is.Empty);Assert.That(restored.Vacuum(1,0,"one-vacuum").AlreadyApplied,Is.True);
        }
        [Test]
        public void TwoLayoutsHaveSameFiniteContentsAndFailedBudgetRetryKeepsItsConfiguration()
        {
            Session compact=Session.CreatePrototype(LayoutKind.Compact,6,false);
            Session far=Session.CreatePrototype(LayoutKind.Separated,12,true);
            Assert.That(compact.State.RemainingDirt,Is.EqualTo(19));Assert.That(far.State.RemainingDirt,Is.EqualTo(19));
            Assert.That(far.State.Cells[Rules.CellId(4,5)].Remaining,Is.EqualTo(4));
            Assert.That(far.Vacuum(1,0).Success,Is.False); // Same player start; dirt moved to far side.
            for(int i=0;i<40;i++){Move(far,1,0);Move(far,-1,0);}
            Assert.That(far.State.Phase,Is.EqualTo(Phase.Finished));Assert.That(far.State.Outcome,Is.EqualTo(RoundOutcome.Failed));
            Session retry=far.Retry();Assert.That(retry.State.Layout,Is.EqualTo(LayoutKind.Separated));
            Assert.That(retry.State.BucketCapacity,Is.EqualTo(12));Assert.That(retry.State.FilterEnabled,Is.True);
            Assert.That(retry.State.ActionsRemaining,Is.EqualTo(80));Assert.That(retry.State.Events,Is.Empty);
        }
        [TestCase("money")]
        [TestCase("free-clean")]
        [TestCase("material")]
        [TestCase("capacity")]
        [TestCase("detergent")]
        [TestCase("budget")]
        [TestCase("obstacle")]
        [TestCase("event")]
        [TestCase("version")]
        public void RestoreRejectsCorruptionBeyondShapeOrBalancedLedgers(string corruption)
        {
            Session s=Session.CreatePrototype();Accept(s.Vacuum(1,0));RoundSnapshot bad=s.ExportSnapshot();
            switch(corruption)
            {
                case "money":bad.Wallet++;break;
                case "free-clean":bad.Cells[Rules.CellId(1,1)].Remaining=0;break;
                case "material":bad.Ordinary++;bad.Cells[1].Remaining--;break;
                case "capacity":bad.BucketCapacity=7;break;
                case "detergent":bad.Detergent++;break;
                case "budget":bad.ActionsRemaining++;break;
                case "obstacle":bad.Cells[Rules.CellId(4,2)].Blocked=false;break;
                case "event":bad.Events[0].Amount++;break;
                case "version":bad.RulesVersion="future";break;
            }
            Session restored;string reason;Assert.That(Session.TryRestore(bad,out restored,out reason),Is.False);
            Assert.That(restored,Is.Null);Assert.That(reason,Is.Not.Empty);
        }
    }
}
