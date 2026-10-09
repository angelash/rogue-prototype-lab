using System;

namespace RecyclingCleaners
{
    public static class Rules
    {
        public const string Version = "0.2.0";
        public const int SchemaVersion = 1;
        public const int Size = 6;
        public const int SmallBucketCapacity = 6;
        public const int LargeBucketCapacity = 12;
        public const int ActionBudget = 80;
        public const int MaxDurability = 48;
        public const int InitialWallet = 6;
        public const int FilterCapacity = 3;
        public const int DetergentCapacity = 2;
        public const int RecipeInput = 2;
        public const int OrdinarySalePrice = 1;
        public const int ConvertibleSalePrice = 2;
        public const int RestockCost = 3;
        public const int RestockActions = 2;
        public const int NarrowActionCost = 1;
        public const int WideActionCost = 2;
        public const int NarrowDurabilityCost = 1;
        public const int WideDurabilityCost = 2;
        public const int AutomaticConversionDurability = 1;
        public const int DetergentCleaningPower = 3;
        public const int SupplyX = 0;
        public const int SupplyY = 0;
        public const int MaximumCommands = 4096;
        public static bool InBounds(int x, int y) { return x >= 0 && x < Size && y >= 0 && y < Size; }
        public static int CellId(int x, int y) { return y * Size + x; }
        public static string BrushName(BrushKind brush) { return brush == BrushKind.Narrow ? "窄刷头" : "广域刷头"; }
        public static string MaterialName(MaterialKind material) { return material == MaterialKind.Ordinary ? "普通回收料" : "可转化料"; }
        public static string DirtName(DirtKind dirt)
        {
            switch (dirt) { case DirtKind.Debris: return "碎屑"; case DirtKind.Water: return "水渍"; case DirtKind.Stubborn: return "顽渍"; default: return "干净"; }
        }

        internal static CellState[] CreateCells(LayoutKind layout)
        {
            var cells = new CellState[Size * Size];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                cells[CellId(x,y)] = new CellState { Id = CellId(x,y), X = x, Y = y };
            cells[CellId(4,2)].Blocked = true;
            cells[CellId(4,3)].Blocked = true;
            SetDirt(cells,layout,1,0,DirtKind.Debris,MaterialKind.Ordinary,4);
            SetDirt(cells,layout,2,0,DirtKind.Debris,MaterialKind.Convertible,4);
            SetDirt(cells,layout,2,1,DirtKind.Debris,MaterialKind.Ordinary,4);
            SetDirt(cells,layout,1,1,DirtKind.Water,MaterialKind.Ordinary,2);
            SetDirt(cells,layout,3,1,DirtKind.Stubborn,MaterialKind.Ordinary,3);
            SetDirt(cells,layout,3,2,DirtKind.Water,MaterialKind.Ordinary,2);
            return cells;
        }
        private static void SetDirt(CellState[] cells, LayoutKind layout, int x, int y, DirtKind dirt, MaterialKind material, int amount)
        {
            if (layout == LayoutKind.Separated) { x = Size - 1 - x; y = Size - 1 - y; }
            CellState cell = cells[CellId(x,y)];
            cell.Dirt = dirt; cell.Material = material; cell.InitialAmount = amount; cell.Remaining = amount;
        }
    }

    public enum Phase { Active, Finished }
    public enum RoundOutcome { None, Completed, Failed }
    public enum LayoutKind { Compact, Separated }
    public enum DirtKind { None, Debris, Water, Stubborn }
    public enum MaterialKind { Ordinary, Convertible }
    public enum BrushKind { Narrow, Wide }
    public enum EventKind { Moved, BrushChanged, Vacuumed, WaterScrubbed, StubbornScrubbed, Converted, DetergentApplied, BucketBlocked, MaterialSold, Discarded, FilterChanged, Restocked, RoundFinished }
    public enum CommandKind { Move, SetBrush, Vacuum, Scrub, UseDetergent, Convert, Sell, Discard, SetFilter, Restock }

    [Serializable]
    public sealed class CellState
    {
        public int Id, X, Y;
        public bool Blocked;
        public DirtKind Dirt;
        public MaterialKind Material;
        public int InitialAmount, Remaining;
        internal CellState Copy() { return (CellState)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class GameEvent
    {
        public int Sequence, ActionIndex;
        public EventKind Kind;
        public int X, Y, Amount;
        public string Reason;
        internal GameEvent Copy() { return (GameEvent)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class CommandRecord
    {
        public string Id;
        public CommandKind Kind;
        public int A, B;
        public bool Flag;
        internal CommandRecord Copy() { return (CommandRecord)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class RoundSnapshot
    {
        public int SchemaVersion;
        public string RulesVersion;
        public LayoutKind Layout;
        public int BucketCapacity;
        public bool FilterEnabled;
        public bool InitialFilterEnabled;
        public Phase Phase;
        public RoundOutcome Outcome;
        public int PlayerX, PlayerY, ActionsRemaining, Durability, Wallet;
        public BrushKind Brush;
        public int Ordinary, Convertible, Detergent, FilterChargesRemaining;
        public CellState[] Cells;
        public int Conversions, DetergentUsed, SoldOrdinary, SoldConvertible, DiscardedOrdinary, DiscardedConvertible;
        public int DurabilitySpent, DurabilityRecovered, Restocks;
        public CommandRecord[] Commands;
        public GameEvent[] Events;
        public int BucketUsed { get { return Ordinary + Convertible; } }
        public int RemainingDirt { get { int sum=0; if(Cells!=null)foreach(CellState c in Cells)if(c!=null)sum+=c.Remaining; return sum; } }
        public int CleanedUnits { get { int sum=0; if(Cells!=null)foreach(CellState c in Cells)if(c!=null)sum+=c.InitialAmount-c.Remaining; return sum; } }
    }
    public sealed class CommandResult
    {
        public bool Success, AlreadyApplied;
        public string Reason;
        public GameEvent[] Events;
    }
}
