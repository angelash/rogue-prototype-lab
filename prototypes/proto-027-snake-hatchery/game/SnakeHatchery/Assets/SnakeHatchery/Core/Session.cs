using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SnakeHatchery
{
    public sealed class Session
    {
        private readonly RoundSnapshot state;
        private readonly List<CommandRecord> commands=new List<CommandRecord>();
        private readonly List<GameEvent> events=new List<GameEvent>();
        private readonly HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
        private GameEvent[] last=new GameEvent[0];
        private int actionIndex;
        public RoundSnapshot State { get { return ExportSnapshot(); } }
        public GameEvent[] LastEvents { get { return last.Select(e=>e.Copy()).ToArray(); } }
        private Session(PresetKind preset)
        {
            var kinds=new[]{SegmentKind.Basic,SegmentKind.Basic,SegmentKind.Collector,SegmentKind.Warehouse,SegmentKind.Warehouse,SegmentKind.Collector,SegmentKind.Warehouse};
            var positions=new[]{new GridPoint(1,1),new GridPoint(1,0),new GridPoint(0,0),new GridPoint(0,1),new GridPoint(0,2)};
            int length=preset==PresetKind.Short?3:5;
            state=new RoundSnapshot{SchemaVersion=Rules.SchemaVersion,RulesVersion=Rules.Version,Preset=preset,Phase=Phase.Active,Outcome=RoundOutcome.None,
                ActionsRemaining=Rules.ActionBudget,DeliveryGoal=Rules.DeliveryGoal,NextCloneId=1,Cells=Rules.CreateCells(),Clones=new SnakeState[0],
                InventoryModuleIds=preset==PresetKind.Short?new[]{4,5}:new int[0],Modules=new ModuleState[Rules.ModuleCount]};
            state.Main=new SnakeState{Id=0,IsMain=true,Direction=Direction.Up,Segments=Enumerable.Range(0,length).Select(i=>new SegmentState{ModuleId=i+1,Kind=kinds[i],X=positions[i].X,Y=positions[i].Y}).ToArray(),Route=new GridPoint[0],WaitingReason=""};
            for(int i=0;i<Rules.ModuleCount;i++)state.Modules[i]=new ModuleState{Id=i+1,Kind=kinds[i],Location=ModuleLocation.Ground,OwnerId=-1,X=i==5?0:7,Y=4};
            SyncOwnership();
        }
        public static Session CreatePrototype(PresetKind preset=PresetKind.Short)
        {
            if(!Enum.IsDefined(typeof(PresetKind),preset))throw new ArgumentOutOfRangeException("preset");
            return new Session(preset);
        }
        public Session Retry() { return new Session(state.Preset); }
        public CommandResult Move(Direction direction,string commandId=null) { return Execute(CommandKind.Move,(int)direction,0,null,commandId); }
        public CommandResult Wait(string commandId=null) { return Execute(CommandKind.Wait,0,0,null,commandId); }
        public CommandResult Collect(string commandId=null) { return Execute(CommandKind.Collect,0,0,null,commandId); }
        public CommandResult Deliver(string commandId=null) { return Execute(CommandKind.Deliver,0,0,null,commandId); }
        public CommandResult Split(int keepSegments,CloneBehavior behavior,GridPoint[] route,string commandId=null) { return Execute(CommandKind.Split,keepSegments,(int)behavior,route,commandId); }
        public CommandResult Reclaim(int cloneId,string commandId=null) { return Execute(CommandKind.Reclaim,cloneId,0,null,commandId); }
        public CommandResult CollectModule(int moduleId,string commandId=null) { return Execute(CommandKind.CollectModule,moduleId,0,null,commandId); }
        public CommandResult DropModule(int moduleId,string commandId=null) { return Execute(CommandKind.DropModule,moduleId,0,null,commandId); }

        public MovePreview PreviewMove(Direction direction)
        {
            var p=new MovePreview{Cost=Rules.ActionCost,Reason="本轮已结束"};
            if(!Enum.IsDefined(typeof(Direction),direction)){p.Reason="未知方向";return p;}
            GridPoint d=Rules.Delta(direction);p.TargetX=state.Main.HeadX+d.X;p.TargetY=state.Main.HeadY+d.Y;
            p.WillGrow=GroundModule(p.TargetX,p.TargetY)!=null;
            if(state.Phase!=Phase.Active)return p;
            string collision=MovementBlock(state.Main,p.TargetX,p.TargetY,p.WillGrow);
            p.Legal=collision==null;p.Reason=collision??(p.WillGrow?"拾取最低编号地面模块，旧尾格本步不腾位":"尾部先腾位，再跟随移动");return p;
        }
        public SplitPreview PreviewSplit(int keepSegments,CloneBehavior behavior,GridPoint[] route)
        {
            var p=new SplitPreview{Cost=Rules.ActionCost,Reason="本轮已结束"};
            if(state.Phase!=Phase.Active)return p;
            if(state.Clones.Length>=Rules.MaxClones){p.Reason="最多两条分身，先回收；拒绝不耗行动";return p;}
            if(keepSegments<Rules.MinMainSegments||keepSegments>=state.Main.Length){p.Reason="主蛇至少保留头及一节，尾段至少一节";return p;}
            if(!Enum.IsDefined(typeof(CloneBehavior),behavior)){p.Reason="未知分身行为";return p;}
            SegmentState[] kept=state.Main.Segments.Take(keepSegments).ToArray(),cut=state.Main.Segments.Skip(keepSegments).ToArray();
            string routeError=RouteError(route,cut[0].X,cut[0].Y,behavior);
            if(routeError!=null){p.Reason=routeError;return p;}
            p.MainLengthAfter=kept.Length;p.CloneLength=cut.Length;
            p.MainCapacityAfter=Rules.BaseCapacity+kept.Count(s=>s.Kind==SegmentKind.Warehouse)*Rules.WarehouseCapacity;
            p.CloneCapacity=Rules.BaseCapacity+cut.Count(s=>s.Kind==SegmentKind.Warehouse)*Rules.WarehouseCapacity;
            p.LostCapacity=state.Main.Capacity-p.MainCapacityAfter;p.LostCollectionRate=cut.Count(s=>s.Kind==SegmentKind.Collector)*Rules.CollectorRate;
            int overflow=Math.Max(0,state.Main.Cargo-p.MainCapacityAfter);
            p.TransferredCargo=Math.Min(overflow,p.CloneCapacity);p.DroppedCargo=overflow-p.TransferredCargo;p.Legal=true;
            p.Reason="转移实际尾段；货物先保留主蛇合法容量，再转给分身，余量掉在新分身头格；新分身下一行动才行动";
            if(route.Any(point=>state.Cells[Rules.CellId(point.X,point.Y)].Blocked))p.Reason+="；路线含公开墙格，分身会在其前等待";
            return p;
        }
        private string RouteError(GridPoint[] route,int x,int y,CloneBehavior behavior)
        {
            if(route==null||route.Length<2||route.Length>Rules.MaximumRouteLength)return "路线需2–32个公开格；不提供自动寻路";
            if(route[0]==null||route[0].X!=x||route[0].Y!=y)return "路线首格必须是切下尾段第一格";
            foreach(GridPoint point in route)if(point==null||!Rules.InBounds(point.X,point.Y))return "路线含越界格";
            for(int i=1;i<route.Length;i++)if(Distance(route[i].X,route[i].Y,route[i-1].X,route[i-1].Y)!=1)return "路线每步只能正交相邻，不能跳格";
            if(behavior==CloneBehavior.Shuttle&&(route[route.Length-1].X!=x||route[route.Length-1].Y!=y||!route.Any(p=>p.X==Rules.DeliveryX&&p.Y==Rules.DeliveryY)||!route.Any(p=>state.Cells[Rules.CellId(p.X,p.Y)].InitialCargo>0)))return "往返搬运需公开闭合路线，经过交付点和采集点";
            return null;
        }
        public ReclaimPreview PreviewReclaim(int id)
        {
            var p=new ReclaimPreview{CloneId=id,Cost=Rules.ActionCost,Reason="本轮已结束"};
            if(state.Phase!=Phase.Active)return p;
            SnakeState clone=state.Clones.FirstOrDefault(c=>c.Id==id);
            if(clone==null){p.Reason="分身不存在或已回收";return p;}
            p.DropX=clone.HeadX;p.DropY=clone.HeadY;
            if(Distance(state.Main.HeadX,state.Main.HeadY,clone.HeadX,clone.HeadY)>1){p.Reason="回收需主蛇头与分身头相邻，消耗1全局行动";return p;}
            p.TransferredCargo=Math.Min(clone.Cargo,state.Main.Capacity-state.Main.Cargo);p.DroppedCargo=clone.Cargo-p.TransferredCargo;
            p.ModulesToInventory=Math.Min(clone.Length,Rules.ModuleInventoryCapacity-state.InventoryModuleIds.Length);p.DroppedModules=clone.Length-p.ModulesToInventory;
            p.Legal=true;p.Reason="回收耗1行动；节按原身体顺序进入模块库存，满则各节落在原格；货物优先主蛇余容量，余量落在分身头格；不接长主蛇";return p;
        }
        private CommandResult Execute(CommandKind kind,int a,int b,GridPoint[] route,string id)
        {
            last=new GameEvent[0];
            if(id!=null&&ids.Contains(id))return new CommandResult{Success=true,AlreadyApplied=true,Reason="命令已提交，不重复执行",Events=last};
            if(state.Phase!=Phase.Active)return Reject("本轮已结束，重试开始独立新轮");
            if(id!=null&&(string.IsNullOrWhiteSpace(id)||id.Length>128))return Reject("命令身份非法");
            actionIndex=Rules.ActionBudget-state.ActionsRemaining+1;
            int before=events.Count;string error;
            switch(kind)
            {
                case CommandKind.Move:error=DoMove(a);break;
                case CommandKind.Wait:Emit(EventKind.Waited,state.Main,1,"主动等待一个全局行动");error=null;break;
                case CommandKind.Collect:error=DoCollect();break;
                case CommandKind.Deliver:error=DoDeliver();break;
                case CommandKind.Split:error=DoSplit(a,b,route);break;
                case CommandKind.Reclaim:error=DoReclaim(a);break;
                case CommandKind.CollectModule:error=DoCollectModule(a);break;
                case CommandKind.DropModule:error=DoDropModule(a);break;
                default:error="未知命令";break;
            }
            if(error!=null)return Reject(error);
            state.ActionsRemaining-=Rules.ActionCost;
            if(state.Phase==Phase.Active)AdvanceClones();
            if(state.Phase==Phase.Active&&(state.Delivered>=state.DeliveryGoal||state.ActionsRemaining==0))
            {
                state.Phase=Phase.Finished;state.Outcome=state.Delivered>=state.DeliveryGoal?RoundOutcome.Completed:RoundOutcome.Failed;
                Emit(EventKind.RoundFinished,state.Main,state.Delivered,state.Outcome==RoundOutcome.Completed?"目标交付完成；先检查完成再检查行动截止":"行动已用尽，目标尚未交付完成");
            }
            SyncOwnership();
            id=id??"c"+(commands.Count+1).ToString(CultureInfo.InvariantCulture);
            // Automatically generated identities must not collide with caller-supplied identities.
            while(ids.Contains(id))id="_"+id;
            commands.Add(new CommandRecord{Id=id,Kind=kind,A=a,B=b,Route=route==null?new GridPoint[0]:route.Select(p=>p.Copy()).ToArray()});ids.Add(id);
            last=events.Skip(before).Select(e=>e.Copy()).ToArray();
            return new CommandResult{Success=true,Reason=last.Length==0?"动作已提交":last[last.Length-1].Reason,Events=LastEvents};
        }
        private CommandResult Reject(string reason) { return new CommandResult{Success=false,Reason=reason,Events=new GameEvent[0]}; }
        private string DoMove(int value)
        {
            if(!Enum.IsDefined(typeof(Direction),value))return "未知方向";
            Direction direction=(Direction)value;MovePreview preview=PreviewMove(direction);
            if(!preview.Legal)
            {
                state.Phase=Phase.Finished;state.Outcome=RoundOutcome.Failed;
                EmitAt(EventKind.Collision,0,preview.TargetX,preview.TargetY,1,"确认移动发生碰撞："+preview.Reason);
                Emit(EventKind.RoundFinished,state.Main,state.Delivered,"占位碰撞导致本轮失败，已提交行动需保存");return null;
            }
            ModuleState growth=GroundModule(preview.TargetX,preview.TargetY);
            ShiftBody(state.Main,preview.TargetX,preview.TargetY,growth);
            state.Main.Direction=direction;
            Emit(EventKind.Moved,state.Main,1,"主蛇移动；位置与完整身体同步");
            if(growth!=null)Emit(EventKind.Grew,state.Main,growth.Id,"模块"+growth.Id+"追加真实尾格，获得"+Rules.KindName(growth.Kind));
            return null;
        }
        private string DoCollect()
        {
            CellState cell=state.Cells[Rules.CellId(state.Main.HeadX,state.Main.HeadY)];
            if(cell.Cargo==0)return "本格没有货物";
            if(state.Main.Cargo>=state.Main.Capacity)return "主蛇货物容量已满，地面货物保留";
            CollectCargo(state.Main);return null;
        }
        private string DoDeliver()
        {
            if(state.Main.HeadX!=Rules.DeliveryX||state.Main.HeadY!=Rules.DeliveryY)return "只在公开交付点交付";
            if(state.Main.Cargo==0)return "没有可交付货物";
            DeliverCargo(state.Main);return null;
        }
        private string DoSplit(int keep,int behavior,GridPoint[] route)
        {
            SplitPreview preview=PreviewSplit(keep,(CloneBehavior)behavior,route);if(!preview.Legal)return preview.Reason;
            SegmentState[] cut=state.Main.Segments.Skip(keep).Select(s=>s.Copy()).ToArray();
            var clone=new SnakeState{Id=state.NextCloneId++,IsMain=false,Segments=cut,Behavior=(CloneBehavior)behavior,Route=route.Select(p=>p.Copy()).ToArray(),BornAction=actionIndex,WaitingReason="新生，本行动不移动",Direction=state.Main.Direction,Cargo=preview.TransferredCargo};
            state.Main.Segments=state.Main.Segments.Take(keep).Select(s=>s.Copy()).ToArray();
            state.Main.Cargo-=preview.TransferredCargo+preview.DroppedCargo;
            state.Clones=state.Clones.Concat(new[]{clone}).ToArray();
            Emit(EventKind.Split,clone,cut.Length,"从主蛇移走实际尾段；主蛇能力立即重算；新分身下一行动才行动");
            if(preview.DroppedCargo>0)DropCargo(clone.HeadX,clone.HeadY,preview.DroppedCargo,clone.Id);
            return null;
        }
        private string DoReclaim(int id)
        {
            ReclaimPreview preview=PreviewReclaim(id);if(!preview.Legal)return preview.Reason;
            SnakeState clone=state.Clones.First(c=>c.Id==id);
            state.Main.Cargo+=preview.TransferredCargo;
            if(preview.DroppedCargo>0)DropCargo(clone.HeadX,clone.HeadY,preview.DroppedCargo,clone.Id);
            var inventory=state.InventoryModuleIds.ToList();
            foreach(SegmentState segment in clone.Segments)
            {
                ModuleState module=state.Modules[segment.ModuleId-1];
                if(inventory.Count<Rules.ModuleInventoryCapacity)inventory.Add(module.Id);
                else { module.Location=ModuleLocation.Ground;module.OwnerId=-1;module.X=segment.X;module.Y=segment.Y;EmitAt(EventKind.ModuleDropped,clone.Id,module.X,module.Y,module.Id,"库存模块满；实际模块留在原分身节格"); }
            }
            state.InventoryModuleIds=inventory.OrderBy(i=>i).ToArray();state.Clones=state.Clones.Where(c=>c.Id!=id).ToArray();
            EmitAt(EventKind.Reclaimed,id,clone.HeadX,clone.HeadY,clone.Length,"回收不接长主蛇；实际节进模块库存或原地可见，货物转移或掉落");return null;
        }
        private string DoCollectModule(int id)
        {
            ModuleState module=state.Modules.FirstOrDefault(m=>m.Id==id&&m.Location==ModuleLocation.Ground);
            if(module==null)return "地面模块不存在";
            if(Distance(state.Main.HeadX,state.Main.HeadY,module.X,module.Y)>1)return "拾取模块需头相邻或同格";
            if(state.InventoryModuleIds.Length>=Rules.ModuleInventoryCapacity)return "模块库存已满，地面模块保留";
            state.InventoryModuleIds=state.InventoryModuleIds.Concat(new[]{id}).OrderBy(i=>i).ToArray();
            EmitAt(EventKind.ModuleCollected,0,module.X,module.Y,id,"模块进入库存，不免费增加主蛇能力");return null;
        }
        private string DoDropModule(int id)
        {
            if(!state.InventoryModuleIds.Contains(id))return "模块不在主蛇库存";
            state.InventoryModuleIds=state.InventoryModuleIds.Where(i=>i!=id).ToArray();
            ModuleState module=state.Modules[id-1];module.Location=ModuleLocation.Ground;module.OwnerId=-1;module.X=state.Main.HeadX;module.Y=state.Main.HeadY;
            Emit(EventKind.ModuleDropped,state.Main,id,"实际库存模块放在主蛇头格；不会当步自动吞入");return null;
        }
        private void AdvanceClones()
        {
            foreach(SnakeState clone in state.Clones.OrderBy(c=>c.Id))
            {
                if(clone.BornAction>=actionIndex||clone.TaskComplete)continue;
                int next=clone.RouteIndex+1;
                if(next>=clone.Route.Length){clone.TaskComplete=true;clone.WaitingReason="有限巡线任务结束，等待回收";Emit(EventKind.TaskCompleted,clone,0,clone.WaitingReason);continue;}
                GridPoint point=clone.Route[next];string block=MovementBlock(clone,point.X,point.Y,false);
                if(block!=null){clone.WaitingReason=block;EmitAt(EventKind.CloneWaited,clone.Id,point.X,point.Y,0,"公开下一格受阻，稳定编号等待："+block);continue;}
                int dx=point.X-clone.HeadX,dy=point.Y-clone.HeadY;
                ShiftBody(clone,point.X,point.Y,null);clone.Direction=dx>0?Direction.Right:dx<0?Direction.Left:dy>0?Direction.Up:Direction.Down;
                clone.RouteIndex=next;clone.WaitingReason="";Emit(EventKind.CloneMoved,clone,1,"按公开路线移动，不改道，不吞入地面模块");
                CollectCargo(clone);
                if(clone.Behavior==CloneBehavior.Shuttle&&clone.HeadX==Rules.DeliveryX&&clone.HeadY==Rules.DeliveryY&&clone.Cargo>0)DeliverCargo(clone);
                if(next==clone.Route.Length-1)
                {
                    if(clone.Behavior==CloneBehavior.Shuttle)clone.RouteIndex=0;
                    else {clone.TaskComplete=true;clone.WaitingReason="有限巡线任务结束，等待回收";Emit(EventKind.TaskCompleted,clone,0,clone.WaitingReason);}
                }
            }
        }
        private string MovementBlock(SnakeState snake,int x,int y,bool growth)
        {
            if(!Rules.InBounds(x,y))return "地图边界";
            if(state.Cells[Rules.CellId(x,y)].Blocked)return "墙格";
            if(snake.IsMain&&snake.Length>1&&snake.Segments[1].X==x&&snake.Segments[1].Y==y)return "禁止主蛇直接反向穿过前一节";
            foreach(SnakeState other in new[]{state.Main}.Concat(state.Clones))
            {
                for(int i=0;i<other.Length;i++)
                {
                    if(other.Segments[i].X!=x||other.Segments[i].Y!=y)continue;
                    if(other.Id==snake.Id&&i==other.Length-1&&!growth)continue;
                    return other.Id==snake.Id?(growth&&i==other.Length-1?"成长本步旧尾格不腾位":"自身身体占位"):"实体"+other.Id+"占位";
                }
            }
            return null;
        }
        private void ShiftBody(SnakeState snake,int x,int y,ModuleState growth)
        {
            SegmentState[] old=snake.Segments;var result=new SegmentState[old.Length+(growth==null?0:1)];
            for(int i=0;i<old.Length;i++){result[i]=old[i].Copy();result[i].X=i==0?x:old[i-1].X;result[i].Y=i==0?y:old[i-1].Y;}
            if(growth!=null)result[old.Length]=new SegmentState{ModuleId=growth.Id,Kind=growth.Kind,X=old[old.Length-1].X,Y=old[old.Length-1].Y};
            snake.Segments=result;
        }
        private ModuleState GroundModule(int x,int y) { return state.Modules.Where(m=>m.Location==ModuleLocation.Ground&&m.X==x&&m.Y==y).OrderBy(m=>m.Id).FirstOrDefault(); }
        private void CollectCargo(SnakeState snake)
        {
            CellState cell=state.Cells[Rules.CellId(snake.HeadX,snake.HeadY)];int amount=Math.Min(cell.Cargo,Math.Min(snake.CollectionRate,snake.Capacity-snake.Cargo));
            if(amount==0)return;cell.Cargo-=amount;snake.Cargo+=amount;Emit(EventKind.Collected,snake,amount,"采集实际货物；容量外货物留在原格");
        }
        private void DeliverCargo(SnakeState snake) { int amount=snake.Cargo;snake.Cargo=0;state.Delivered+=amount;Emit(EventKind.Delivered,snake,amount,"货物唯一转入已交付，不发额外模块或行动"); }
        private void DropCargo(int x,int y,int amount,int owner) { state.Cells[Rules.CellId(x,y)].Cargo+=amount;EmitAt(EventKind.CargoDropped,owner,x,y,amount,"容量溢出货物留在公开格"); }
        private static int Distance(int ax,int ay,int bx,int by) { return Math.Abs(ax-bx)+Math.Abs(ay-by); }
        private void SyncOwnership()
        {
            foreach(SnakeState snake in new[]{state.Main}.Concat(state.Clones))foreach(SegmentState segment in snake.Segments)
            {ModuleState m=state.Modules[segment.ModuleId-1];m.Location=snake.IsMain?ModuleLocation.Main:ModuleLocation.Clone;m.OwnerId=snake.Id;m.X=segment.X;m.Y=segment.Y;}
            foreach(int id in state.InventoryModuleIds){ModuleState m=state.Modules[id-1];m.Location=ModuleLocation.Inventory;m.OwnerId=0;m.X=m.Y=-1;}
        }
        private void Emit(EventKind kind,SnakeState snake,int amount,string reason) { EmitAt(kind,snake.Id,snake.HeadX,snake.HeadY,amount,reason); }
        private void EmitAt(EventKind kind,int entity,int x,int y,int amount,string reason) { events.Add(new GameEvent{Sequence=events.Count+1,ActionIndex=actionIndex,EntityId=entity,Kind=kind,X=x,Y=y,Amount=amount,Reason=reason}); }

        public RoundSnapshot ExportSnapshot()
        {
            return new RoundSnapshot{SchemaVersion=state.SchemaVersion,RulesVersion=state.RulesVersion,Preset=state.Preset,Phase=state.Phase,Outcome=state.Outcome,
                ActionsRemaining=state.ActionsRemaining,Delivered=state.Delivered,DeliveryGoal=state.DeliveryGoal,NextCloneId=state.NextCloneId,
                Main=state.Main.Copy(),Clones=state.Clones.Select(c=>c.Copy()).ToArray(),Modules=state.Modules.Select(m=>m.Copy()).ToArray(),
                InventoryModuleIds=(int[])state.InventoryModuleIds.Clone(),Cells=state.Cells.Select(c=>c.Copy()).ToArray(),Commands=commands.Select(c=>c.Copy()).ToArray(),Events=events.Select(e=>e.Copy()).ToArray()};
        }
        public static bool ValidateSnapshot(RoundSnapshot snapshot,out string reason) { Session ignored;return TryRestore(snapshot,out ignored,out reason); }
        public static bool TryRestore(RoundSnapshot snapshot,out Session restored,out string reason)
        {
            restored=null;reason="快照结构、范围或版本非法";
            if(!BasicValid(snapshot))return false;
            var replay=new Session(snapshot.Preset);
            foreach(CommandRecord command in snapshot.Commands)
            {
                if(command==null||!Enum.IsDefined(typeof(CommandKind),command.Kind)||string.IsNullOrWhiteSpace(command.Id)||command.Id.Length>128||command.Route==null||command.Route.Length>Rules.MaximumRouteLength||command.Route.Any(p=>p==null)||(command.Kind!=CommandKind.Split&&command.Route.Length!=0)){reason="无效命令记录";return false;}
                CommandResult result=replay.Execute(command.Kind,command.A,command.B,command.Route,command.Id);
                if(!result.Success||result.AlreadyApplied){reason="生产命令重放失败："+result.Reason;return false;}
            }
            if(Canonical(snapshot)!=Canonical(replay.ExportSnapshot())){reason="快照与生产命令不一致，拒绝免费模块、货物或行动";return false;}
            replay.last=new GameEvent[0];restored=replay;reason="有效完整动作检查点；恢复不执行额外动作";return true;
        }
        private static bool BasicValid(RoundSnapshot s)
        {
            if(s==null||s.SchemaVersion!=Rules.SchemaVersion||s.RulesVersion!=Rules.Version||!Enum.IsDefined(typeof(PresetKind),s.Preset)
                ||!Enum.IsDefined(typeof(Phase),s.Phase)||!Enum.IsDefined(typeof(RoundOutcome),s.Outcome)||s.ActionsRemaining<0||s.ActionsRemaining>Rules.ActionBudget
                ||s.Delivered<0||s.Delivered>Rules.DeliveryGoal||s.DeliveryGoal!=Rules.DeliveryGoal||s.NextCloneId<1||s.NextCloneId>Rules.ActionBudget+1
                ||s.Modules==null||s.Modules.Length!=Rules.ModuleCount||s.Cells==null||s.Cells.Length!=Rules.Width*Rules.Height||s.Clones==null||s.Clones.Length>Rules.MaxClones
                ||s.InventoryModuleIds==null||s.InventoryModuleIds.Length>Rules.ModuleInventoryCapacity||s.Commands==null||s.Commands.Length>Rules.MaximumCommands||s.Events==null||s.Events.Length>2000)return false;
            var snakes=new[]{s.Main}.Concat(s.Clones).ToArray();var moduleIds=new List<int>();var occupied=new HashSet<int>();var entityIds=new HashSet<int>();
            foreach(SnakeState snake in snakes)
            {
                if(snake==null||snake.Id<0||snake.Id>=s.NextCloneId||!entityIds.Add(snake.Id)||snake.Segments==null||snake.Length<1||snake.Length>Rules.ModuleCount||snake.Route==null||snake.Route.Length>Rules.MaximumRouteLength||snake.Route.Any(p=>p==null)||snake.WaitingReason==null||!Enum.IsDefined(typeof(Direction),snake.Direction)||!Enum.IsDefined(typeof(CloneBehavior),snake.Behavior)||snake.Cargo<0)return false;
                foreach(SegmentState segment in snake.Segments)
                {
                    if(segment==null||segment.ModuleId<1||segment.ModuleId>Rules.ModuleCount||!Rules.InBounds(segment.X,segment.Y)||!Enum.IsDefined(typeof(SegmentKind),segment.Kind)||!occupied.Add(Rules.CellId(segment.X,segment.Y)))return false;
                    moduleIds.Add(segment.ModuleId);
                }
                if(snake.Cargo>snake.Capacity)return false;
            }
            if(s.Main.Id!=0||!s.Main.IsMain||s.Main.Length<Rules.MinMainSegments||s.Clones.Any(c=>c.Id==0||c.IsMain))return false;
            foreach(ModuleState m in s.Modules)
            {
                if(m==null||m.Id<1||m.Id>Rules.ModuleCount||!Enum.IsDefined(typeof(SegmentKind),m.Kind)||!Enum.IsDefined(typeof(ModuleLocation),m.Location))return false;
                if(m.Location==ModuleLocation.Ground){if(!Rules.InBounds(m.X,m.Y))return false;moduleIds.Add(m.Id);}
            }
            moduleIds.AddRange(s.InventoryModuleIds);
            if(moduleIds.Count!=Rules.ModuleCount||moduleIds.Distinct().Count()!=Rules.ModuleCount||moduleIds.Any(id=>id<1||id>Rules.ModuleCount))return false;
            long cargo=s.Delivered+snakes.Sum(c=>c.Cargo);
            for(int i=0;i<s.Cells.Length;i++){CellState c=s.Cells[i];if(c==null||c.Id!=i||c.X!=i%Rules.Width||c.Y!=i/Rules.Width||c.Cargo<0||c.Cargo>Rules.DeliveryGoal||c.InitialCargo<0||c.InitialCargo>4||(c.Blocked&&occupied.Contains(i)))return false;cargo+=c.Cargo;}
            if(cargo!=Rules.DeliveryGoal)return false;
            foreach(GameEvent e in s.Events)if(e==null||e.Reason==null||!Enum.IsDefined(typeof(EventKind),e.Kind))return false;
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
