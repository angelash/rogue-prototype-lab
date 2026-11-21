using System;

namespace MagnetShift
{
    [Serializable]
    public sealed class ShiftProgress
    {
        public int schema = 1;
        public int current;
        public string[] histories;
        public string[] records;
        public bool muted;

        public static ShiftProgress Fresh()
        {
            int count = ShiftLevels.All.Count;
            return new ShiftProgress
            {
                histories = Array.ConvertAll(new int[count], _ => ""),
                records = Array.ConvertAll(new int[count], _ => "")
            };
        }

        public bool Open(int index)
        {
            return index == 0 || records[index - 1].Length > 0;
        }

        public void Record(ShiftGame game)
        {
            histories[current] = game.History;
            if (game.Won && (records[current].Length == 0 || game.History.Length < records[current].Length))
                records[current] = game.History;
        }

        public static bool Valid(ShiftProgress save)
        {
            int count = ShiftLevels.All.Count;
            if (save == null || save.schema != 1 || save.histories == null || save.records == null ||
                save.histories.Length != count || save.records.Length != count ||
                save.current < 0 || save.current >= count)
                return false;
            for (int i = 0; i < count; i++)
            {
                var level = ShiftLevels.All[i];
                string moves = save.histories[i], best = save.records[i];
                if (moves == null || best == null || moves.Length > level.Budget || best.Length > level.Budget)
                    return false;
                if (!save.Open(i) && (best.Length != 0 || moves.Length != 0))
                    return false;
                if (!ShiftGame.TryRestore(level, moves, out var currentGame))
                    return false;
                if (currentGame.Won && (best.Length == 0 || best.Length > moves.Length))
                    return false;
                if (best.Length > 0 && (!ShiftGame.TryRestore(level, best, out var completed) || !completed.Won))
                    return false;
            }
            return save.Open(save.current);
        }
    }
}
