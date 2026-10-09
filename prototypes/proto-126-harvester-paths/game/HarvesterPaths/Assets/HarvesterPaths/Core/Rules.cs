using System;
using System.Linq;

namespace HarvesterPaths
{
    // Assistant starting values, not an established balance result.
    public static class Rules
    {
        public const string Version="0.2.0";
        public const int SchemaVersion=1,Width=10,Height=10,RoundCount=3,TotalCrops=10;
        public const int DepotX=1,DepotY=1,InitialFuel=24,MaxFuel=24,InitialWallet=4;
        public const int BaseCapacity=6,RollerCapacity=4,BoxCapacity=10;
        public const int MoveActionCost=1,MoveFuelCost=1,MudActionCost=2,MudFuelCost=3;
        public const int TurnActionCost=1,TurnFuelCost=0,HarvestActionCost=1,HarvestFuelCost=1;
        public const int WideTurnActionCost=2,WideTurnFuelCost=1,WideHarvestActionCost=2,WideHarvestFuelCost=2;
        public const int DeliveryActionCost=1,UnloadActionCost=1,RefuelActionCost=1,RefuelPrice=2,GrainPrice=2;
        public const int ConvertActionCost=1,ConvertStrawAmount=2,ConvertFuelAmount=6,PaveActionCost=1,PaveStrawAmount=2;
        public const int MaximumCommands=110,MaximumEvents=220;
        public static int CellId(int x,int y) { return y*Width+x; }
        public static bool InBounds(int x,int y) { return x>=0&&x<Width&&y>=0&&y<Height; }
        public static bool IsDepot(int x,int y) { return x==DepotX&&y==DepotY; }
        public static int RoundGoal(int round) { CheckRound(round);return round==3?4:3; }
        public static int RoundBudget(int round) { CheckRound(round);return round==1?24:round==2?32:52; }
        private static void CheckRound(int round) { if(round<1||round>RoundCount)throw new ArgumentOutOfRangeException("round"); }
        public static int Capacity(ModuleKind module) { return module==ModuleKind.Roller?RollerCapacity:module==ModuleKind.CargoBox?BoxCapacity:BaseCapacity; }
        public static string ModuleName(ModuleKind module) { switch(module){case ModuleKind.Roller:return "滚轮";case ModuleKind.WideHead:return "宽切头";case ModuleKind.CargoBox:return "大箱体";default:return "基础机";} }
        public static string TileName(TileKind kind) { return kind==TileKind.Crop?"作物":kind==TileKind.Mud?"泥地":"地面"; }
        public static string DirectionName(Direction direction) { switch(direction){case Direction.Up:return "上";case Direction.Right:return "右";case Direction.Down:return "下";default:return "左";} }
        public static GridPoint Delta(Direction direction) { switch(direction){case Direction.Up:return new GridPoint(0,1);case Direction.Right:return new GridPoint(1,0);case Direction.Down:return new GridPoint(0,-1);default:return new GridPoint(-1,0);} }
        public static TileKind InitialTileKind(int x,int y)
        {
            if(!InBounds(x,y))throw new ArgumentOutOfRangeException("x");
            if(((x==2||x==4)&&y>=1&&y<=3)||(x==6&&y>=1&&y<=4))return TileKind.Crop;
            if((x==3&&y<=4)||(x==5&&y<=5))return TileKind.Mud;
            return TileKind.Ground;
        }
        internal static TileState[] CreateTiles()
        {
            return Enumerable.Range(0,Width*Height).Select(id=>new TileState{Id=id,X=id%Width,Y=id/Width,InitialKind=InitialTileKind(id%Width,id/Width),Kind=InitialTileKind(id%Width,id/Width)}).ToArray();
        }
        public static GridPoint[] HarvestCoverage(int x,int y,Direction facing,ModuleKind module)
        {
            GridPoint forward=Delta(facing);int centerX=x+forward.X,centerY=y+forward.Y;
            if(module!=ModuleKind.WideHead)return new[]{new GridPoint(centerX,centerY)};
            // Stable left-to-right relative to the driver's facing; off-map cells are ignored as targets.
            int sideX=forward.Y,sideY=-forward.X;
            return new[]{new GridPoint(centerX-sideX,centerY-sideY),new GridPoint(centerX,centerY),new GridPoint(centerX+sideX,centerY+sideY)};
        }
    }
    public enum Phase { Active, RoundComplete, Finished }
    public enum RoundOutcome { None, Completed, Failed }
    public enum Direction { Up, Right, Down, Left }
    public enum TileKind { Crop, Ground, Mud }
    public enum ModuleKind { None, Roller, WideHead, CargoBox }
    public enum CommandKind { MoveForward, Turn, Harvest, Deliver, UnloadStraw, Refuel, ConvertStraw, Pave, ContinueRound }
    public enum EventKind { Moved, Turned, Harvested, Delivered, UnloadedStraw, Refueled, ConvertedStraw, Paved, RoundCompleted, RoundContinued, RunFinished }
    [Serializable] public sealed class GridPoint
    {
        public int X,Y;
        public GridPoint() { }
        public GridPoint(int x,int y) { X=x;Y=y; }
        internal GridPoint Copy() { return new GridPoint(X,Y); }
    }
    [Serializable] public sealed class TileState
    {
        public int Id,X,Y;
        public TileKind InitialKind,Kind;
        public bool Harvested,Paved;
        internal TileState Copy() { return (TileState)MemberwiseClone(); }
    }
    [Serializable] public sealed class VehicleState
    {
        public int X,Y,Fuel,Grain,Straw;
        public Direction Facing;
        public ModuleKind Module;
        public int Capacity { get { return Rules.Capacity(Module); } }
        public int CargoUsed { get { return Grain+Straw; } }
        internal VehicleState Copy() { return (VehicleState)MemberwiseClone(); }
    }
    [Serializable] public sealed class RoundState
    {
        public int Number,Goal,Budget,Delivered,ActionsSpent;
        public ModuleKind Module;
        public bool Entered,Complete;
        internal RoundState Copy() { return (RoundState)MemberwiseClone(); }
    }
    [Serializable] public sealed class GameEvent
    {
        public int Sequence,ActionIndex,RoundIndex,X,Y,Amount,ActionCost,FuelCost,GrainDelta,StrawDelta,FuelDelta,WalletDelta;
        public EventKind Kind;
        public string Reason;
        public GridPoint[] Cells;
        internal GameEvent Copy() { var copy=(GameEvent)MemberwiseClone();copy.Cells=Cells.Select(c=>c.Copy()).ToArray();return copy; }
    }
    [Serializable] public sealed class CommandRecord
    {
        public string Id;
        public CommandKind Kind;
        public int A,B;
        internal CommandRecord Copy() { return (CommandRecord)MemberwiseClone(); }
    }
    [Serializable] public sealed class RoundSnapshot
    {
        public int SchemaVersion;
        public string RulesVersion;
        public ModuleKind InitialModule;
        public Phase Phase;
        public RoundOutcome Outcome;
        public int RoundIndex,ActionsRemaining,DeliveredThisRound,DeliveredTotal,StoredStraw,Wallet,HarvestedCrops,ConvertedStraw,PavedStraw,FuelSpent,FuelAdded,ActionIndex;
        public VehicleState Vehicle;
        public TileState[] Tiles;
        public RoundState[] Rounds;
        public CommandRecord[] Commands;
        public GameEvent[] Events;
        public int Goal { get { return Rules.RoundGoal(RoundIndex); } }
        public int RemainingCrops { get { return Rules.TotalCrops-HarvestedCrops; } }
        internal RoundSnapshot MemberCopy() { return (RoundSnapshot)MemberwiseClone(); }
    }
    public sealed class CommandResult
    {
        public bool Success,AlreadyApplied;
        public string Reason;
        public GameEvent[] Events;
    }
    public class ActionPreview
    {
        public bool Legal;
        public int ActionCost,FuelCost,StrawRequired,Amount,FuelGain;
        public string Reason;
        public GridPoint[] TargetCells=new GridPoint[0];
        public int Cost { get { return ActionCost; } }
    }
    public sealed class HarvestPreview : ActionPreview
    {
        public GridPoint[] CoverageCells=new GridPoint[0];
        public int GrainProduced,StrawProduced,SpaceRequired;
    }
}
