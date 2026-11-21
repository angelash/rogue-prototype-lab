using System;
using System.Collections.Generic;

namespace MagnetShift
{
    public enum ShiftAction { Left, Right, Pulse, Unload }

    public readonly struct ShiftCell : IEquatable<ShiftCell>
    {
        public readonly int X, Y;

        public ShiftCell(int x, int y) { X = x; Y = y; }
        public bool Equals(ShiftCell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is ShiftCell cell && Equals(cell);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public static ShiftCell operator +(ShiftCell a, ShiftCell b) => new ShiftCell(a.X + b.X, a.Y + b.Y);
        public static bool operator ==(ShiftCell a, ShiftCell b) => a.Equals(b);
        public static bool operator !=(ShiftCell a, ShiftCell b) => !a.Equals(b);
    }

    public sealed class ShiftPiece
    {
        public int Id { get; }
        public ShiftCell Origin { get; }
        public char Kind { get; }
        public bool Target { get; }
        public string Name { get; }
        public int Weight { get; }
        public int Length => Kind == 'r' ? 2 : 1;

        internal ShiftPiece(int id, ShiftCell origin, char symbol)
        {
            Id = id;
            Origin = origin;
            Kind = char.ToLowerInvariant(symbol);
            Target = char.IsUpper(symbol);
            switch (Kind)
            {
                case 'r': Name = "长杆"; Weight = 2; break;
                case 'e': Name = "弯头"; Weight = 1; break;
                case 'h': Name = "重芯"; Weight = 3; break;
                case 'p': Name = "短片"; Weight = 1; break;
                default: throw new ArgumentException("未知料箱符号 " + symbol);
            }
        }
    }

    public sealed class ShiftLevel
    {
        readonly string[] rows;
        public string Id { get; }
        public string Title { get; }
        public string Brief { get; }
        public int StartX { get; }
        public int Range { get; }
        public int Capacity { get; }
        public int Budget { get; }
        public int Width => rows[0].Length;
        public int Height => rows.Length;
        public int Targets { get; }
        public IReadOnlyList<ShiftPiece> Pieces { get; }

        public ShiftLevel(string id, string title, string brief, int startX, int range,
                          int capacity, int budget, params string[] rows)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || brief == null)
                throw new ArgumentException("工单编号、名称和说明缺失");
            if (rows == null || rows.Length == 0 || string.IsNullOrEmpty(rows[0]))
                throw new ArgumentException("料箱为空", nameof(rows));
            this.rows = (string[])rows.Clone();
            if (startX < 0 || startX >= Width || range <= 0 || capacity <= 0 || budget <= 0)
                throw new ArgumentException("初始轨位、射程、载重或步数不合法");

            Id = id;
            Title = title;
            Brief = brief;
            StartX = startX;
            Range = range;
            Capacity = capacity;
            Budget = budget;
            var pieces = new List<ShiftPiece>();
            for (int y = 0; y < Height; y++)
            {
                if (rows[y] == null || rows[y].Length != Width)
                    throw new ArgumentException("料箱各行宽度必须相等", nameof(rows));
                for (int x = 0; x < Width; x++)
                {
                    char symbol = rows[y][x];
                    if (symbol == '.' || symbol == '#') continue;
                    var piece = new ShiftPiece(pieces.Count + 1, new ShiftCell(x, y), symbol);
                    pieces.Add(piece);
                    if (piece.Target) Targets++;
                }
            }
            if (Targets == 0) throw new ArgumentException("工单须包含至少一件订单货", nameof(rows));
            Pieces = pieces.AsReadOnly();
        }

        public bool InBounds(ShiftCell cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
        public bool IsWall(ShiftCell cell) => InBounds(cell) && rows[cell.Y][cell.X] == '#';
    }

    public sealed class ShiftResult
    {
        public bool Legal { get; }
        public string Message { get; }
        public ShiftPiece Piece { get; }
        public ShiftCell ExitHeading { get; }
        public IReadOnlyList<ShiftCell> Ray { get; }
        public IReadOnlyList<ShiftCell> CandidateCells { get; }

        internal ShiftResult(bool legal, string message, ShiftPiece piece = null,
                             List<ShiftCell> ray = null, List<ShiftCell> cells = null,
                             ShiftCell exitHeading = default)
        {
            Legal = legal;
            Message = message;
            Piece = piece;
            ExitHeading = exitHeading;
            Ray = ray == null ? (IReadOnlyList<ShiftCell>)Array.Empty<ShiftCell>() : ray.AsReadOnly();
            CandidateCells = cells == null ? (IReadOnlyList<ShiftCell>)Array.Empty<ShiftCell>() : cells.AsReadOnly();
        }
    }

    public sealed class ShiftGame
    {
        const string Codes = "LRPU";
        readonly List<ShiftPiece> bin = new List<ShiftPiece>();
        readonly List<ShiftPiece> chain = new List<ShiftPiece>();
        readonly List<ShiftPiece> sold = new List<ShiftPiece>();
        readonly List<ShiftCell> chainCells = new List<ShiftCell>();
        readonly ShiftResult[] previews = new ShiftResult[Codes.Length];

        public ShiftLevel Level { get; }
        public int CarX { get; private set; }
        public string History { get; private set; }
        public int Remaining => Level.Budget - History.Length;
        public int Load { get; private set; }
        public int Delivered { get; private set; }
        public bool Won => Delivered == Level.Targets;
        public bool Lost => !Won && Remaining == 0;
        public ShiftCell End { get; private set; }
        public ShiftCell Heading { get; private set; }
        public IReadOnlyList<ShiftPiece> Chain { get; }
        public IReadOnlyList<ShiftPiece> Bin { get; }
        public IReadOnlyList<ShiftPiece> Sold { get; }
        public IReadOnlyList<ShiftCell> ChainCells { get; }

        public ShiftGame(ShiftLevel level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Chain = chain.AsReadOnly();
            Bin = bin.AsReadOnly();
            Sold = sold.AsReadOnly();
            ChainCells = chainCells.AsReadOnly();
            Restart();
        }

        public void Restart()
        {
            History = "";
            CarX = Level.StartX;
            Delivered = 0;
            chain.Clear();
            sold.Clear();
            bin.Clear();
            bin.AddRange(Level.Pieces);
            RebuildChain();
        }

        // A folded rod occupies one bin cell. On the magnet its two links unfold along the incoming ray.
        static void Extend(ShiftPiece piece, List<ShiftCell> cells, ref ShiftCell end, ref ShiftCell heading)
        {
            for (int i = 0; i < piece.Length; i++)
            {
                end += heading;
                cells.Add(end);
            }
            if (piece.Kind == 'e') heading = new ShiftCell(heading.Y, -heading.X);
        }

        void RebuildChain()
        {
            Array.Clear(previews, 0, previews.Length);
            chainCells.Clear();
            Load = 0;
            var end = new ShiftCell(CarX, -1);
            var heading = new ShiftCell(0, 1);
            foreach (ShiftPiece piece in chain)
            {
                Extend(piece, chainCells, ref end, ref heading);
                Load += piece.Weight;
            }
            End = end;
            Heading = heading;
        }

        ShiftPiece LooseAt(ShiftCell cell)
        {
            foreach (ShiftPiece piece in bin)
                if (piece.Origin == cell) return piece;
            return null;
        }

        string FitError(ShiftCell cell, ShiftPiece beingLifted)
        {
            if (!Level.InBounds(cell)) return "吊件会碰到箱沿";
            if (Level.IsWall(cell)) return "吊件会撞上隔板";
            ShiftPiece loose = LooseAt(cell);
            if (loose != null && loose != beingLifted) return "吊件会撞上" + loose.Name;
            return null;
        }

        ShiftResult CheckMove(int dx)
        {
            int x = CarX + dx;
            if (x < 0 || x >= Level.Width) return new ShiftResult(false, "吊车已到轨道尽头");
            foreach (ShiftCell cell in chainCells)
            {
                string error = FitError(new ShiftCell(cell.X + dx, cell.Y), null);
                if (error != null) return new ShiftResult(false, error);
            }
            return new ShiftResult(true, dx < 0 ? "向左一格" : "向右一格");
        }

        ShiftResult CheckPulse()
        {
            var ray = new List<ShiftCell>();
            ShiftCell position = End;
            for (int i = 0; i < Level.Range; i++)
            {
                position += Heading;
                if (!Level.InBounds(position)) return new ShiftResult(false, "前方是箱沿", ray: ray);
                ray.Add(position);
                if (Level.IsWall(position)) return new ShiftResult(false, "隔板挡住了磁线", ray: ray);
                if (chainCells.Contains(position)) return new ShiftResult(false, "吊件挡住了磁线", ray: ray);
                ShiftPiece piece = LooseAt(position);
                if (piece == null) continue;
                if (Load + piece.Weight > Level.Capacity)
                    return new ShiftResult(false, "吸起" + piece.Name + "将超重 " + (Load + piece.Weight - Level.Capacity), piece, ray);
                var cells = new List<ShiftCell>(piece.Length);
                ShiftCell end = End, heading = Heading;
                Extend(piece, cells, ref end, ref heading);
                foreach (ShiftCell cell in cells)
                {
                    string error = FitError(cell, piece);
                    if (error == null && chainCells.Contains(cell)) error = "接上后会撞到已吊起的零件";
                    if (error != null) return new ShiftResult(false, error, piece, ray, cells);
                }
                return new ShiftResult(true, "吸取" + piece.Name, piece, ray, cells, heading);
            }
            return new ShiftResult(false, "射程内没有零件", ray: ray);
        }

        ShiftResult CheckUnload()
        {
            if (chain.Count == 0) return new ShiftResult(false, "磁头上没有零件");
            ShiftPiece tail = chain[chain.Count - 1];
            return new ShiftResult(true, (tail.Target ? "交付" : "回收") + tail.Name, tail);
        }

        public ShiftResult Preview(ShiftAction action)
        {
            int index = (int)action;
            if (index < 0 || index >= previews.Length) return new ShiftResult(false, "未知操作");
            if (previews[index] != null) return previews[index];
            ShiftResult result;
            if (Won || Lost) result = new ShiftResult(false, Won ? "工单已完成" : "步数已用完");
            else
            {
                switch (action)
                {
                    case ShiftAction.Left: result = CheckMove(-1); break;
                    case ShiftAction.Right: result = CheckMove(1); break;
                    case ShiftAction.Pulse: result = CheckPulse(); break;
                    default: result = CheckUnload(); break;
                }
            }
            previews[index] = result;
            return result;
        }

        public ShiftResult Act(ShiftAction action)
        {
            ShiftResult result = Preview(action);
            if (!result.Legal) return result;
            switch (action)
            {
                case ShiftAction.Left: CarX--; break;
                case ShiftAction.Right: CarX++; break;
                case ShiftAction.Pulse:
                    bin.Remove(result.Piece);
                    chain.Add(result.Piece);
                    break;
                case ShiftAction.Unload:
                    chain.RemoveAt(chain.Count - 1);
                    sold.Add(result.Piece);
                    if (result.Piece.Target) Delivered++;
                    break;
            }
            History += Codes[(int)action];
            RebuildChain();
            return result;
        }

        public bool Undo()
        {
            if (History.Length == 0) return false;
            switch (History[History.Length - 1])
            {
                case 'L': CarX++; break;
                case 'R': CarX--; break;
                case 'P':
                    ShiftPiece lifted = chain[chain.Count - 1];
                    chain.RemoveAt(chain.Count - 1);
                    int insertion = bin.FindIndex(piece => piece.Id > lifted.Id);
                    bin.Insert(insertion < 0 ? bin.Count : insertion, lifted);
                    break;
                case 'U':
                    ShiftPiece unloaded = sold[sold.Count - 1];
                    sold.RemoveAt(sold.Count - 1);
                    chain.Add(unloaded);
                    if (unloaded.Target) Delivered--;
                    break;
            }
            History = History.Substring(0, History.Length - 1);
            RebuildChain();
            return true;
        }

        public static bool TryRestore(ShiftLevel level, string history, out ShiftGame restored)
        {
            restored = null;
            if (level == null || history == null || history.Length > level.Budget) return false;
            var game = new ShiftGame(level);
            foreach (char code in history)
            {
                int index = Codes.IndexOf(code);
                if (index < 0 || !game.Act((ShiftAction)index).Legal) return false;
            }
            restored = game;
            return true;
        }
    }
}
