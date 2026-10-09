using System;
using System.Linq;

namespace SnakeHatchery
{
    // Assistant v0.2 starting values; play balance has not been established.
    public static class Rules
    {
        public const string Version = "0.2.0";
        public const int SchemaVersion = 1;
        public const int Width = 8, Height = 6, ActionBudget = 80;
        public const int MaxClones = 2, MinMainSegments = 2;
        public const int BaseCapacity = 2, BaseCollectionRate = 1;
        public const int WarehouseCapacity = 2, CollectorRate = 1;
        public const int ModuleInventoryCapacity = 2, ModuleCount = 7;
        public const int DeliveryGoal = 8, DeliveryX = 1, DeliveryY = 1;
        public const int DockX = DeliveryX, DockY = DeliveryY;
        public const int ActionCost = 1, MaximumRouteLength = 32, MaximumCommands = ActionBudget;
        public static bool InBounds(int x,int y) { return x>=0 && x<Width && y>=0 && y<Height; }
        public static int CellId(int x,int y) { return y*Width+x; }
        public static string KindName(SegmentKind kind) { return kind==SegmentKind.Collector?"采集节":kind==SegmentKind.Warehouse?"货仓节":"基础节"; }
        public static string BehaviorName(CloneBehavior behavior) { return behavior==CloneBehavior.Shuttle?"往返搬运":"巡线采集"; }
        public static string DirectionName(Direction direction) { return new[]{"上","右","下","左"}[(int)direction]; }
        internal static GridPoint Delta(Direction direction)
        {
            switch(direction) { case Direction.Up:return new GridPoint(0,1);case Direction.Right:return new GridPoint(1,0);case Direction.Down:return new GridPoint(0,-1);default:return new GridPoint(-1,0); }
        }
        internal static CellState[] CreateCells()
        {
            var cells=new CellState[Width*Height];
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)cells[CellId(x,y)]=new CellState{Id=CellId(x,y),X=x,Y=y,Blocked=x==3&&(y==1||y==2||y==4)};
            cells[CellId(2,3)].Cargo=cells[CellId(2,3)].InitialCargo=4;
            cells[CellId(6,3)].Cargo=cells[CellId(6,3)].InitialCargo=4;
            return cells;
        }
        // Authored loop, not a path finder. A cut head outside this loop has no suggested task.
        // Both tasks use exactly this public route; Patrol stops after one circuit, Shuttle repeats.
        public static GridPoint[] SuggestedRoute(int x,int y,CloneBehavior behavior)
        {
            var loop=new[]{new GridPoint(0,0),new GridPoint(1,0),new GridPoint(1,1),new GridPoint(2,1),new GridPoint(2,2),new GridPoint(2,3),new GridPoint(1,3),new GridPoint(0,3),new GridPoint(0,2),new GridPoint(0,1)};
            int index=Array.FindIndex(loop,p=>p.X==x&&p.Y==y);
            if(index<0)return new GridPoint[0];
            var result=new GridPoint[loop.Length+1];
            for(int i=0;i<result.Length;i++)result[i]=loop[(index+i)%loop.Length].Copy();
            return result;
        }
    }
    public enum Phase { Active, Finished }
    public enum RoundOutcome { None, Completed, Failed }
    public enum PresetKind { Short, Long }
    public enum SegmentKind { Basic, Collector, Warehouse }
    public enum CloneBehavior { PatrolCollect, Shuttle }
    public enum Direction { Up, Right, Down, Left }
    public enum ModuleLocation { Main, Clone, Inventory, Ground }
    public enum CommandKind { Move, Wait, Collect, Deliver, Split, Reclaim, CollectModule, DropModule }
    public enum EventKind { Moved, Grew, Collected, Delivered, Split, Reclaimed, ModuleCollected, ModuleDropped, CargoDropped, CloneMoved, CloneWaited, TaskCompleted, Waited, Collision, RoundFinished }

    [Serializable] public sealed class GridPoint
    {
        public int X,Y;
        public GridPoint() { }
        public GridPoint(int x,int y) { X=x;Y=y; }
        internal GridPoint Copy() { return new GridPoint(X,Y); }
    }
    [Serializable] public sealed class SegmentState
    {
        public int ModuleId,X,Y;
        public SegmentKind Kind;
        internal SegmentState Copy() { return (SegmentState)MemberwiseClone(); }
    }
    [Serializable] public sealed class SnakeState
    {
        public int Id;
        public bool IsMain;
        public SegmentState[] Segments;
        public int Cargo;
        public CloneBehavior Behavior;
        public GridPoint[] Route;
        public int RouteIndex,BornAction;
        public string WaitingReason;
        public bool TaskComplete;
        public Direction Direction;
        public int HeadX { get { return Segments[0].X; } }
        public int HeadY { get { return Segments[0].Y; } }
        public int Length { get { return Segments.Length; } }
        public int Capacity { get { return Rules.BaseCapacity+Segments.Count(s=>s.Kind==SegmentKind.Warehouse)*Rules.WarehouseCapacity; } }
        public int CollectionRate { get { return Rules.BaseCollectionRate+Segments.Count(s=>s.Kind==SegmentKind.Collector)*Rules.CollectorRate; } }
        internal SnakeState Copy() { var c=(SnakeState)MemberwiseClone();c.Segments=Segments.Select(s=>s.Copy()).ToArray();c.Route=Route.Select(p=>p.Copy()).ToArray();return c; }
    }
    [Serializable] public sealed class ModuleState
    {
        public int Id,OwnerId,X,Y;
        public SegmentKind Kind;
        public ModuleLocation Location;
        internal ModuleState Copy() { return (ModuleState)MemberwiseClone(); }
    }
    [Serializable] public sealed class CellState
    {
        public int Id,X,Y,Cargo,InitialCargo;
        public bool Blocked;
        internal CellState Copy() { return (CellState)MemberwiseClone(); }
    }
    [Serializable] public sealed class GameEvent
    {
        public int Sequence,ActionIndex,EntityId,X,Y,Amount;
        public EventKind Kind;
        public string Reason;
        internal GameEvent Copy() { return (GameEvent)MemberwiseClone(); }
    }
    [Serializable] public sealed class CommandRecord
    {
        public string Id;
        public CommandKind Kind;
        public int A,B;
        public GridPoint[] Route;
        internal CommandRecord Copy() { var c=(CommandRecord)MemberwiseClone();c.Route=Route.Select(p=>p.Copy()).ToArray();return c; }
    }
    [Serializable] public sealed class RoundSnapshot
    {
        public int SchemaVersion;
        public string RulesVersion;
        public PresetKind Preset;
        public Phase Phase;
        public RoundOutcome Outcome;
        public int ActionsRemaining,Delivered,DeliveryGoal,NextCloneId;
        public SnakeState Main;
        public SnakeState[] Clones;
        public ModuleState[] Modules;
        public int[] InventoryModuleIds;
        public CellState[] Cells;
        public CommandRecord[] Commands;
        public GameEvent[] Events;
        public int GroundCargo { get { return Cells.Sum(c=>c.Cargo); } }
        public int CarriedCargo { get { return Main.Cargo+Clones.Sum(c=>c.Cargo); } }
    }
    public sealed class CommandResult
    {
        public bool Success,AlreadyApplied;
        public string Reason;
        public GameEvent[] Events;
    }
    public sealed class MovePreview
    {
        public bool Legal,WillGrow;
        public int TargetX,TargetY,Cost;
        public string Reason;
    }
    public sealed class SplitPreview
    {
        public bool Legal;
        public int Cost,MainLengthAfter,CloneLength,MainCapacityAfter,CloneCapacity,LostCollectionRate,LostCapacity,TransferredCargo,DroppedCargo;
        public string Reason;
    }
    public sealed class ReclaimPreview
    {
        public bool Legal;
        public int Cost,CloneId,TransferredCargo,DroppedCargo,ModulesToInventory,DroppedModules,DropX,DropY;
        public string Reason;
    }
}
