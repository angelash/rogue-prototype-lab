using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MagnetShift.Tests
{
    public sealed class ShiftLevelsTests
    {
        static ShiftGame Play(int level, string moves)
        {
            var game = new ShiftGame(ShiftLevels.All[level]);
            foreach (char move in moves)
            {
                ShiftAction action = (ShiftAction)"LRPU".IndexOf(move);
                ShiftResult result = game.Act(action);
                Assert.IsTrue(result.Legal, game.Level.Id + " " + game.History + " + " + move + " " + result.Message);
            }
            return game;
        }

        [TestCase(0, "PPU", 1, 5)]
        [TestCase(1, "PRPPU", 1, 5)]
        [TestCase(2, "PPUURRRRRPU", 2, 2)]
        [TestCase(3, "PRRPUU", 2, 5)]
        [TestCase(4, "PRRPPPUUUU", 2, 6)]
        public void HandRoutedOrdersAndShortestHintsReachTheSameDeadline(
            int index, string route, int delivered, int spare)
        {
            ShiftGame routed = Play(index, route);
            Assert.IsTrue(routed.Won);
            Assert.AreEqual(delivered, routed.Delivered);
            Assert.AreEqual(spare, routed.Remaining);
            Assert.AreEqual(route.Length, ShiftLevels.Par(index));

            var game = new ShiftGame(ShiftLevels.All[index]);
            for (int i = 0; i < route.Length; i++)
            {
                string history = game.History;
                Assert.AreEqual(ShiftRouteStatus.Found, ShiftAdvice.Find(game, out ShiftAction next, 2000, 10000));
                Assert.AreEqual(history, game.History, "Asking for a hint must leave the player where they were.");
                Assert.IsTrue(game.Act(next).Legal);
            }
            Assert.IsTrue(game.Won, "The shortest route changed relative to the hand-routed budget.");
        }

        [Test]
        public void ShaftRodIsTheOnlyBridgeToTheCore()
        {
            ShiftGame game = Play(0, "PU");
            Assert.AreEqual(ShiftRouteStatus.Impossible, ShiftAdvice.Find(game, out _, 2000, 10000));
            Assert.IsTrue(game.Undo());
            Assert.AreEqual('h', game.Preview(ShiftAction.Pulse).Piece.Kind);
            Assert.AreEqual(ShiftRouteStatus.Found, ShiftAdvice.Find(game, out _, 2000, 10000));
        }

        [Test]
        public void GantryRequiresSellingTheRodToCrossAfterTheDeepPick()
        {
            ShiftGame game = Play(2, "PPUR");
            Assert.AreEqual('r', game.Chain[0].Kind);
            Assert.IsFalse(game.Act(ShiftAction.Right).Legal);
            Assert.IsTrue(game.Act(ShiftAction.Unload).Legal);
            Assert.IsTrue(game.Act(ShiftAction.Right).Legal);

            // Collecting the right-hand order first leaves too few moves for the return journey.
            game = Play(2, "RRRRRPU");
            Assert.AreEqual(ShiftRouteStatus.Impossible, ShiftAdvice.Find(game, out _, 2000, 10000));
        }

        [Test]
        public void ScalesPayingTheShortPlateEarlyLeavesNoToolWithinTheLoadLimit()
        {
            ShiftGame soldPlate = Play(3, "PU");
            Assert.AreEqual(1, soldPlate.Delivered);
            Assert.AreEqual(ShiftRouteStatus.Impossible, ShiftAdvice.Find(soldPlate, out _, 2000, 10000));

            ShiftGame rodAtCore = Play(3, "PULLPRRRR");
            ShiftResult overweight = rodAtCore.Preview(ShiftAction.Pulse);
            Assert.AreEqual('h', overweight.Piece.Kind);
            Assert.Greater(rodAtCore.Load + overweight.Piece.Weight, rodAtCore.Level.Capacity);
            Assert.IsFalse(overweight.Legal);
        }

        [Test]
        public void BlindPocketTakesTheCoreFromBelowTheLid()
        {
            ShiftGame game = Play(4, "PRRPP");
            Assert.AreEqual(new ShiftCell(0, -1), game.Heading);
            Assert.AreEqual(new ShiftCell(4, 2), game.End);
            ShiftPiece target = game.Preview(ShiftAction.Pulse).Piece;
            Assert.AreEqual(new ShiftCell(4, 1), target.Origin);
            Assert.IsTrue(game.Level.IsWall(new ShiftCell(4, 0)));
        }

        [Test]
        public void AdviceSearchLimitCannotBeReportedAsAnUnsolvableOrder()
        {
            var game = new ShiftGame(ShiftLevels.All[0]);
            Assert.AreEqual(ShiftRouteStatus.Limit, ShiftAdvice.Find(game, out _, 0, 10000));
        }

        [TestCase(0, 66, 22)]
        [TestCase(1, 189, 49)]
        [TestCase(2, 217, 36)]
        [TestCase(3, 253, 55)]
        [TestCase(4, 341, 87)]
        public void AdviceMatchesAllReachableOrderStates(int level, int expectedStates, int expectedViable)
        {
            var solved = new Dictionary<string, bool>();
            var game = new ShiftGame(ShiftLevels.All[level]);
            Assert.IsTrue(CheckAllContinuations(game, solved));
            Assert.AreEqual(expectedStates, solved.Count);
            Assert.AreEqual(expectedViable, solved.Values.Count(value => value));
        }

        static string StateAtBudget(ShiftGame game)
        {
            return game.CarX + ":" + game.Remaining + ":" +
                string.Join(",", game.Bin.Select(p => p.Id)) + "/" +
                string.Join(",", game.Chain.Select(p => p.Id));
        }

        static bool CheckAllContinuations(ShiftGame game, Dictionary<string, bool> solved)
        {
            string key = StateAtBudget(game);
            if (solved.TryGetValue(key, out bool known)) return known;

            // Every forward edge spends a move. Keep checking sibling moves after finding a winning
            // continuation so each configuration at each remaining budget gets its own verdict.
            bool viable = game.Won;
            foreach (ShiftAction action in System.Enum.GetValues(typeof(ShiftAction)))
            {
                if (!game.Act(action).Legal) continue;
                if (CheckAllContinuations(game, solved)) viable = true;
                game.Undo();
            }
            solved.Add(key, viable);

            string trace = game.Level.Id + " " + game.History;
            ShiftRouteStatus expected = game.Won ? ShiftRouteStatus.Complete :
                viable ? ShiftRouteStatus.Found : ShiftRouteStatus.Impossible;
            // Preserve the production node cap, while keeping hardware timing out of the verdict.
            Assert.AreEqual(expected, ShiftAdvice.Find(game, out ShiftAction next, 2000, 30000), trace);
            if (expected == ShiftRouteStatus.Found)
            {
                Assert.IsTrue(game.Act(next).Legal, trace);
                Assert.IsTrue(solved[StateAtBudget(game)], trace);
                game.Undo();
            }
            return viable;
        }

        [Test]
        public void OrderIdsAreUniqueSaveKeys()
        {
            var seen = new HashSet<string>();
            foreach (ShiftLevel level in ShiftLevels.All) Assert.IsTrue(seen.Add(level.Id), level.Id);
        }
    }
}
