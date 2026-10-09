using System;
using System.Linq;

namespace MagnetScavenger
{
    // Assistant starting values, not an established balance result.
    public static class Rules
    {
        public const string Version="0.2.0";
        public const int SchemaVersion=1,Width=12,Height=8;
        public const int InitialEnergy=60,BaseRange=4,MaxLoad=6,InitialWallet=0;
        public const int MoveCost=1,RotateCost=2,AttractCost=2,RetainCost=1,SellCost=1,DetachCost=2,AttachCost=1;
        public const int OrderBonus=2,ItemCount=5,MaximumCommands=InitialEnergy;
        public const int InitialRootX=1,InitialRootY=1,DepotMinX=0,DepotMaxX=2,DepotMinY=0,DepotMaxY=3;
        public static bool InBounds(int x,int y) { return x>=0&&x<Width&&y>=0&&y<Height; }
        public static int CellId(int x,int y) { return y*Width+x; }
        public static bool IsBlocked(int x,int y) { return x==5&&(y==2||y==4); }
        public static bool IsDepot(int x,int y) { return x>=DepotMinX&&x<=DepotMaxX&&y>=DepotMinY&&y<=DepotMaxY; }
        public static string ItemName(ItemKind kind) { switch(kind){case ItemKind.LongRod:return "长杆";case ItemKind.Elbow:return "L形件";case ItemKind.HeavyBlock:return "重块";default:return "短片";} }
        public static string ShapeName(ItemKind kind) { return ItemName(kind); }
        public static string OrderName(OrderKind kind) { return kind==OrderKind.Parts?"工坊零件：长杆+L形件":"重料回收：长杆+重块"; }
        public static string DirectionName(Direction d) { return new[]{"上","右","下","左"}[(int)d]; }
        public static int Weight(ItemKind kind) { switch(kind){case ItemKind.LongRod:case ItemKind.Elbow:return 2;case ItemKind.HeavyBlock:return 5;default:return 1;} }
        public static int BaseValue(ItemKind kind) { switch(kind){case ItemKind.LongRod:return 6;case ItemKind.Elbow:return 5;case ItemKind.HeavyBlock:return 9;default:return 3;} }
        public static GridPoint[] LocalCells(ItemKind kind)
        {
            switch(kind){case ItemKind.LongRod:return new[]{new GridPoint(0,0),new GridPoint(1,0),new GridPoint(2,0)};
                case ItemKind.Elbow:return new[]{new GridPoint(0,0),new GridPoint(1,0),new GridPoint(1,1)};
                case ItemKind.HeavyBlock:return new[]{new GridPoint(0,0),new GridPoint(1,0),new GridPoint(0,1),new GridPoint(1,1)};
                default:return new[]{new GridPoint(0,0)};}
        }
        public static GridPoint LocalExit(ItemKind kind) { return kind==ItemKind.LongRod?new GridPoint(2,0):kind==ItemKind.Elbow?new GridPoint(1,1):kind==ItemKind.HeavyBlock?new GridPoint(1,0):new GridPoint(0,0); }
        public static Direction ExitDirection(ItemKind kind,Direction facing) { return kind==ItemKind.Elbow?(Direction)(((int)facing+3)%4):facing; }
        public static GridPoint Delta(Direction direction) { switch(direction){case Direction.Up:return new GridPoint(0,1);case Direction.Right:return new GridPoint(1,0);case Direction.Down:return new GridPoint(0,-1);default:return new GridPoint(-1,0);} }
        public static GridPoint Transform(GridPoint local,int anchorX,int anchorY,Direction facing)
        {
            int x=local.X,y=local.Y;
            for(int i=0;i<(1-(int)facing+4)%4;i++){int old=x;x=-y;y=old;}
            return new GridPoint(anchorX+x,anchorY+y);
        }
        public static GridPoint[] ItemCells(ItemKind kind,int anchorX,int anchorY,Direction facing) { return LocalCells(kind).Select(p=>Transform(p,anchorX,anchorY,facing)).ToArray(); }
        public static GridPoint ItemExit(ItemKind kind,int anchorX,int anchorY,Direction facing) { return Transform(LocalExit(kind),anchorX,anchorY,facing); }
        internal static ItemState[] CreateItems()
        {
            return new[]{CreateItem(1,ItemKind.ShortPlate,2,1),CreateItem(2,ItemKind.LongRod,4,3),CreateItem(3,ItemKind.Elbow,8,3),CreateItem(4,ItemKind.HeavyBlock,6,5),CreateItem(5,ItemKind.ShortPlate,9,5)};
        }
        private static ItemState CreateItem(int id,ItemKind kind,int x,int y)
        {
            var item=new ItemState{Id=id,Kind=kind,Location=ItemLocation.Crate,AnchorX=x,AnchorY=y,Facing=Direction.Right};SetPose(item,x,y,Direction.Right);return item;
        }
        internal static void SetPose(ItemState item,int x,int y,Direction facing)
        {
            item.AnchorX=x;item.AnchorY=y;item.Facing=facing;item.EntryX=x;item.EntryY=y;item.Cells=ItemCells(item.Kind,x,y,facing);
            GridPoint exit=ItemExit(item.Kind,x,y,facing);item.ExitX=exit.X;item.ExitY=exit.Y;item.ExitDirection=ExitDirection(item.Kind,facing);
        }
        internal static OrderState CreateOrder(OrderKind kind)
        {
            return new OrderState{Kind=kind,Requirements=new[]{new OrderRequirement{Kind=ItemKind.LongRod,Required=1},new OrderRequirement{Kind=kind==OrderKind.Parts?ItemKind.Elbow:ItemKind.HeavyBlock,Required=1}}};
        }
    }
    public enum Phase { Active, AwaitingDisposition, Finished }
    public enum RoundOutcome { None, Completed, Failed }
    public enum Direction { Up, Right, Down, Left }
    public enum ItemKind { ShortPlate, LongRod, Elbow, HeavyBlock }
    public enum OrderKind { Parts, Heavy }
    public enum ItemLocation { Crate, Pending, Chain, Storage, Sold }
    public enum CommandKind { Move, Rotate, Attract, Retain, Sell, Detach, Attach }
    public enum EventKind { Moved, Rotated, Attracted, Retained, Sold, Detached, Attached, RoundFinished }
    [Serializable] public sealed class GridPoint
    {
        public int X,Y;
        public GridPoint() { }
        public GridPoint(int x,int y) { X=x;Y=y; }
        internal GridPoint Copy() { return new GridPoint(X,Y); }
    }
    [Serializable] public sealed class ItemState
    {
        public int Id,AnchorX,AnchorY,EntryX,EntryY,ExitX,ExitY;
        public ItemKind Kind;
        public ItemLocation Location;
        public Direction Facing,ExitDirection;
        public GridPoint[] Cells;
        public int Weight { get { return Rules.Weight(Kind); } }
        public int BaseValue { get { return Rules.BaseValue(Kind); } }
        internal ItemState Copy() { var c=(ItemState)MemberwiseClone();c.Cells=Cells.Select(p=>p.Copy()).ToArray();return c; }
    }
    [Serializable] public sealed class OrderRequirement
    {
        public ItemKind Kind;
        public int Required,Delivered;
        internal OrderRequirement Copy() { return (OrderRequirement)MemberwiseClone(); }
    }
    [Serializable] public sealed class OrderState
    {
        public OrderKind Kind;
        public OrderRequirement[] Requirements;
        public int Delivered { get { return Requirements.Sum(r=>r.Delivered); } }
        public int Required { get { return Requirements.Sum(r=>r.Required); } }
        public bool Complete { get { return Requirements.All(r=>r.Delivered>=r.Required); } }
        internal OrderState Copy() { var c=(OrderState)MemberwiseClone();c.Requirements=Requirements.Select(r=>r.Copy()).ToArray();return c; }
    }
    [Serializable] public sealed class GameEvent
    {
        public int Sequence,ActionIndex,ItemId,X,Y,Amount;
        public EventKind Kind;
        public string Reason;
        internal GameEvent Copy() { return (GameEvent)MemberwiseClone(); }
    }
    [Serializable] public sealed class CommandRecord
    {
        public string Id;
        public CommandKind Kind;
        public int A;
        internal CommandRecord Copy() { return (CommandRecord)MemberwiseClone(); }
    }
    [Serializable] public sealed class RoundSnapshot
    {
        public int SchemaVersion;
        public string RulesVersion;
        public OrderKind OrderKind;
        public Phase Phase;
        public RoundOutcome Outcome;
        public int RootX,RootY,Energy,Wallet,PendingItemId,EndX,EndY,TotalWeight,ActionIndex;
        public Direction Facing,EndDirection;
        public int[] ChainIds,StorageIds,SoldIds;
        public GridPoint[] OccupiedCells;
        public ItemState[] Items;
        public OrderState Order;
        public CommandRecord[] Commands;
        public GameEvent[] Events;
    }
    public sealed class CommandResult
    {
        public bool Success,AlreadyApplied;
        public string Reason;
        public GameEvent[] Events;
    }
    public sealed class ActionPreview
    {
        public bool Legal;
        public int Cost,ItemId,AfterWeight;
        public GridPoint[] Cells;
        public int[] AffectedItemIds;
        public string Reason;
    }
    public sealed class AttractPreview
    {
        public bool Legal;
        public int Cost,ItemId,Distance,FirstX,FirstY,BlockX,BlockY,AfterWeight,EntryX,EntryY,ExitX,ExitY;
        public Direction ExitDirection;
        public GridPoint[] RayCells,CandidateCells;
        public string Reason;
    }
}
