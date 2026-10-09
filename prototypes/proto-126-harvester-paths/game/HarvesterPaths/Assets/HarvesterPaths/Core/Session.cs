using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace HarvesterPaths
{
    public sealed class Session
    {
        private readonly RoundSnapshot state;
        private readonly List<CommandRecord> commands=new List<CommandRecord>();
        private readonly List<GameEvent> events=new List<GameEvent>();
        private readonly HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
        private GameEvent[] last=new GameEvent[0];
        public RoundSnapshot State { get { return ExportSnapshot(); } }
        public GameEvent[] LastEvents { get { return last.Select(e=>e.Copy()).ToArray(); } }
        private Session(ModuleKind module)
        {
            state=new RoundSnapshot{SchemaVersion=Rules.SchemaVersion,RulesVersion=Rules.Version,InitialModule=module,Phase=Phase.Active,Outcome=RoundOutcome.None,
                RoundIndex=1,ActionsRemaining=Rules.RoundBudget(1),Wallet=Rules.InitialWallet,Tiles=Rules.CreateTiles(),
                Vehicle=new VehicleState{X=Rules.DepotX,Y=Rules.DepotY,Facing=Direction.Right,Module=module,Fuel=Rules.InitialFuel},
                Rounds=Enumerable.Range(1,Rules.RoundCount).Select(n=>new RoundState{Number=n,Goal=Rules.RoundGoal(n),Budget=Rules.RoundBudget(n),Module=n==1?module:ModuleKind.None,Entered=n==1}).ToArray()};
        }
        public static Session CreatePrototype(ModuleKind module=ModuleKind.None)
        {
            if(!Enum.IsDefined(typeof(ModuleKind),module))throw new ArgumentOutOfRangeException("module");return new Session(module);
        }
        public Session Retry() { return new Session(state.InitialModule); }
        public CommandResult MoveForward(string commandId=null) { return Execute(CommandKind.MoveForward,0,0,commandId); }
        public CommandResult Turn(int quarterTurns,string commandId=null) { return Execute(CommandKind.Turn,quarterTurns,0,commandId); }
        public CommandResult Harvest(string commandId=null) { return Execute(CommandKind.Harvest,0,0,commandId); }
        public CommandResult Deliver(int amount,string commandId=null) { return Execute(CommandKind.Deliver,amount,0,commandId); }
        public CommandResult UnloadStraw(int amount,string commandId=null) { return Execute(CommandKind.UnloadStraw,amount,0,commandId); }
        public CommandResult Refuel(string commandId=null) { return Execute(CommandKind.Refuel,0,0,commandId); }
        public CommandResult ConvertStraw(string commandId=null) { return Execute(CommandKind.ConvertStraw,0,0,commandId); }
        public CommandResult Pave(int x,int y,string commandId=null) { return Execute(CommandKind.Pave,x,y,commandId); }
        public CommandResult ContinueRound(ModuleKind module,string commandId=null) { return Execute(CommandKind.ContinueRound,(int)module,0,commandId); }

        private ActionPreview BasicPreview(int actionCost,int fuelCost,bool maintenance=false)
        {
            var p=new ActionPreview{ActionCost=actionCost,FuelCost=fuelCost,Reason="本局已结束"};
            if(state.Phase==Phase.Finished)return p;
            if(state.Phase==Phase.RoundComplete&&!maintenance){p.Reason="本轮已达标，仅可在仓库用旧预算卸秸/补油或显式进入下一轮";return p;}
            if(state.ActionsRemaining<actionCost){p.Reason="行动不足：需要"+actionCost+"，剩余"+state.ActionsRemaining;return p;}
            if(state.Vehicle.Fuel<fuelCost){p.Reason="燃料不足：需要"+fuelCost+"，剩余"+state.Vehicle.Fuel+"；补油不增加行动";return p;}
            p.Legal=true;p.Reason="合法动作；行动-"+actionCost+"，燃料-"+fuelCost;return p;
        }
        private static void Invalid(ActionPreview p,string reason) { p.Legal=false;p.Reason=reason; }
        private bool AtDepot { get { return Rules.IsDepot(state.Vehicle.X,state.Vehicle.Y); } }
        public ActionPreview PreviewMoveForward()
        {
            GridPoint d=Rules.Delta(state.Vehicle.Facing);int x=state.Vehicle.X+d.X,y=state.Vehicle.Y+d.Y;
            bool mud=Rules.InBounds(x,y)&&state.Tiles[Rules.CellId(x,y)].Kind==TileKind.Mud;
            int action=mud&&state.Vehicle.Module!=ModuleKind.Roller?Rules.MudActionCost:Rules.MoveActionCost;
            int fuel=mud&&state.Vehicle.Module!=ModuleKind.Roller?Rules.MudFuelCost:Rules.MoveFuelCost;
            if(state.Vehicle.Module==ModuleKind.CargoBox)fuel++;
            ActionPreview p=BasicPreview(action,fuel);p.TargetCells=new[]{new GridPoint(x,y)};
            if(!p.Legal)return p;
            if(!Rules.InBounds(x,y)){Invalid(p,"前方超出田地边界");return p;}
            if(state.Tiles[Rules.CellId(x,y)].Kind==TileKind.Crop)Invalid(p,"前方("+(x+1)+","+(y+1)+")仍是作物，先收割成为通路");
            return p;
        }
        public ActionPreview PreviewTurn(int quarterTurns)
        {
            bool wide=state.Vehicle.Module==ModuleKind.WideHead;
            ActionPreview p=BasicPreview(wide?Rules.WideTurnActionCost:Rules.TurnActionCost,wide?Rules.WideTurnFuelCost:Rules.TurnFuelCost);
            if(quarterTurns!=1&&quarterTurns!=-1)Invalid(p,"每次只允许正负一个90度转向");return p;
        }
        public HarvestPreview PreviewHarvest()
        {
            bool wide=state.Vehicle.Module==ModuleKind.WideHead;
            ActionPreview basic=BasicPreview(wide?Rules.WideHarvestActionCost:Rules.HarvestActionCost,wide?Rules.WideHarvestFuelCost:Rules.HarvestFuelCost);
            var p=new HarvestPreview{Legal=basic.Legal,ActionCost=basic.ActionCost,FuelCost=basic.FuelCost,Reason=basic.Reason,
                CoverageCells=Rules.HarvestCoverage(state.Vehicle.X,state.Vehicle.Y,state.Vehicle.Facing,state.Vehicle.Module)};
            p.TargetCells=p.CoverageCells.Where(c=>Rules.InBounds(c.X,c.Y)&&state.Tiles[Rules.CellId(c.X,c.Y)].Kind==TileKind.Crop).Select(c=>c.Copy()).ToArray();
            p.GrainProduced=p.StrawProduced=p.TargetCells.Length;p.SpaceRequired=p.GrainProduced+p.StrawProduced;
            if(!p.Legal)return p;
            if(p.TargetCells.Length==0){Invalid(p,"切幅内没有未收割作物；已收割地块不能再次产出");return p;}
            if(state.Vehicle.CargoUsed+p.SpaceRequired>state.Vehicle.Capacity)Invalid(p,"整次收割需要空间"+p.SpaceRequired+"，余量"+(state.Vehicle.Capacity-state.Vehicle.CargoUsed)+"；不收一半、不删除秸秆");
            return p;
        }
        public ActionPreview PreviewDeliver(int amount)
        {
            ActionPreview p=BasicPreview(Rules.DeliveryActionCost,0);p.Amount=amount;
            if(!p.Legal)return p;
            if(!AtDepot){Invalid(p,"交粮只能在仓库实际卸下");return p;}
            if(amount<=0||amount>state.Vehicle.Grain||amount>state.Goal-state.DeliveredThisRound)Invalid(p,"交粮数量须大于0，不超过车粮及本轮尚需数量");return p;
        }
        public ActionPreview PreviewUnloadStraw(int amount)
        {
            ActionPreview p=BasicPreview(Rules.UnloadActionCost,0,true);p.Amount=amount;
            if(!p.Legal)return p;
            if(!AtDepot){Invalid(p,"卸秸只能在仓库");return p;}
            if(amount<=0||amount>state.Vehicle.Straw)Invalid(p,"卸秸数量须大于0且不超过车载秸秆；仓库存秸不能远程取回");return p;
        }
        public ActionPreview PreviewRefuel()
        {
            ActionPreview p=BasicPreview(Rules.RefuelActionCost,0,true);p.FuelGain=Rules.MaxFuel-state.Vehicle.Fuel;
            if(!p.Legal)return p;
            if(!AtDepot){Invalid(p,"付费补油只能在仓库");return p;}
            if(state.Vehicle.Fuel>=Rules.MaxFuel){Invalid(p,"油箱已满，不重复收补给费");return p;}
            if(state.Wallet<Rules.RefuelPrice)Invalid(p,"现金不足：补油需要"+Rules.RefuelPrice);return p;
        }
        public ActionPreview PreviewConvertStraw()
        {
            ActionPreview p=BasicPreview(Rules.ConvertActionCost,0);p.StrawRequired=Rules.ConvertStrawAmount;p.FuelGain=Math.Min(Rules.ConvertFuelAmount,Rules.MaxFuel-state.Vehicle.Fuel);
            if(!p.Legal)return p;
            if(state.Vehicle.Straw<p.StrawRequired){Invalid(p,"转换需要车载秸秆"+p.StrawRequired+"；仓库存秸不参与");return p;}
            if(p.FuelGain<=0)Invalid(p,"油箱已满，不能白耗秸秆；固定消耗2秸，实际补油最多6");return p;
        }
        public ActionPreview PreviewPave(int x,int y)
        {
            ActionPreview p=BasicPreview(Rules.PaveActionCost,0);p.StrawRequired=Rules.PaveStrawAmount;p.TargetCells=new[]{new GridPoint(x,y)};
            if(!p.Legal)return p;
            if(!Rules.InBounds(x,y)){Invalid(p,"铺路目标超出田地边界");return p;}
            if(Math.Abs(x-state.Vehicle.X)+Math.Abs(y-state.Vehicle.Y)!=1){Invalid(p,"只能铺正交相邻一格泥地，不能铺自身或远处");return p;}
            if(state.Tiles[Rules.CellId(x,y)].Kind!=TileKind.Mud){Invalid(p,"目标不是未铺泥地，不能重复铺路");return p;}
            if(state.Vehicle.Straw<p.StrawRequired)Invalid(p,"铺路需要车载秸秆"+p.StrawRequired+"；与转换燃料互斥消耗");return p;
        }
        public ActionPreview PreviewContinueRound(ModuleKind module)
        {
            var p=new ActionPreview{Reason="仅本轮达标后在仓库进入下一轮"};
            if(state.Phase!=Phase.RoundComplete||state.RoundIndex>=Rules.RoundCount||!AtDepot)return p;
            if(!Enum.IsDefined(typeof(ModuleKind),module)){p.Reason="未知模块";return p;}
            if(state.Vehicle.CargoUsed>Rules.Capacity(module)){p.Reason="现有货物"+state.Vehicle.CargoUsed+"超过下一模块容量"+Rules.Capacity(module)+"；先用旧预算卸秸或选可承载模块";return p;}
            p.Legal=true;p.Reason="进入下一固定订单预算，保留车/图/粮秸/油/钱；本轮模块锁定为"+Rules.ModuleName(module);return p;
        }

        private CommandResult Execute(CommandKind kind,int a,int b,string id)
        {
            last=new GameEvent[0];
            if(id!=null&&ids.Contains(id))return new CommandResult{Success=true,AlreadyApplied=true,Reason="命令已提交，不重复扣费或交付",Events=last};
            if(id!=null&&(string.IsNullOrWhiteSpace(id)||id.Length>128))return Reject("命令身份非法");
            ActionPreview p;
            switch(kind)
            {
                case CommandKind.MoveForward:p=PreviewMoveForward();break;
                case CommandKind.Turn:p=PreviewTurn(a);break;
                case CommandKind.Harvest:p=PreviewHarvest();break;
                case CommandKind.Deliver:p=PreviewDeliver(a);break;
                case CommandKind.UnloadStraw:p=PreviewUnloadStraw(a);break;
                case CommandKind.Refuel:p=PreviewRefuel();break;
                case CommandKind.ConvertStraw:p=PreviewConvertStraw();break;
                case CommandKind.Pave:p=PreviewPave(a,b);break;
                case CommandKind.ContinueRound:p=PreviewContinueRound((ModuleKind)a);break;
                default:return Reject("未知命令");
            }
            if(!p.Legal)return Reject(p.Reason);
            int before=events.Count;state.ActionIndex=commands.Count+1;
            state.ActionsRemaining-=p.ActionCost;state.Rounds[state.RoundIndex-1].ActionsSpent+=p.ActionCost;
            state.Vehicle.Fuel-=p.FuelCost;state.FuelSpent+=p.FuelCost;
            VehicleState v=state.Vehicle;
            switch(kind)
            {
                case CommandKind.MoveForward:
                    v.X=p.TargetCells[0].X;v.Y=p.TargetCells[0].Y;Emit(EventKind.Moved,1,p,"实际前进；目标("+(v.X+1)+","+(v.Y+1)+")",fuelDelta:-p.FuelCost);break;
                case CommandKind.Turn:
                    v.Facing=(Direction)(((int)v.Facing+a+4)%4);Emit(EventKind.Turned,a,p,"转向"+Rules.DirectionName(v.Facing),fuelDelta:-p.FuelCost);break;
                case CommandKind.Harvest:
                    HarvestPreview harvest=(HarvestPreview)p;
                    foreach(GridPoint c in p.TargetCells){TileState tile=state.Tiles[Rules.CellId(c.X,c.Y)];tile.Kind=TileKind.Ground;tile.Harvested=true;}
                    v.Grain+=harvest.GrainProduced;v.Straw+=harvest.StrawProduced;state.HarvestedCrops+=harvest.TargetCells.Length;
                    Emit(EventKind.Harvested,harvest.TargetCells.Length,p,"实际作物整次收割，地块成为通路；粮秸各+"+harvest.TargetCells.Length,harvest.GrainProduced,harvest.StrawProduced,-p.FuelCost);break;
                case CommandKind.Deliver:
                    v.Grain-=a;state.DeliveredThisRound+=a;state.DeliveredTotal+=a;state.Rounds[state.RoundIndex-1].Delivered+=a;state.Wallet+=a*Rules.GrainPrice;
                    Emit(EventKind.Delivered,a,p,"实际交粮"+a+"，现金+"+(a*Rules.GrainPrice)+"，本轮"+state.DeliveredThisRound+"/"+state.Goal,-a,0,0,a*Rules.GrainPrice);break;
                case CommandKind.UnloadStraw:
                    v.Straw-=a;state.StoredStraw+=a;Emit(EventKind.UnloadedStraw,a,p,"车载秸进入仓库"+a+"；只入库记账，不能远程转换或铺路",strawDelta:-a);break;
                case CommandKind.Refuel:
                    v.Fuel+=p.FuelGain;state.FuelAdded+=p.FuelGain;state.Wallet-=Rules.RefuelPrice;
                    Emit(EventKind.Refueled,p.FuelGain,p,"补油实际+"+p.FuelGain+"，现金-"+Rules.RefuelPrice+"；行动预算不增加",fuelDelta:p.FuelGain,walletDelta:-Rules.RefuelPrice);break;
                case CommandKind.ConvertStraw:
                    v.Straw-=p.StrawRequired;state.ConvertedStraw+=p.StrawRequired;v.Fuel+=p.FuelGain;state.FuelAdded+=p.FuelGain;
                    Emit(EventKind.ConvertedStraw,p.FuelGain,p,"车载秸-"+p.StrawRequired+"，实际油+"+p.FuelGain+"；不增加行动",strawDelta:-p.StrawRequired,fuelDelta:p.FuelGain);break;
                case CommandKind.Pave:
                    TileState paved=state.Tiles[Rules.CellId(a,b)];paved.Kind=TileKind.Ground;paved.Paved=true;v.Straw-=p.StrawRequired;state.PavedStraw+=p.StrawRequired;
                    Emit(EventKind.Paved,1,p,"邻格泥地永久变通路，车载秸-"+p.StrawRequired+"；三轮保留",strawDelta:-p.StrawRequired);break;
                case CommandKind.ContinueRound:
                    state.RoundIndex++;state.DeliveredThisRound=0;state.ActionsRemaining=Rules.RoundBudget(state.RoundIndex);state.Phase=Phase.Active;v.Module=(ModuleKind)a;
                    RoundState next=state.Rounds[state.RoundIndex-1];next.Entered=true;next.Module=v.Module;
                    Emit(EventKind.RoundContinued,state.RoundIndex,p,"明确进入第"+state.RoundIndex+"轮，固定预算"+state.ActionsRemaining+"；模块"+Rules.ModuleName(v.Module)+"，图/货/油/钱延续");break;
            }
            // A committed delivery wins at zero remaining actions; maintenance at zero never fails an already completed round.
            if(state.DeliveredThisRound==state.Goal)
            {
                state.Rounds[state.RoundIndex-1].Complete=true;
                if(state.RoundIndex==Rules.RoundCount)
                {
                    state.Phase=Phase.Finished;state.Outcome=RoundOutcome.Completed;
                    Emit(EventKind.RunFinished,state.DeliveredTotal,new ActionPreview(),"三轮实际交粮完成；先目标、后行动截止");
                }
                else if(state.Phase!=Phase.RoundComplete)
                {
                    state.Phase=Phase.RoundComplete;Emit(EventKind.RoundCompleted,state.DeliveredThisRound,new ActionPreview(),"本轮达标；旧预算仅仓库卸秸/补油，显式进入下一轮");
                }
            }
            else if(state.ActionsRemaining==0)
            {
                state.Phase=Phase.Finished;state.Outcome=RoundOutcome.Failed;
                Emit(EventKind.RunFinished,state.DeliveredThisRound,new ActionPreview(),"本轮行动截止，已交"+state.DeliveredThisRound+"/"+state.Goal+"；补油不能延长期限");
            }
            id=id??"c"+(commands.Count+1).ToString(CultureInfo.InvariantCulture);while(ids.Contains(id))id="_"+id;
            commands.Add(new CommandRecord{Id=id,Kind=kind,A=a,B=b});ids.Add(id);last=events.Skip(before).Select(e=>e.Copy()).ToArray();
            return new CommandResult{Success=true,Reason=last[last.Length-1].Reason,Events=LastEvents};
        }
        private void Emit(EventKind kind,int amount,ActionPreview p,string reason,int grainDelta=0,int strawDelta=0,int fuelDelta=0,int walletDelta=0)
        {
            events.Add(new GameEvent{Sequence=events.Count+1,ActionIndex=state.ActionIndex,RoundIndex=state.RoundIndex,Kind=kind,X=state.Vehicle.X,Y=state.Vehicle.Y,Amount=amount,
                ActionCost=p.ActionCost,FuelCost=p.FuelCost,GrainDelta=grainDelta,StrawDelta=strawDelta,FuelDelta=fuelDelta,WalletDelta=walletDelta,Reason=reason,Cells=p.TargetCells.Select(c=>c.Copy()).ToArray()});
        }
        private CommandResult Reject(string reason) { return new CommandResult{Success=false,Reason=reason,Events=new GameEvent[0]}; }
        public RoundSnapshot ExportSnapshot()
        {
            var copy=(RoundSnapshot)state.MemberCopy();copy.Vehicle=state.Vehicle.Copy();copy.Tiles=state.Tiles.Select(t=>t.Copy()).ToArray();copy.Rounds=state.Rounds.Select(r=>r.Copy()).ToArray();
            copy.Commands=commands.Select(c=>c.Copy()).ToArray();copy.Events=events.Select(e=>e.Copy()).ToArray();return copy;
        }
        public static bool ValidateSnapshot(RoundSnapshot snapshot,out string reason) { Session ignored;return TryRestore(snapshot,out ignored,out reason); }
        public static bool TryRestore(RoundSnapshot snapshot,out Session restored,out string reason)
        {
            restored=null;reason="快照结构、版本或资源范围非法";if(!BasicValid(snapshot))return false;
            var replay=new Session(snapshot.InitialModule);
            foreach(CommandRecord c in snapshot.Commands)
            {
                if(c==null||!Enum.IsDefined(typeof(CommandKind),c.Kind)||string.IsNullOrWhiteSpace(c.Id)||c.Id.Length>128||!RecordArgumentsValid(c)){reason="无效命令记录";return false;}
                CommandResult result=replay.Execute(c.Kind,c.A,c.B,c.Id);
                if(!result.Success||result.AlreadyApplied){reason="生产命令重放失败："+result.Reason;return false;}
            }
            if(Canonical(snapshot)!=Canonical(replay.ExportSnapshot())){reason="快照与完整生产命令不一致，拒绝免费粮秸、燃料、行动、现金或轮进度";return false;}
            replay.last=new GameEvent[0];restored=replay;reason="有效完整行动检查点；恢复不重收、重复交付或追加预算";return true;
        }
        private static bool RecordArgumentsValid(CommandRecord c)
        {
            if(c.Kind==CommandKind.Pave)return Rules.InBounds(c.A,c.B);
            if(c.B!=0)return false;
            switch(c.Kind)
            {
                case CommandKind.Turn:return c.A==1||c.A==-1;
                case CommandKind.Deliver:case CommandKind.UnloadStraw:return c.A>0&&c.A<=Rules.TotalCrops;
                case CommandKind.ContinueRound:return Enum.IsDefined(typeof(ModuleKind),(ModuleKind)c.A);
                default:return c.A==0;
            }
        }
        private static bool BasicValid(RoundSnapshot s)
        {
            if(s==null||s.SchemaVersion!=Rules.SchemaVersion||s.RulesVersion!=Rules.Version||!Enum.IsDefined(typeof(ModuleKind),s.InitialModule)||!Enum.IsDefined(typeof(Phase),s.Phase)||!Enum.IsDefined(typeof(RoundOutcome),s.Outcome)
                ||s.RoundIndex<1||s.RoundIndex>Rules.RoundCount||s.ActionsRemaining<0||s.ActionsRemaining>Rules.RoundBudget(s.RoundIndex)||s.DeliveredThisRound<0||s.DeliveredThisRound>Rules.RoundGoal(s.RoundIndex)||s.DeliveredTotal<0||s.DeliveredTotal>Rules.TotalCrops
                ||s.StoredStraw<0||s.StoredStraw>Rules.TotalCrops||s.Wallet<0||s.Wallet>Rules.InitialWallet+Rules.TotalCrops*Rules.GrainPrice||s.HarvestedCrops<0||s.HarvestedCrops>Rules.TotalCrops||s.ConvertedStraw<0||s.PavedStraw<0||s.ConvertedStraw+s.PavedStraw>Rules.TotalCrops
                ||s.FuelSpent<0||s.FuelSpent>432||s.FuelAdded<0||s.FuelAdded>2592||s.ActionIndex<0||s.ActionIndex>Rules.MaximumCommands
                ||s.Vehicle==null||!Rules.InBounds(s.Vehicle.X,s.Vehicle.Y)||!Enum.IsDefined(typeof(Direction),s.Vehicle.Facing)||!Enum.IsDefined(typeof(ModuleKind),s.Vehicle.Module)||s.Vehicle.Fuel<0||s.Vehicle.Fuel>Rules.MaxFuel||s.Vehicle.Grain<0||s.Vehicle.Straw<0||s.Vehicle.CargoUsed>s.Vehicle.Capacity
                ||s.Tiles==null||s.Tiles.Length!=Rules.Width*Rules.Height||s.Rounds==null||s.Rounds.Length!=Rules.RoundCount||s.Commands==null||s.Commands.Length>Rules.MaximumCommands||s.Events==null||s.Events.Length>Rules.MaximumEvents)return false;
            for(int i=0;i<s.Tiles.Length;i++)
            {
                TileState t=s.Tiles[i];if(t==null||t.Id!=i||t.X!=i%Rules.Width||t.Y!=i/Rules.Width||t.InitialKind!=Rules.InitialTileKind(t.X,t.Y)||!Enum.IsDefined(typeof(TileKind),t.Kind))return false;
            }
            for(int i=0;i<s.Rounds.Length;i++)
            {
                RoundState r=s.Rounds[i];if(r==null||r.Number!=i+1||r.Goal!=Rules.RoundGoal(i+1)||r.Budget!=Rules.RoundBudget(i+1)||r.Delivered<0||r.Delivered>r.Goal||r.ActionsSpent<0||r.ActionsSpent>r.Budget||!Enum.IsDefined(typeof(ModuleKind),r.Module))return false;
            }
            foreach(GameEvent e in s.Events)if(e==null||e.Reason==null||!Enum.IsDefined(typeof(EventKind),e.Kind)||e.Cells==null||e.Cells.Length>3||e.Cells.Any(c=>c==null))return false;
            if(s.Vehicle.Grain+s.DeliveredTotal!=s.HarvestedCrops||s.Vehicle.Straw+s.StoredStraw+s.ConvertedStraw+s.PavedStraw!=s.HarvestedCrops||Rules.InitialFuel+s.FuelAdded-s.FuelSpent!=s.Vehicle.Fuel)return false;
            return true;
        }
        private static string Canonical(object value)
        {
            if(value==null)return "null;";Type type=value.GetType();
            if(type.IsEnum||type.IsPrimitive||value is string){string text=System.Convert.ToString(value,CultureInfo.InvariantCulture);return text.Length+":"+text+";";}
            var array=value as Array;var result=new StringBuilder();
            if(array!=null){result.Append('[').Append(array.Length).Append(':');foreach(object item in array)result.Append(Canonical(item));return result.Append(']').ToString();}
            foreach(FieldInfo field in type.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))result.Append(field.Name).Append('=').Append(Canonical(field.GetValue(value)));
            return result.ToString();
        }
    }
}
