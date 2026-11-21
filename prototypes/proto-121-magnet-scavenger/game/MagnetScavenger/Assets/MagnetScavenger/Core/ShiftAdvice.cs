using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MagnetShift
{
    public enum ShiftRouteStatus { Found, Impossible, Limit, Complete }

    public static class ShiftAdvice
    {
        static readonly ShiftAction[] actions =
            { ShiftAction.Pulse, ShiftAction.Unload, ShiftAction.Left, ShiftAction.Right };

        readonly struct Visit
        {
            public readonly string History;
            public readonly ShiftAction First;

            public Visit(string history, ShiftAction first)
            {
                History = history;
                First = first;
            }
        }

        public static string Next(ShiftGame game)
        {
            ShiftRouteStatus status = Find(game, out ShiftAction action);
            if (status == ShiftRouteStatus.Complete) return "工单已交齐。";
            if (status == ShiftRouteStatus.Limit) return "暂时没算出路线。可以先检查磁线和吊件载重。";
            if (status == ShiftRouteStatus.Impossible) return "从这里已无法交齐。撤销几步，再试试另一条路线。";
            return "下一步可以" + game.Preview(action).Message + "。";
        }

        // A forward search never undoes a player's move. Undo here only backs out a simulated sibling.
        public static ShiftRouteStatus Find(ShiftGame game, out ShiftAction next,
                                            int maxNodes = 2000, int maxMilliseconds = 75)
        {
            next = default;
            if (game.Won) return ShiftRouteStatus.Complete;
            if (game.Lost) return ShiftRouteStatus.Impossible;

            var timer = Stopwatch.StartNew();
            var visited = new HashSet<string> { Key(game) };
            var pending = new Queue<Visit>();
            pending.Enqueue(new Visit(game.History, default));
            int expanded = 0;
            while (pending.Count > 0)
            {
                if (expanded >= maxNodes || timer.ElapsedMilliseconds >= maxMilliseconds)
                    return ShiftRouteStatus.Limit;
                expanded++;
                Visit node = pending.Dequeue();
                if (!ShiftGame.TryRestore(game.Level, node.History, out ShiftGame trial))
                    throw new System.InvalidOperationException("提示路线无法重放");
                foreach (ShiftAction action in actions)
                {
                    if (!trial.Act(action).Legal) continue;
                    ShiftAction first = node.History.Length == game.History.Length ? action : node.First;
                    if (trial.Won)
                    {
                        next = first;
                        return ShiftRouteStatus.Found;
                    }
                    if (!trial.Lost && visited.Add(Key(trial)))
                        pending.Enqueue(new Visit(trial.History, first));
                    trial.Undo();
                }
            }
            return ShiftRouteStatus.Impossible;
        }

        // Bin IDs and chain order determine the sold set. Breadth-first search first sees a
        // configuration with its greatest remaining budget, so a later revisit cannot help.
        static string Key(ShiftGame game)
        {
            var key = new StringBuilder().Append(game.CarX).Append(':');
            foreach (ShiftPiece piece in game.Bin) key.Append(piece.Id).Append(',');
            key.Append('/');
            foreach (ShiftPiece piece in game.Chain) key.Append(piece.Id).Append(',');
            return key.ToString();
        }
    }
}
