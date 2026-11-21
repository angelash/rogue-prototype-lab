using System;
using System.Linq;
using NUnit.Framework;

namespace MagnetShift.Tests
{
    public class ShiftGameTests
    {
        static ShiftGame Game(int x, int range, int capacity, int budget, params string[] rows)
        {
            return new ShiftGame(new ShiftLevel("test", "测试工单", "", x, range, capacity, budget, rows));
        }

        [Test]
        public void FoldedRodExtendsTwoCellsAndReachesTheDeepOrder()
        {
            var game = Game(0, 2, 5, 3, "r", ".", ".", "H");
            ShiftResult first = game.Preview(ShiftAction.Pulse);
            Assert.AreEqual(new ShiftCell(0, 0), first.Piece.Origin);
            CollectionAssert.AreEqual(new[] { new ShiftCell(0, 0), new ShiftCell(0, 1) }, first.CandidateCells);
            Assert.AreEqual(0, game.Load);
            Assert.AreSame(first, game.Act(ShiftAction.Pulse));
            CollectionAssert.AreEqual(first.CandidateCells, game.ChainCells);

            ShiftResult second = game.Act(ShiftAction.Pulse);
            CollectionAssert.AreEqual(new[] { new ShiftCell(0, 2), new ShiftCell(0, 3) }, second.Ray);
            Assert.AreEqual(5, game.Load);
            Assert.AreEqual(0, game.Delivered);
            Assert.IsTrue(game.Act(ShiftAction.Unload).Legal);
            Assert.IsTrue(game.Won);
            Assert.IsFalse(game.Lost);
            Assert.AreEqual(0, game.Remaining);
            Assert.AreEqual("PPU", game.History);
            Assert.AreEqual('r', game.Chain.Single().Kind);
        }

        [Test]
        public void OverweightFirstPieceBlocksLaterTargetAndCostsNoStep()
        {
            var game = Game(0, 4, 3, 9, "p", "h", "P");
            game.Act(ShiftAction.Pulse);
            var hit = game.Act(ShiftAction.Pulse);
            Assert.IsFalse(hit.Legal);
            Assert.AreEqual('h', hit.Piece.Kind);
            Assert.AreEqual(new ShiftCell(0, 1), hit.Ray.Last());
            StringAssert.Contains("超重", hit.Message);
            Assert.AreEqual("P", game.History);
            Assert.AreEqual(2, game.Bin.Count);
            game.Act(ShiftAction.Unload);
            Assert.AreEqual('h', game.Act(ShiftAction.Pulse).Piece.Kind);
        }

        [Test]
        public void UnfoldingCannotPushThroughSecondLoosePiece()
        {
            var game = Game(0, 3, 5, 8, "r", "P", ".");
            var result = game.Act(ShiftAction.Pulse);
            Assert.IsFalse(result.Legal);
            StringAssert.Contains("短片", result.Message);
            Assert.AreEqual('r', result.Piece.Kind);
            Assert.AreEqual(2, result.CandidateCells.Count);
            Assert.AreEqual(2, game.Bin.Count);
            Assert.AreEqual(0, game.Chain.Count);
            Assert.AreEqual(8, game.Remaining);
        }

        [Test]
        public void WallStopsRayBeforeTarget()
        {
            var game = Game(0, 4, 4, 9, ".", "#", "P");
            var result = game.Act(ShiftAction.Pulse);
            Assert.IsFalse(result.Legal);
            Assert.IsNull(result.Piece);
            CollectionAssert.AreEqual(new[] { new ShiftCell(0, 0), new ShiftCell(0, 1) }, result.Ray);
            StringAssert.Contains("隔板", result.Message);
            Assert.AreEqual(9, game.Remaining);
        }

        [Test]
        public void ElbowTurnsRayFromItsSocketAndUnloadingRestoresHeading()
        {
            var game = Game(0, 3, 5, 6, "e.P", "...");
            var preview = game.Preview(ShiftAction.Pulse);
            Assert.AreEqual(new ShiftCell(1, 0), preview.ExitHeading);
            Assert.AreEqual(new ShiftCell(0, 1), game.Heading);
            game.Act(ShiftAction.Pulse);
            Assert.AreEqual(new ShiftCell(0, 0), game.End);
            Assert.AreEqual(preview.ExitHeading, game.Heading);
            ShiftResult order = game.Act(ShiftAction.Pulse);
            Assert.AreEqual(new ShiftCell(2, 0), order.Piece.Origin);
            Assert.AreEqual(new ShiftCell(1, 0), game.End);
            Assert.IsTrue(game.Undo());
            Assert.IsTrue(game.Act(ShiftAction.Unload).Legal);
            Assert.AreEqual(new ShiftCell(0, 1), game.Heading);
            Assert.AreEqual(new ShiftCell(0, -1), game.End);
            Assert.AreEqual(0, game.Delivered);
        }

        [Test]
        public void AttachedChainAlsoShieldsTheRayAfterThreeTurns()
        {
            var game = Game(1, 2, 8, 10, ".r.", "..e", ".ee", "...", "..P");
            for (int i = 0; i < 4; i++) Assert.IsTrue(game.Act(ShiftAction.Pulse).Legal);
            ShiftResult blocked = game.Act(ShiftAction.Pulse);
            Assert.IsFalse(blocked.Legal);
            Assert.AreEqual(new ShiftCell(1, 1), blocked.Ray.Single());
            StringAssert.Contains("吊件挡住", blocked.Message);
        }

        [Test]
        public void SellingToolShortensChainAndLetsCarriagePassWall()
        {
            var game = Game(0, 2, 5, 12, "r..", ".#.", "..P");
            game.Act(ShiftAction.Pulse);
            Assert.IsFalse(game.Act(ShiftAction.Right).Legal);
            Assert.AreEqual(0, game.CarX);
            Assert.AreEqual(2, game.Load);
            game.Act(ShiftAction.Unload);
            Assert.IsTrue(game.Act(ShiftAction.Right).Legal);
            Assert.AreEqual(1, game.CarX);
            Assert.AreEqual(0, game.Load);
            Assert.AreEqual('r', game.Sold.Single().Kind);
        }

        [Test]
        public void OnlyMarkedCopyCountsAndUndoRestoresTheExactSale()
        {
            var game = Game(0, 2, 4, 5, "pP");
            game.Act(ShiftAction.Pulse);
            game.Act(ShiftAction.Unload);
            Assert.AreEqual(0, game.Delivered);
            game.Act(ShiftAction.Right);
            game.Act(ShiftAction.Pulse);
            game.Act(ShiftAction.Unload);
            Assert.IsTrue(game.Won);
            Assert.IsFalse(game.Act(ShiftAction.Left).Legal);
            Assert.IsTrue(game.Undo());
            Assert.IsFalse(game.Won);
            Assert.AreEqual(2, game.Chain.Single().Id);
            Assert.AreEqual(1, game.Sold.Single().Id);
            Assert.AreEqual(1, game.Remaining);
        }

        [Test]
        public void EmptyBudgetLosesBeforeDeliveryAndCanBeUndoneOrRestarted()
        {
            var game = Game(0, 1, 3, 1, "P");
            game.Act(ShiftAction.Pulse);
            Assert.IsTrue(game.Lost);
            Assert.IsFalse(game.Act(ShiftAction.Unload).Legal);
            Assert.IsTrue(game.Undo());
            Assert.IsFalse(game.Lost);
            Assert.AreEqual(0, game.Load);
            Assert.AreEqual("短片", game.Bin.Single().Name);
            Assert.IsFalse(game.Undo());
            game.Act(ShiftAction.Pulse);
            game.Restart();
            Assert.AreEqual("", game.History);
            Assert.AreEqual(1, game.Remaining);
            Assert.AreEqual(1, game.Bin.Count);
        }

        [Test]
        public void ReplayRejectsInvalidHistoryAndPreservesUndoOrderOnValidHistory()
        {
            var level = Game(0, 2, 5, 6, "r.", "..", "..", "H.").Level;
            Assert.IsFalse(ShiftGame.TryRestore(level, "PPUL", out var afterWin));
            Assert.IsNull(afterWin);
            Assert.IsFalse(ShiftGame.TryRestore(level, "PX", out _));
            Assert.IsFalse(ShiftGame.TryRestore(level, "U", out _));
            Assert.IsFalse(ShiftGame.TryRestore(level, "RRRRRRR", out _));
            Assert.IsTrue(ShiftGame.TryRestore(level, "PPU", out var game));
            Assert.IsTrue(game.Won);
            Assert.IsTrue(game.Undo());
            CollectionAssert.AreEqual(new[] { 'r', 'h' }, game.Chain.Select(piece => piece.Kind));
            Assert.IsTrue(game.Undo());
            Assert.IsTrue(game.Undo());
            CollectionAssert.AreEqual(new[] { 1, 2 }, game.Bin.Select(piece => piece.Id));
        }

        [Test]
        public void LevelCopiesSourceAndRejectsAmbiguousMaps()
        {
            string[] rows = { ".P", ".#" };
            var level = Game(0, 2, 5, 6, rows).Level;
            rows[1] = "..";
            Assert.IsTrue(level.IsWall(new ShiftCell(1, 1)));
            Assert.Throws<ArgumentException>(() => Game(0, 2, 5, 6, "r."));
            Assert.Throws<ArgumentException>(() => Game(0, 2, 5, 6, "P?", ".."));
            Assert.Throws<ArgumentException>(() => Game(0, 2, 5, 6, "P", ".."));
            Assert.Throws<ArgumentException>(() => Game(3, 2, 5, 6, "P."));
        }
    }
}
