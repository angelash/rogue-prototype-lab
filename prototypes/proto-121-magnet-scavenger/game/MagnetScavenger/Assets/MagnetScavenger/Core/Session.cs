using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MagnetScavenger
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
        private Session(OrderKind order)
        {
            state=new RoundSnapshot{SchemaVersion=Rules.SchemaVersion,RulesVersion=Rules.Version,OrderKind=order,Order=Rules.CreateOrder(order),
                Phase=Phase.Active,Outcome=RoundOutcome.None,RootX=Rules.InitialRootX,RootY=Rules.InitialRootY,Facing=Direction.Right,
                Energy=Rules.InitialEnergy,Wallet=Rules.InitialWallet,ChainIds=new int[0],StorageIds=new int[0],SoldIds=new int[0],Items=Rules.CreateItems()};
            Rebuild();
        }
        public static Session CreatePrototype(OrderKind order=OrderKind.Parts)
        {
            if(!Enum.IsDefined(typeof(OrderKind),order))throw new ArgumentOutOfRangeException("order");return new Session(order);
        }
        public Session Retry() { return new Session(state.OrderKind); }
        public CommandResult Move(Direction direction,string commandId=null) { return Execute(CommandKind.Move,(int)direction,commandId); }
        public CommandResult Rotate(int quarterTurns,string commandId=null) { return Execute(CommandKind.Rotate,quarterTurns,commandId); }
        public CommandResult Attract(string commandId=null) { return Execute(CommandKind.Attract,0,commandId); }
        public CommandResult Retain(string commandId=null) { return Execute(CommandKind.Retain,0,commandId); }
        public CommandResult Sell(int itemId,string commandId=null) { return Execute(CommandKind.Sell,itemId,commandId); }
        public CommandResult Detach(int chainIndex,string commandId=null) { return Execute(CommandKind.Detach,chainIndex,commandId); }
        public CommandResult Attach(int itemId,string commandId=null) { return Execute(CommandKind.Attach,itemId,commandId); }

        private sealed class Projection
        {
            public GridPoint[] Cells;
            public ItemState[] Items;
            public int EndX,EndY,Weight;
            public Direction EndDirection;
        }
        private Projection Project(int rootX,int rootY,Direction facing,int[] chain,int pending)
        {
            var occupied=new List<GridPoint>{new GridPoint(rootX,rootY)};var items=new List<ItemState>();
            int x=rootX,y=rootY,weight=0;Direction direction=facing;
            foreach(int id in chain)
            {
                ItemState item=state.Items[id-1].Copy();GridPoint delta=Rules.Delta(direction);Rules.SetPose(item,x+delta.X,y+delta.Y,direction);
                occupied.AddRange(item.Cells);items.Add(item);weight+=item.Weight;x=item.ExitX;y=item.ExitY;direction=item.ExitDirection;
            }
            var p=new Projection{EndX=x,EndY=y,EndDirection=direction};
            if(pending>0)
            {
                ItemState item=state.Items[pending-1].Copy();GridPoint delta=Rules.Delta(direction);Rules.SetPose(item,x+delta.X,y+delta.Y,direction);
                occupied.AddRange(item.Cells);items.Add(item);weight+=item.Weight;
            }
            p.Items=items.ToArray();p.Cells=occupied.ToArray();p.Weight=weight;return p;
        }
        private string GeometryError(Projection p)
        {
            if(p.Weight>Rules.MaxLoad)return "总重量"+p.Weight+"超过承重"+Rules.MaxLoad;
            var occupied=new HashSet<int>();var held=new HashSet<int>(p.Items.Select(i=>i.Id));
            foreach(GridPoint cell in p.Cells)
            {
                if(!Rules.InBounds(cell.X,cell.Y))return "完整结构超出地图边界";
                if(Rules.IsBlocked(cell.X,cell.Y))return "完整结构撞墙("+(cell.X+1)+","+(cell.Y+1)+")";
                if(!occupied.Add(Rules.CellId(cell.X,cell.Y)))return "固定接点接入后结构自身重叠";
                foreach(ItemState item in state.Items.Where(i=>i.Location==ItemLocation.Crate&&!held.Contains(i.Id)))
                    if(item.Cells.Any(c=>c.X==cell.X&&c.Y==cell.Y))return "完整结构与箱内物件ID"+item.Id+"重叠";
            }
            return null;
        }
        private ActionPreview BasicPreview(int cost,int itemId=0)
        {
            var p=new ActionPreview{Cost=cost,ItemId=itemId,AfterWeight=state.TotalWeight,Cells=state.OccupiedCells.Select(c=>c.Copy()).ToArray(),AffectedItemIds=new int[0],Reason="本轮已结束"};
            if(state.Phase==Phase.Finished)return p;
            if(state.Energy<cost){p.Reason="电量不足：需要"+cost+"，剩余"+state.Energy;return p;}
            p.Legal=true;p.Reason="合法动作，电量成本"+cost;return p;
        }
        private void ApplyProjectionPreview(ActionPreview p,Projection project)
        {
            p.AfterWeight=project.Weight;p.Cells=project.Cells.Select(c=>c.Copy()).ToArray();string error=GeometryError(project);
            if(error!=null){p.Legal=false;p.Reason=error;}
        }
        public ActionPreview PreviewMove(Direction direction)
        {
            ActionPreview p=BasicPreview(Rules.MoveCost);if(!p.Legal)return p;
            if(!Enum.IsDefined(typeof(Direction),direction)){p.Legal=false;p.Reason="未知移动方向";return p;}
            GridPoint d=Rules.Delta(direction);ApplyProjectionPreview(p,Project(state.RootX+d.X,state.RootY+d.Y,state.Facing,state.ChainIds,state.PendingItemId));return p;
        }
        public ActionPreview PreviewRotate(int quarterTurns)
        {
            ActionPreview p=BasicPreview(Rules.RotateCost);if(!p.Legal)return p;
            if(quarterTurns!=1&&quarterTurns!=-1){p.Legal=false;p.Reason="每次旋转仅允许正负一个90度";return p;}
            Direction facing=(Direction)(((int)state.Facing+quarterTurns+4)%4);ApplyProjectionPreview(p,Project(state.RootX,state.RootY,facing,state.ChainIds,state.PendingItemId));return p;
        }
        public AttractPreview PreviewAttract()
        {
            var p=new AttractPreview{Cost=Rules.AttractCost,AfterWeight=state.TotalWeight,BlockX=-1,BlockY=-1,FirstX=-1,FirstY=-1,EntryX=-1,EntryY=-1,ExitX=-1,ExitY=-1,RayCells=new GridPoint[0],CandidateCells=new GridPoint[0],Reason="本轮已结束"};
            if(state.Phase==Phase.Finished)return p;
            if(state.PendingItemId>0){p.Reason="待处置件计重占格但不延伸；先保留或到回收区出售，不能续吸";return p;}
            GridPoint d=Rules.Delta(state.EndDirection);var ray=new List<GridPoint>();ItemState first=null;
            for(int distance=1;distance<=Rules.BaseRange;distance++)
            {
                int x=state.EndX+d.X*distance,y=state.EndY+d.Y*distance;ray.Add(new GridPoint(x,y));
                if(!Rules.InBounds(x,y)||Rules.IsBlocked(x,y)||state.OccupiedCells.Any(c=>c.X==x&&c.Y==y))
                {p.BlockX=x;p.BlockY=y;p.RayCells=ray.ToArray();p.Reason=!Rules.InBounds(x,y)?"射线到达边界":Rules.IsBlocked(x,y)?"墙阻断后方铁件":"已装结构阻断射线";return p;}
                first=state.Items.Where(i=>i.Location==ItemLocation.Crate&&i.Cells.Any(c=>c.X==x&&c.Y==y)).OrderBy(i=>i.Id).FirstOrDefault();
                if(first==null)continue;p.ItemId=first.Id;p.Distance=distance;p.FirstX=x;p.FirstY=y;break;
            }
            p.RayCells=ray.ToArray();
            if(first==null){p.Reason="唯一末端射程内没有铁件";return p;}
            Projection project=Project(state.RootX,state.RootY,state.Facing,state.ChainIds,first.Id);ItemState candidate=project.Items.Last();
            p.CandidateCells=candidate.Cells.Select(c=>c.Copy()).ToArray();p.EntryX=candidate.EntryX;p.EntryY=candidate.EntryY;p.ExitX=candidate.ExitX;p.ExitY=candidate.ExitY;p.ExitDirection=candidate.ExitDirection;p.AfterWeight=project.Weight;
            if(state.Energy<p.Cost){p.Reason="电量不足：需要"+p.Cost+"，剩余"+state.Energy;return p;}
            string error=GeometryError(project);if(error!=null){p.Reason="首件ID"+first.Id+"不能吸取："+error+"；不跳过它";return p;}
            p.Legal=true;p.Reason="首件ID"+first.Id+"吸入待处置；成本"+p.Cost+"，不在此步发钱或延伸";return p;
        }
        public ActionPreview PreviewRetain()
        {
            ActionPreview p=BasicPreview(Rules.RetainCost,state.PendingItemId);if(!p.Legal)return p;
            if(state.PendingItemId==0){p.Legal=false;p.Reason="没有待处置件";return p;}
            p.AffectedItemIds=new[]{state.PendingItemId};ApplyProjectionPreview(p,Project(state.RootX,state.RootY,state.Facing,state.ChainIds.Concat(new[]{state.PendingItemId}).ToArray(),0));return p;
        }
        public ActionPreview PreviewSell(int itemId)
        {
            ActionPreview p=BasicPreview(Rules.SellCost,itemId);if(!p.Legal)return p;
            if(!Rules.IsDepot(state.RootX,state.RootY)){p.Legal=false;p.Reason="只能在回收区出售，实际交付前不发钱或进度";return p;}
            ItemState item=state.Items.FirstOrDefault(i=>i.Id==itemId);
            if(item==null||item.Location==ItemLocation.Crate||item.Location==ItemLocation.Sold){p.Legal=false;p.Reason="物件不在可出售归属，已售件不能再次交付";return p;}
            if(state.PendingItemId>0&&state.PendingItemId!=itemId){p.Legal=false;p.Reason="先处置当前待处置件，不能绕过确认去出售别件";return p;}
            if(item.Location==ItemLocation.Chain&&state.ChainIds.Last()!=itemId){p.Legal=false;p.Reason="链中段须先拆下该件及全部后段；仅链尾可直接出售";return p;}
            Projection project=Project(state.RootX,state.RootY,state.Facing,state.ChainIds.Where(id=>id!=itemId).ToArray(),state.PendingItemId==itemId?0:state.PendingItemId);
            p.AfterWeight=project.Weight;p.Cells=project.Cells.Select(c=>c.Copy()).ToArray();p.AffectedItemIds=new[]{itemId};return p;
        }
        public ActionPreview PreviewDetach(int chainIndex)
        {
            ActionPreview p=BasicPreview(Rules.DetachCost);if(!p.Legal)return p;
            if(state.PendingItemId>0){p.Legal=false;p.Reason="先保留或出售待处置件，再拆装";return p;}
            if(!Rules.IsDepot(state.RootX,state.RootY)){p.Legal=false;p.Reason="拆装仅在回收区进行";return p;}
            if(chainIndex<0||chainIndex>=state.ChainIds.Length){p.Legal=false;p.Reason="所选链段不存在";return p;}
            p.ItemId=state.ChainIds[chainIndex];p.AffectedItemIds=state.ChainIds.Skip(chainIndex).ToArray();
            ApplyProjectionPreview(p,Project(state.RootX,state.RootY,state.Facing,state.ChainIds.Take(chainIndex).ToArray(),0));
            if(p.Legal)p.Reason="选中件与全部后段进入公开暂存，不留下悬空延伸；成本"+p.Cost;return p;
        }
        public ActionPreview PreviewAttach(int itemId)
        {
            ActionPreview p=BasicPreview(Rules.AttachCost,itemId);if(!p.Legal)return p;
            if(state.PendingItemId>0){p.Legal=false;p.Reason="先处置待处置件，再从暂存重装";return p;}
            if(!Rules.IsDepot(state.RootX,state.RootY)){p.Legal=false;p.Reason="重装仅在回收区进行";return p;}
            if(!state.StorageIds.Contains(itemId)){p.Legal=false;p.Reason="该物件不在暂存区";return p;}
            p.AffectedItemIds=new[]{itemId};ApplyProjectionPreview(p,Project(state.RootX,state.RootY,state.Facing,state.ChainIds.Concat(new[]{itemId}).ToArray(),0));return p;
        }

        private CommandResult Execute(CommandKind kind,int a,string id)
        {
            last=new GameEvent[0];
            if(id!=null&&ids.Contains(id))return new CommandResult{Success=true,AlreadyApplied=true,Reason="命令已提交，不重复执行",Events=last};
            if(id!=null&&(string.IsNullOrWhiteSpace(id)||id.Length>128))return Reject("命令身份非法");
            ActionPreview action=null;AttractPreview attraction=null;
            switch(kind)
            {
                case CommandKind.Move:action=PreviewMove((Direction)a);break;
                case CommandKind.Rotate:action=PreviewRotate(a);break;
                case CommandKind.Attract:attraction=PreviewAttract();break;
                case CommandKind.Retain:action=PreviewRetain();break;
                case CommandKind.Sell:action=PreviewSell(a);break;
                case CommandKind.Detach:action=PreviewDetach(a);break;
                case CommandKind.Attach:action=PreviewAttach(a);break;
                default:return Reject("未知命令");
            }
            if(attraction!=null&&!attraction.Legal)return Reject(attraction.Reason);
            if(action!=null&&!action.Legal)return Reject(action.Reason);
            int cost=attraction!=null?attraction.Cost:action.Cost;int before=events.Count;
            state.ActionIndex=commands.Count+1;state.Energy-=cost;
            switch(kind)
            {
                case CommandKind.Move:
                    GridPoint d=Rules.Delta((Direction)a);state.RootX+=d.X;state.RootY+=d.Y;Emit(EventKind.Moved,0,cost,"根移动，完整链和待处置件同步；电量-"+cost);break;
                case CommandKind.Rotate:
                    state.Facing=(Direction)(((int)state.Facing+a+4)%4);Emit(EventKind.Rotated,0,cost,"绕根整体旋转90度，所有接点锁向；电量-"+cost);break;
                case CommandKind.Attract:
                    state.PendingItemId=attraction.ItemId;state.Items[attraction.ItemId-1].Location=ItemLocation.Pending;Emit(EventKind.Attracted,attraction.ItemId,cost,"唯一首件进入待处置；计重占格，不延伸、不发钱；电量-"+cost);break;
                case CommandKind.Retain:
                    int retained=state.PendingItemId;state.ChainIds=state.ChainIds.Concat(new[]{retained}).ToArray();state.PendingItemId=0;state.Items[retained-1].Location=ItemLocation.Chain;Emit(EventKind.Retained,retained,cost,"同一物件成为工具，唯一末端按固定接点改变；电量-"+cost);break;
                case CommandKind.Sell:
                    ItemState sold=state.Items[a-1];int income=sold.BaseValue;OrderRequirement requirement=state.Order.Requirements.FirstOrDefault(r=>r.Kind==sold.Kind&&r.Delivered<r.Required);
                    if(requirement!=null){requirement.Delivered++;income+=Rules.OrderBonus;}
                    state.Wallet+=income;state.ChainIds=state.ChainIds.Where(i=>i!=a).ToArray();state.StorageIds=state.StorageIds.Where(i=>i!=a).ToArray();if(state.PendingItemId==a)state.PendingItemId=0;
                    state.SoldIds=state.SoldIds.Concat(new[]{a}).OrderBy(i=>i).ToArray();sold.Location=ItemLocation.Sold;Emit(EventKind.Sold,a,income,"实际出售一次，收入"+income+"；移除物件及延伸；电量-"+cost);break;
                case CommandKind.Detach:
                    int[] detached=state.ChainIds.Skip(a).ToArray();state.ChainIds=state.ChainIds.Take(a).ToArray();state.StorageIds=state.StorageIds.Concat(detached).OrderBy(i=>i).ToArray();
                    foreach(int item in detached){state.Items[item-1].Location=ItemLocation.Storage;Emit(EventKind.Detached,item,cost,"该件与其全部后段进入暂存；整次拆卸电量-"+cost+"，不逐件重复收费");}break;
                case CommandKind.Attach:
                    state.StorageIds=state.StorageIds.Where(i=>i!=a).ToArray();state.ChainIds=state.ChainIds.Concat(new[]{a}).ToArray();state.Items[a-1].Location=ItemLocation.Chain;Emit(EventKind.Attached,a,cost,"暂存实际物件按唯一末端重装；重新校验几何/负载；电量-"+cost);break;
            }
            Rebuild();
            if(state.Order.Complete||state.Energy==0)
            {
                state.Phase=Phase.Finished;state.Outcome=state.Order.Complete?RoundOutcome.Completed:RoundOutcome.Failed;
                Emit(EventKind.RoundFinished,0,state.Order.Delivered,state.Outcome==RoundOutcome.Completed?"订单实际交付完成；先目标、后电量截止":"电量耗尽，订单未完成；保留真实归属与已交数量");
            }
            else state.Phase=state.PendingItemId>0?Phase.AwaitingDisposition:Phase.Active;
            id=id??"c"+(commands.Count+1).ToString(CultureInfo.InvariantCulture);while(ids.Contains(id))id="_"+id;
            commands.Add(new CommandRecord{Id=id,Kind=kind,A=a});ids.Add(id);last=events.Skip(before).Select(e=>e.Copy()).ToArray();
            return new CommandResult{Success=true,Reason=last[last.Length-1].Reason,Events=LastEvents};
        }
        private CommandResult Reject(string reason) { return new CommandResult{Success=false,Reason=reason,Events=new GameEvent[0]}; }
        private void Rebuild()
        {
            Projection p=Project(state.RootX,state.RootY,state.Facing,state.ChainIds,state.PendingItemId);
            state.EndX=p.EndX;state.EndY=p.EndY;state.EndDirection=p.EndDirection;state.TotalWeight=p.Weight;state.OccupiedCells=p.Cells.Select(c=>c.Copy()).ToArray();
            foreach(ItemState pose in p.Items)
            {
                ItemState item=state.Items[pose.Id-1];Rules.SetPose(item,pose.AnchorX,pose.AnchorY,pose.Facing);
                item.Location=pose.Id==state.PendingItemId?ItemLocation.Pending:ItemLocation.Chain;
            }
        }
        private void Emit(EventKind kind,int itemId,int amount,string reason) { events.Add(new GameEvent{Sequence=events.Count+1,ActionIndex=state.ActionIndex,Kind=kind,ItemId=itemId,X=state.RootX,Y=state.RootY,Amount=amount,Reason=reason}); }
        public RoundSnapshot ExportSnapshot()
        {
            return new RoundSnapshot{SchemaVersion=state.SchemaVersion,RulesVersion=state.RulesVersion,OrderKind=state.OrderKind,Phase=state.Phase,Outcome=state.Outcome,
                RootX=state.RootX,RootY=state.RootY,Facing=state.Facing,Energy=state.Energy,Wallet=state.Wallet,PendingItemId=state.PendingItemId,
                EndX=state.EndX,EndY=state.EndY,EndDirection=state.EndDirection,TotalWeight=state.TotalWeight,ActionIndex=state.ActionIndex,
                ChainIds=(int[])state.ChainIds.Clone(),StorageIds=(int[])state.StorageIds.Clone(),SoldIds=(int[])state.SoldIds.Clone(),
                OccupiedCells=state.OccupiedCells.Select(c=>c.Copy()).ToArray(),Items=state.Items.Select(i=>i.Copy()).ToArray(),Order=state.Order.Copy(),Commands=commands.Select(c=>c.Copy()).ToArray(),Events=events.Select(e=>e.Copy()).ToArray()};
        }
        public static bool ValidateSnapshot(RoundSnapshot snapshot,out string reason) { Session ignored;return TryRestore(snapshot,out ignored,out reason); }
        public static bool TryRestore(RoundSnapshot snapshot,out Session restored,out string reason)
        {
            restored=null;reason="快照结构、版本或范围非法";if(!BasicValid(snapshot))return false;
            var replay=new Session(snapshot.OrderKind);
            foreach(CommandRecord command in snapshot.Commands)
            {
                if(command==null||!Enum.IsDefined(typeof(CommandKind),command.Kind)||string.IsNullOrWhiteSpace(command.Id)||command.Id.Length>128||((command.Kind==CommandKind.Attract||command.Kind==CommandKind.Retain)&&command.A!=0)){reason="无效命令记录";return false;}
                CommandResult result=replay.Execute(command.Kind,command.A,command.Id);
                if(!result.Success||result.AlreadyApplied){reason="生产命令重放失败："+result.Reason;return false;}
            }
            if(Canonical(snapshot)!=Canonical(replay.ExportSnapshot())){reason="快照与生产命令不一致，拒绝免费物件、钱、进度或电量";return false;}
            replay.last=new GameEvent[0];restored=replay;reason="有效完整操作检查点；恢复不额外吸取或出售";return true;
        }
        private static bool BasicValid(RoundSnapshot s)
        {
            if(s==null||s.SchemaVersion!=Rules.SchemaVersion||s.RulesVersion!=Rules.Version||!Enum.IsDefined(typeof(OrderKind),s.OrderKind)||!Enum.IsDefined(typeof(Phase),s.Phase)||!Enum.IsDefined(typeof(RoundOutcome),s.Outcome)
                ||!Rules.InBounds(s.RootX,s.RootY)||!Enum.IsDefined(typeof(Direction),s.Facing)||!Enum.IsDefined(typeof(Direction),s.EndDirection)||s.Energy<0||s.Energy>Rules.InitialEnergy||s.Wallet<0||s.TotalWeight<0||s.TotalWeight>Rules.MaxLoad||s.ActionIndex<0||s.ActionIndex>Rules.MaximumCommands
                ||s.Items==null||s.Items.Length!=Rules.ItemCount||s.ChainIds==null||s.ChainIds.Length>Rules.ItemCount||s.StorageIds==null||s.StorageIds.Length>Rules.ItemCount||s.SoldIds==null||s.SoldIds.Length>Rules.ItemCount||s.PendingItemId<0||s.PendingItemId>Rules.ItemCount
                ||s.OccupiedCells==null||s.OccupiedCells.Length<1||s.OccupiedCells.Length>21||s.OccupiedCells.Any(c=>c==null)||s.Order==null||s.Order.Requirements==null||s.Order.Requirements.Length!=2||s.Commands==null||s.Commands.Length>Rules.MaximumCommands||s.Events==null||s.Events.Length>200)return false;
            var ownership=s.ChainIds.Concat(s.StorageIds).Concat(s.SoldIds).Concat(s.PendingItemId==0?new int[0]:new[]{s.PendingItemId}).ToList();
            for(int i=0;i<s.Items.Length;i++)
            {
                ItemState item=s.Items[i];if(item==null||item.Id!=i+1||!Enum.IsDefined(typeof(ItemKind),item.Kind)||!Enum.IsDefined(typeof(ItemLocation),item.Location)||!Enum.IsDefined(typeof(Direction),item.Facing)||!Enum.IsDefined(typeof(Direction),item.ExitDirection)||item.Cells==null||item.Cells.Length<1||item.Cells.Length>4||item.Cells.Any(c=>c==null||!Rules.InBounds(c.X,c.Y)))return false;
                if(item.Location==ItemLocation.Crate)ownership.Add(item.Id);
            }
            if(ownership.Count!=Rules.ItemCount||ownership.Distinct().Count()!=Rules.ItemCount||ownership.Any(i=>i<1||i>Rules.ItemCount))return false;
            foreach(OrderRequirement requirement in s.Order.Requirements)if(requirement==null||!Enum.IsDefined(typeof(ItemKind),requirement.Kind)||requirement.Required!=1||requirement.Delivered<0||requirement.Delivered>1)return false;
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
