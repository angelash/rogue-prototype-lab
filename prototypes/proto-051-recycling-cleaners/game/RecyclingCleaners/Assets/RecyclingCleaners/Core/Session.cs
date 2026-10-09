using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RecyclingCleaners
{
    public sealed class Session
    {
        private readonly RoundSnapshot state;
        private readonly List<CommandRecord> commands = new List<CommandRecord>();
        private readonly List<GameEvent> events = new List<GameEvent>();
        private readonly HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        private List<GameEvent> pending;
        private GameEvent[] last = new GameEvent[0];
        public RoundSnapshot State { get { return ExportSnapshot(); } }
        public GameEvent[] LastEvents { get { return last.Select(e=>e.Copy()).ToArray(); } }

        private Session(LayoutKind layout, int capacity, bool filter)
        {
            if (!Enum.IsDefined(typeof(LayoutKind),layout)) throw new ArgumentOutOfRangeException("layout");
            if (capacity!=Rules.SmallBucketCapacity && capacity!=Rules.LargeBucketCapacity) throw new ArgumentOutOfRangeException("capacity");
            state = new RoundSnapshot { SchemaVersion=Rules.SchemaVersion, RulesVersion=Rules.Version, Layout=layout,
                BucketCapacity=capacity, FilterEnabled=filter, InitialFilterEnabled=filter, Phase=Phase.Active,
                PlayerX=Rules.SupplyX, PlayerY=Rules.SupplyY, ActionsRemaining=Rules.ActionBudget, Durability=Rules.MaxDurability,
                Wallet=Rules.InitialWallet, Brush=BrushKind.Narrow, FilterChargesRemaining=Rules.FilterCapacity, Cells=Rules.CreateCells(layout) };
        }
        public static Session CreatePrototype(LayoutKind layout=LayoutKind.Compact,int bucketCapacity=Rules.SmallBucketCapacity,bool filterEnabled=false) { return new Session(layout,bucketCapacity,filterEnabled); }
        public Session Retry() { return new Session(state.Layout,state.BucketCapacity,state.InitialFilterEnabled); }
        public CommandResult Move(int dx,int dy,string commandId=null) { return Execute(CommandKind.Move,dx,dy,false,commandId); }
        public CommandResult SetBrush(BrushKind brush,string commandId=null) { return Execute(CommandKind.SetBrush,(int)brush,0,false,commandId); }
        public CommandResult Vacuum(int x,int y,string commandId=null) { return Execute(CommandKind.Vacuum,x,y,false,commandId); }
        public CommandResult Scrub(int x,int y,string commandId=null) { return Execute(CommandKind.Scrub,x,y,false,commandId); }
        public CommandResult UseDetergent(int x,int y,string commandId=null) { return Execute(CommandKind.UseDetergent,x,y,false,commandId); }
        public CommandResult Convert(string commandId=null) { return Execute(CommandKind.Convert,0,0,false,commandId); }
        public CommandResult Sell(string commandId=null) { return Execute(CommandKind.Sell,0,0,false,commandId); }
        public CommandResult Discard(MaterialKind material,int amount,string commandId=null) { return Execute(CommandKind.Discard,(int)material,amount,false,commandId); }
        public CommandResult SetFilter(bool enabled,string commandId=null) { return Execute(CommandKind.SetFilter,0,0,enabled,commandId); }
        public CommandResult Restock(string commandId=null) { return Execute(CommandKind.Restock,0,0,false,commandId); }

        public int[] PreviewCoverage(int x,int y)
        {
            if(!Rules.InBounds(x,y) || state.Cells[Rules.CellId(x,y)].Blocked || Distance(state.PlayerX,state.PlayerY,x,y)>1) return new int[0];
            return state.Cells.Where(c=>!c.Blocked && (state.Brush==BrushKind.Narrow ? c.X==x && c.Y==y : Distance(c.X,c.Y,x,y)<=1))
                .OrderBy(c=>Distance(state.PlayerX,state.PlayerY,c.X,c.Y)).ThenBy(c=>c.Id).Select(c=>c.Id).ToArray();
        }
        private static int Distance(int ax,int ay,int bx,int by) { return Math.Abs(ax-bx)+Math.Abs(ay-by); }
        private bool AtSupply { get { return state.PlayerX==Rules.SupplyX && state.PlayerY==Rules.SupplyY; } }
        private string CostError(int actions,int durability)
        {
            if(state.ActionsRemaining<actions)return "行动预算不足";
            if(state.Durability<durability)return "耐久不足，请返回补给点付费恢复";
            return null;
        }
        private void Pay(int actions,int durability) { state.ActionsRemaining-=actions;state.Durability-=durability;state.DurabilitySpent+=durability; }
        private int BrushCost { get { return state.Brush==BrushKind.Narrow?Rules.NarrowActionCost:Rules.WideActionCost; } }
        private int BrushDurability { get { return state.Brush==BrushKind.Narrow?Rules.NarrowDurabilityCost:Rules.WideDurabilityCost; } }
        private bool CanConvert { get { return state.FilterEnabled && state.Convertible>=Rules.RecipeInput && state.FilterChargesRemaining>0 && state.Detergent<Rules.DetergentCapacity; } }

        private CommandResult Execute(CommandKind kind,int a,int b,bool flag,string id)
        {
            if(id==null){int suffix=commands.Count+1;do{id="auto-"+suffix++;}while(ids.Contains(id));}
            if(string.IsNullOrWhiteSpace(id)||id.Length>128)return Failure("命令编号无效");
            if(ids.Contains(id))return new CommandResult{Success=true,AlreadyApplied=true,Reason="该动作已提交",Events=new GameEvent[0]};
            if(state.Phase==Phase.Finished)return Failure("本轮已结束，请重试或恢复安全点");
            if(commands.Count>=Rules.MaximumCommands)return Failure("本轮操作记录已达上限");
            pending=new List<GameEvent>();
            string error;
            switch(kind)
            {
                case CommandKind.Move:error=DoMove(a,b);break;
                case CommandKind.SetBrush:error=DoBrush((BrushKind)a);break;
                case CommandKind.Vacuum:error=DoVacuum(a,b);break;
                case CommandKind.Scrub:error=DoScrub(a,b);break;
                case CommandKind.UseDetergent:error=DoDetergent(a,b);break;
                case CommandKind.Convert:error=DoConvert();break;
                case CommandKind.Sell:error=DoSell();break;
                case CommandKind.Discard:error=DoDiscard((MaterialKind)a,b);break;
                case CommandKind.SetFilter:error=DoFilter(flag);break;
                case CommandKind.Restock:error=DoRestock();break;
                default:error="未知动作";break;
            }
            if(error!=null){pending=null;return Failure(error);}
            if(state.RemainingDirt==0 || state.ActionsRemaining==0)
            {
                state.Phase=Phase.Finished;
                state.Outcome=state.RemainingDirt==0?RoundOutcome.Completed:RoundOutcome.Failed;
                Emit(EventKind.RoundFinished,state.PlayerX,state.PlayerY,state.CleanedUnits,state.Outcome==RoundOutcome.Completed?"全部污物已清洁；先更新完成度再检查截止":"行动截止，仍有污物未处理");
            }
            commands.Add(new CommandRecord{Id=id,Kind=kind,A=a,B=b,Flag=flag});ids.Add(id);
            last=pending.ToArray();pending=null;
            return new CommandResult{Success=true,Reason="完成",Events=LastEvents};
        }
        private static CommandResult Failure(string reason) { return new CommandResult{Reason=reason,Events=new GameEvent[0]}; }
        private string DoMove(int dx,int dy)
        {
            if(Math.Abs((long)dx)+Math.Abs((long)dy)!=1)return "每次只能正交移动一格";
            int x=state.PlayerX+dx,y=state.PlayerY+dy;
            if(!Rules.InBounds(x,y)||state.Cells[Rules.CellId(x,y)].Blocked)return "目的地越界或有障碍";
            string error=CostError(1,0);if(error!=null)return error;
            Pay(1,0);state.PlayerX=x;state.PlayerY=y;Emit(EventKind.Moved,x,y,1,"移动消耗1行动，耐久不变");return null;
        }
        private string DoBrush(BrushKind brush)
        {
            if(!AtSupply)return "换头仅在补给点(0,0)";
            if(!Enum.IsDefined(typeof(BrushKind),brush)||brush==state.Brush)return "刷头无效或没有改变";
            string error=CostError(1,1);if(error!=null)return error;
            Pay(1,1);state.Brush=brush;Emit(EventKind.BrushChanged,state.PlayerX,state.PlayerY,1,Rules.BrushName(brush)+"：付1行动/1耐久换头");return null;
        }
        private string DoVacuum(int x,int y)
        {
            int[] covered=PreviewCoverage(x,y);
            if(covered.Length==0)return "目标不在可达刷头范围";
            if(!covered.Any(id=>state.Cells[id].Dirt==DirtKind.Debris && state.Cells[id].Remaining>0))return "覆盖区没有可吸碎屑";
            bool auto=CanConvert && state.Durability>=BrushDurability+Rules.AutomaticConversionDurability;
            int available=state.BucketCapacity-state.BucketUsed+(auto?Rules.RecipeInput:0);
            if(available<=0)return "桶满：剩余碎屑留在场地，先出售、丢弃或转换";
            string error=CostError(BrushCost,BrushDurability+(auto?Rules.AutomaticConversionDurability:0));if(error!=null)return error;
            Pay(BrushCost,BrushDurability+(auto?Rules.AutomaticConversionDurability:0));
            if(auto)ConvertMaterial("吸取前只转换桶内旧材料一次，不循环转换新入料");
            int power=state.Brush==BrushKind.Narrow?2:1;
            foreach(int id in covered)
            {
                CellState cell=state.Cells[id];
                if(cell.Dirt!=DirtKind.Debris || cell.Remaining==0)continue;
                int take=Math.Min(power,Math.Min(cell.Remaining,state.BucketCapacity-state.BucketUsed));
                if(take>0){cell.Remaining-=take;if(cell.Material==MaterialKind.Ordinary)state.Ordinary+=take;else state.Convertible+=take;Emit(EventKind.Vacuumed,cell.X,cell.Y,take,"碎屑减少与"+Rules.MaterialName(cell.Material)+"入桶等量");}
                if(cell.Remaining>0 && state.BucketUsed==state.BucketCapacity)Emit(EventKind.BucketBlocked,cell.X,cell.Y,cell.Remaining,"装不下的碎屑仍留场");
            }
            return null;
        }
        private string DoScrub(int x,int y)
        {
            int[] covered=PreviewCoverage(x,y);
            if(covered.Length==0)return "目标不在可达刷头范围";
            if(!covered.Any(id=>state.Cells[id].Remaining>0 && (state.Cells[id].Dirt==DirtKind.Water || state.Cells[id].Dirt==DirtKind.Stubborn)))return "覆盖区没有可擦洗水渍或顽渍";
            string error=CostError(BrushCost,BrushDurability);if(error!=null)return error;
            Pay(BrushCost,BrushDurability);
            foreach(int id in covered)
            {
                CellState cell=state.Cells[id];
                if(cell.Remaining==0 || (cell.Dirt!=DirtKind.Water && cell.Dirt!=DirtKind.Stubborn))continue;
                int amount=Math.Min(cell.Remaining,cell.Dirt==DirtKind.Water && state.Brush==BrushKind.Narrow?2:1);
                cell.Remaining-=amount;Emit(cell.Dirt==DirtKind.Water?EventKind.WaterScrubbed:EventKind.StubbornScrubbed,cell.X,cell.Y,amount,"基础刷头擦洗；不产生回收材料");
            }
            return null;
        }
        private string DoDetergent(int x,int y)
        {
            if(!Rules.InBounds(x,y)||Distance(state.PlayerX,state.PlayerY,x,y)>1)return "清洁剂目标不可达";
            CellState cell=state.Cells[Rules.CellId(x,y)];
            if(cell.Dirt!=DirtKind.Stubborn||cell.Remaining==0||state.Detergent==0)return "需要清洁剂与未清完的顽渍";
            string error=CostError(1,1);if(error!=null)return error;
            Pay(1,1);state.Detergent--;state.DetergentUsed++;int amount=Math.Min(Rules.DetergentCleaningPower,cell.Remaining);cell.Remaining-=amount;
            Emit(EventKind.DetergentApplied,x,y,amount,"消耗1剂清顽渍，剂不能再次卖作原料");return null;
        }
        private void ConvertMaterial(string reason)
        {
            state.Convertible-=Rules.RecipeInput;state.Detergent++;state.Conversions++;state.FilterChargesRemaining--;
            Emit(EventKind.Converted,state.PlayerX,state.PlayerY,1,reason+"：可转化料2→清洁剂1");
        }
        private string DoConvert()
        {
            if(!CanConvert)return "转换需开启滤芯、可转化料2、未满剂槽及剩余滤芯次数";
            string error=CostError(1,1);if(error!=null)return error;
            Pay(1,1);ConvertMaterial("手动一次转换，消耗1行动/1耐久");return null;
        }
        private string DoSell()
        {
            if(!AtSupply)return "出售仅在补给点(0,0)";
            if(state.BucketUsed==0)return "桶内没有可售材料，清洁剂不出售";
            string error=CostError(1,1);if(error!=null)return error;
            Pay(1,1);int income=state.Ordinary*Rules.OrdinarySalePrice+state.Convertible*Rules.ConvertibleSalePrice;
            state.SoldOrdinary+=state.Ordinary;state.SoldConvertible+=state.Convertible;state.Ordinary=0;state.Convertible=0;state.Wallet+=income;
            Emit(EventKind.MaterialSold,state.PlayerX,state.PlayerY,income,"材料永久出售；1行动/1耐久腾桶，不能再合成");return null;
        }
        private string DoDiscard(MaterialKind material,int amount)
        {
            if(!AtSupply)return "丢弃仅在补给点(0,0)";
            if(!Enum.IsDefined(typeof(MaterialKind),material)||amount<=0)return "丢弃类型或数量无效";
            int stock=material==MaterialKind.Ordinary?state.Ordinary:state.Convertible;
            if(amount>stock)return "材料不足，不执行部分丢弃";
            string error=CostError(1,1);if(error!=null)return error;
            Pay(1,1);if(material==MaterialKind.Ordinary){state.Ordinary-=amount;state.DiscardedOrdinary+=amount;}else{state.Convertible-=amount;state.DiscardedConvertible+=amount;}
            Emit(EventKind.Discarded,state.PlayerX,state.PlayerY,amount,"丢弃"+Rules.MaterialName(material)+"无现金；1行动/1耐久腾桶");return null;
        }
        private string DoFilter(bool enabled)
        {
            if(state.FilterEnabled==enabled)return "滤芯开关没有改变";
            string error=CostError(1,0);if(error!=null)return error;
            Pay(1,0);state.FilterEnabled=enabled;Emit(EventKind.FilterChanged,state.PlayerX,state.PlayerY,enabled?1:0,"滤芯开关消耗1行动，不补次数");return null;
        }
        private string DoRestock()
        {
            if(!AtSupply)return "耐久补给仅在(0,0)";
            if(state.Durability==Rules.MaxDurability)return "耐久已满";
            if(state.Wallet<Rules.RestockCost)return "补给现金不足";
            string error=CostError(Rules.RestockActions,0);if(error!=null)return error;
            Pay(Rules.RestockActions,0);int restored=Rules.MaxDurability-state.Durability;state.Durability=Rules.MaxDurability;
            state.DurabilityRecovered+=restored;state.Restocks++;state.Wallet-=Rules.RestockCost;
            Emit(EventKind.Restocked,state.PlayerX,state.PlayerY,restored,"现金3/行动2恢复耐久，不加预算、不补滤芯");return null;
        }
        private void Emit(EventKind kind,int x,int y,int amount,string reason)
        {
            var e=new GameEvent{Sequence=events.Count+1,ActionIndex=Rules.ActionBudget-state.ActionsRemaining,Kind=kind,X=x,Y=y,Amount=amount,Reason=reason};events.Add(e);pending.Add(e);
        }

        public RoundSnapshot ExportSnapshot()
        {
            var copy=new RoundSnapshot{SchemaVersion=state.SchemaVersion,RulesVersion=state.RulesVersion,Layout=state.Layout,BucketCapacity=state.BucketCapacity,
                FilterEnabled=state.FilterEnabled,InitialFilterEnabled=state.InitialFilterEnabled,Phase=state.Phase,Outcome=state.Outcome,
                PlayerX=state.PlayerX,PlayerY=state.PlayerY,ActionsRemaining=state.ActionsRemaining,Durability=state.Durability,Wallet=state.Wallet,Brush=state.Brush,
                Ordinary=state.Ordinary,Convertible=state.Convertible,Detergent=state.Detergent,FilterChargesRemaining=state.FilterChargesRemaining,
                Conversions=state.Conversions,DetergentUsed=state.DetergentUsed,SoldOrdinary=state.SoldOrdinary,SoldConvertible=state.SoldConvertible,
                DiscardedOrdinary=state.DiscardedOrdinary,DiscardedConvertible=state.DiscardedConvertible,DurabilitySpent=state.DurabilitySpent,
                DurabilityRecovered=state.DurabilityRecovered,Restocks=state.Restocks,Cells=state.Cells.Select(c=>c.Copy()).ToArray(),Commands=commands.Select(c=>c.Copy()).ToArray(),Events=events.Select(e=>e.Copy()).ToArray()};
            return copy;
        }
        public static bool ValidateSnapshot(RoundSnapshot snapshot,out string reason) { Session ignored;return TryRestore(snapshot,out ignored,out reason); }
        public static bool TryRestore(RoundSnapshot snapshot,out Session restored,out string reason)
        {
            restored=null;reason="快照结构、版本或范围非法";
            if(!BasicValid(snapshot))return false;
            var replay=new Session(snapshot.Layout,snapshot.BucketCapacity,snapshot.InitialFilterEnabled);
            foreach(CommandRecord command in snapshot.Commands)
            {
                if(command==null||!Enum.IsDefined(typeof(CommandKind),command.Kind)||string.IsNullOrWhiteSpace(command.Id)){reason="无效命令记录";return false;}
                CommandResult result=replay.Execute(command.Kind,command.A,command.B,command.Flag,command.Id);
                if(!result.Success||result.AlreadyApplied){reason="命令重放失败："+result.Reason;return false;}
            }
            if(Canonical(snapshot)!=Canonical(replay.ExportSnapshot())){reason="快照与生产命令不一致，拒绝免费料、钱或清洁";return false;}
            replay.last=new GameEvent[0];restored=replay;reason="有效完整动作检查点";return true;
        }
        private static bool BasicValid(RoundSnapshot s)
        {
            if(s==null||s.SchemaVersion!=Rules.SchemaVersion||s.RulesVersion!=Rules.Version||!Enum.IsDefined(typeof(LayoutKind),s.Layout)
                ||(s.BucketCapacity!=Rules.SmallBucketCapacity&&s.BucketCapacity!=Rules.LargeBucketCapacity)||!Rules.InBounds(s.PlayerX,s.PlayerY)
                ||s.Cells==null||s.Cells.Length!=Rules.Size*Rules.Size||s.Commands==null||s.Commands.Length>Rules.MaximumCommands||s.Events==null||s.Events.Length>50000
                ||!Enum.IsDefined(typeof(Phase),s.Phase)||!Enum.IsDefined(typeof(RoundOutcome),s.Outcome)||!Enum.IsDefined(typeof(BrushKind),s.Brush)
                ||s.ActionsRemaining<0||s.ActionsRemaining>Rules.ActionBudget||s.Durability<0||s.Durability>Rules.MaxDurability||s.Wallet<0
                ||s.Ordinary<0||s.Convertible<0||(long)s.Ordinary+s.Convertible>s.BucketCapacity||s.Detergent<0||s.Detergent>Rules.DetergentCapacity
                ||s.Conversions<0||s.Conversions>Rules.FilterCapacity||s.DetergentUsed<0||s.FilterChargesRemaining!=Rules.FilterCapacity-s.Conversions
                ||s.Detergent!=s.Conversions-s.DetergentUsed||s.SoldOrdinary<0||s.SoldConvertible<0||s.DiscardedOrdinary<0||s.DiscardedConvertible<0
                ||s.DurabilitySpent<0||s.DurabilityRecovered<0||s.Restocks<0)return false;
            long ordinaryExtracted=0,convertibleExtracted=0;
            for(int i=0;i<s.Cells.Length;i++)
            {
                CellState c=s.Cells[i];if(c==null||c.Id!=i||c.X!=i%Rules.Size||c.Y!=i/Rules.Size||!Enum.IsDefined(typeof(DirtKind),c.Dirt)
                    ||!Enum.IsDefined(typeof(MaterialKind),c.Material)||c.InitialAmount<0||c.InitialAmount>4||c.Remaining<0||c.Remaining>c.InitialAmount)return false;
                if(c.Dirt==DirtKind.Debris){if(c.Material==MaterialKind.Ordinary)ordinaryExtracted+=c.InitialAmount-c.Remaining;else convertibleExtracted+=c.InitialAmount-c.Remaining;}
            }
            if(s.Cells[Rules.CellId(s.PlayerX,s.PlayerY)].Blocked || ordinaryExtracted!=(long)s.Ordinary+s.SoldOrdinary+s.DiscardedOrdinary
                ||convertibleExtracted!=(long)s.Convertible+s.SoldConvertible+s.DiscardedConvertible+(long)Rules.RecipeInput*s.Conversions
                ||s.Wallet!=(long)Rules.InitialWallet+(long)s.SoldOrdinary*Rules.OrdinarySalePrice+(long)s.SoldConvertible*Rules.ConvertibleSalePrice-(long)s.Restocks*Rules.RestockCost
                ||s.Durability!=(long)Rules.MaxDurability+s.DurabilityRecovered-s.DurabilitySpent)return false;
            if(s.Phase==Phase.Active&&(s.Outcome!=RoundOutcome.None||s.RemainingDirt==0||s.ActionsRemaining==0))return false;
            if(s.Phase==Phase.Finished&&(s.Outcome!=(s.RemainingDirt==0?RoundOutcome.Completed:RoundOutcome.Failed)||(s.RemainingDirt>0&&s.ActionsRemaining>0)))return false;
            foreach(GameEvent e in s.Events)if(e==null||e.Reason==null)return false;
            return true;
        }
        // Public serialized fields only; sorted names give a stable exhaustive comparison without Unity or JSON.
        private static string Canonical(object value)
        {
            if(value==null)return "null;";
            Type type=value.GetType();
            if(type.IsEnum||type.IsPrimitive||value is string){string text=System.Convert.ToString(value,CultureInfo.InvariantCulture);return text.Length+":"+text+";";}
            var array=value as Array;
            var result=new StringBuilder();
            if(array!=null){result.Append('[').Append(array.Length).Append(':');foreach(object item in array)result.Append(Canonical(item));return result.Append(']').ToString();}
            foreach(FieldInfo field in type.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))result.Append(field.Name).Append('=').Append(Canonical(field.GetValue(value)));
            return result.ToString();
        }
    }
}
