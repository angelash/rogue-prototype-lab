using System;

namespace SushiWorkshop
{
    // Initial, adjustable v0.2 proposals. These are not measured balance results.
    public static class Rules
    {
        public const string Version = "0.2.0";
        public const int SnapshotSchemaVersion = 1;
        public const int SlotCount = 6;
        public const int PlateCount = 6;
        public const int EntrySlot = 0;
        public const int CustomerSlot = 5;
        public const int StepsPerCircle = 6;
        public const int StepLimit = 18;
        public const int InitialWallet = 18;
        public const int AdjustmentsPerRound = 4;
        public const int WholeAmountUnits = 2;
        public const int SmallAmountUnits = 1;
        public const int IngredientCost = 3;
        public const int WashCost = 1;
        public const int MoveStationCost = 2;
        public const int BuyStationCost = 6;
        public const int SellStationPrice = 3;
        public const int AbandonOrderCost = 2;
        public const int BaseSalePerAmountUnit = 1;
        public const int BaseTradeTarget = 6;
        public const int OrderQuantity = 2;
        public const int WholeOrderPrice = 10;
        public const int SmallOrderPrice = 7;
        public const int WholeOrderWait = 18;
        public const int SmallOrderWait = 12;
        public const int MaximumRecordedCommands = 4096;

        public static int NextSlot(int slot) { return (slot + 1) % SlotCount; }

        public static string FoodName(FoodState food)
        {
            switch (food)
            {
                case FoodState.RiceBase: return "米基底";
                case FoodState.FilledRice: return "卷料";
                case FoodState.Roll: return "成卷";
                case FoodState.HeatedRoll: return "熟卷";
                case FoodState.SmallRoll: return "小份卷";
                case FoodState.SmallHeatedRoll: return "小份熟卷";
                default: return "未知料理";
            }
        }

        public static string StationName(StationKind station)
        {
            switch (station)
            {
                case StationKind.None: return "空工位";
                case StationKind.Filling: return "加料";
                case StationKind.Rolling: return "卷制";
                case StationKind.Heating: return "加热";
                case StationKind.Cutting: return "切分";
                default: return "未知工站";
            }
        }

        public static string OrderName(OrderKind order)
        {
            return order == OrderKind.WholeHeated ? "高价整份熟卷" : "快速小份卷";
        }
    }

    public enum Phase { Preparation, Running, CircleBoundary, Finished }
    public enum FoodState { RiceBase, FilledRice, Roll, HeatedRoll, SmallRoll, SmallHeatedRoll }
    public enum StationKind { None, Filling, Rolling, Heating, Cutting }
    public enum OrderKind { WholeHeated, QuickSmall }
    public enum OrderStatus { Waiting, Fulfilled, Expired, Abandoned }
    public enum PlateLocation { CleanInventory, DirtyInventory, Ring, Staged }
    public enum RoundOutcome { None, OrderCompleted, BaseTradeCompleted, Failed }
    public enum EventKind
    {
        RoundStarted, RoundResumed, StepStarted, PlateMoved, StagedEntered, EntryBlocked,
        IngredientLoaded, PlateMarked, BaseSaleMarked, PlateWashed, StationMoved,
        StationSold, StationBought, FoodProcessed, ProcessingSkipped, FoodSplit,
        SplitBlocked, OrderDelivered, OrderRejected, OrderFulfilled, OrderDeparted,
        OrderAbandoned, BaseSold, CircleOpened, RoundFinished
    }
    public enum CommandKind
    {
        StartOrResume, Step, LoadIngredient, SetDeliverable, QueueBaseSale, WashOne,
        MoveStation, SellStation, BuyStation, AbandonOrder
    }

    [Serializable]
    public sealed class PlateState
    {
        public int Id;
        public PlateLocation Location;
        public int Slot = -1;
        public FoodState Food;
        public int AmountUnits;
        public bool Deliverable;
        public bool BaseSaleRequested;

        internal PlateState Copy() { return (PlateState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class OrderState
    {
        public OrderKind Kind;
        public FoodState RequiredFood;
        public int RequiredAmountUnits;
        public int RequiredQuantity;
        public int DeliveredQuantity;
        public int RemainingWait;
        public int UnitPrice;
        public OrderStatus Status;

        internal OrderState Copy() { return (OrderState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class GameEvent
    {
        public int Sequence;
        public int Step;
        public EventKind Kind;
        public int Slot;
        public int PlateId;
        public int Amount;
        public string Reason;

        internal GameEvent Copy() { return (GameEvent)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class CommandRecord
    {
        public string Id;
        public CommandKind Kind;
        public int A;
        public int B;
        public bool Flag;

        internal CommandRecord Copy() { return (CommandRecord)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class RoundSnapshot
    {
        public int SchemaVersion;
        public string RulesVersion;
        public Phase Phase;
        public int Step;
        public int StepLimit;
        public int Wallet;
        public int AdjustmentsLeft;
        public RoundOutcome Outcome;
        public PlateState[] Plates;
        public StationKind[] Stations;
        public OrderState Order;
        public int LoadedPortions;
        public int DeliveredAmountUnits;
        public int BaseSoldAmountUnits;
        public int OrderReceipts;
        public int BaseReceipts;
        public int IngredientSpent;
        public int WashSpent;
        public int MoveSpent;
        public int StationPurchaseSpent;
        public int StationSaleReceipts;
        public int AbandonSpent;
        public CommandRecord[] Commands;
        public GameEvent[] Events;

        public int CleanPlateCount { get { return Count(PlateLocation.CleanInventory); } }
        public int DirtyPlateCount { get { return Count(PlateLocation.DirtyInventory); } }
        public int RingPlateCount { get { return Count(PlateLocation.Ring); } }
        public int StagedPlateCount { get { return Count(PlateLocation.Staged); } }
        public int FoodUnitsInSystem
        {
            get
            {
                int total = 0;
                if (Plates != null) foreach (PlateState plate in Plates) if (plate != null) total += plate.AmountUnits;
                return total;
            }
        }

        private int Count(PlateLocation location)
        {
            int result = 0;
            if (Plates != null) foreach (PlateState plate in Plates) if (plate != null && plate.Location == location) result++;
            return result;
        }
    }

    public sealed class CommandResult
    {
        public bool Success;
        public bool AlreadyApplied;
        public string Reason;
        public GameEvent[] Events;
    }
}
