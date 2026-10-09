using System;
using System.Collections.Generic;

namespace SushiWorkshop
{
    public sealed class Session
    {
        private readonly RoundSnapshot state;
        private readonly List<CommandRecord> commands = new List<CommandRecord>();
        private readonly List<GameEvent> events = new List<GameEvent>();
        private readonly HashSet<string> appliedIds = new HashSet<string>(StringComparer.Ordinal);
        private List<GameEvent> pendingEvents;
        private GameEvent[] lastEvents = new GameEvent[0];

        public RoundSnapshot State { get { return ExportSnapshot(); } }
        public GameEvent[] LastEvents { get { return CopyEvents(lastEvents); } }
        public bool CanPrepare { get { return state.Phase == Phase.Preparation || state.Phase == Phase.CircleBoundary; } }

        private Session(OrderKind order)
        {
            if (!Enum.IsDefined(typeof(OrderKind), order)) throw new ArgumentOutOfRangeException("order");
            state = new RoundSnapshot
            {
                SchemaVersion = Rules.SnapshotSchemaVersion,
                RulesVersion = Rules.Version,
                Phase = Phase.Preparation,
                StepLimit = Rules.StepLimit,
                Wallet = Rules.InitialWallet,
                AdjustmentsLeft = Rules.AdjustmentsPerRound,
                Plates = new PlateState[Rules.PlateCount],
                Stations = new StationKind[Rules.SlotCount],
                Order = new OrderState
                {
                    Kind = order,
                    RequiredFood = order == OrderKind.WholeHeated ? FoodState.HeatedRoll : FoodState.SmallRoll,
                    RequiredAmountUnits = order == OrderKind.WholeHeated ? Rules.WholeAmountUnits : Rules.SmallAmountUnits,
                    RequiredQuantity = Rules.OrderQuantity,
                    RemainingWait = order == OrderKind.WholeHeated ? Rules.WholeOrderWait : Rules.SmallOrderWait,
                    UnitPrice = order == OrderKind.WholeHeated ? Rules.WholeOrderPrice : Rules.SmallOrderPrice,
                    Status = OrderStatus.Waiting
                }
            };
            for (int i = 0; i < state.Plates.Length; i++)
                state.Plates[i] = new PlateState { Id = i, Location = PlateLocation.CleanInventory, Slot = -1 };
            state.Stations[1] = StationKind.Filling;
            state.Stations[2] = StationKind.Rolling;
            state.Stations[3] = StationKind.Heating;
            state.Stations[4] = StationKind.Cutting;
        }

        public static Session CreatePrototype(OrderKind order = OrderKind.WholeHeated) { return new Session(order); }
        public Session Retry() { return new Session(state.Order.Kind); }

        public CommandResult StartOrResume(string commandId = null) { return Execute(CommandKind.StartOrResume, 0, 0, false, commandId); }
        public CommandResult Step(string commandId = null) { return Execute(CommandKind.Step, 0, 0, false, commandId); }
        public CommandResult LoadIngredient(string commandId = null) { return Execute(CommandKind.LoadIngredient, 0, 0, false, commandId); }
        public CommandResult SetDeliverable(int plateId, bool value, string commandId = null) { return Execute(CommandKind.SetDeliverable, plateId, 0, value, commandId); }
        public CommandResult QueueBaseSale(int plateId, bool value, string commandId = null) { return Execute(CommandKind.QueueBaseSale, plateId, 0, value, commandId); }
        public CommandResult WashOne(string commandId = null) { return Execute(CommandKind.WashOne, 0, 0, false, commandId); }
        public CommandResult MoveStation(int sourceSlot, int targetSlot, string commandId = null) { return Execute(CommandKind.MoveStation, sourceSlot, targetSlot, false, commandId); }
        public CommandResult SellStation(int slot, string commandId = null) { return Execute(CommandKind.SellStation, slot, 0, false, commandId); }
        public CommandResult BuyStation(int slot, StationKind kind, string commandId = null) { return Execute(CommandKind.BuyStation, slot, (int)kind, false, commandId); }
        public CommandResult AbandonOrder(string commandId = null) { return Execute(CommandKind.AbandonOrder, 0, 0, false, commandId); }

        private CommandResult Execute(CommandKind kind, int a, int b, bool flag, string id)
        {
            if (id == null)
            {
                int suffix = commands.Count + 1;
                do { id = "auto-" + suffix++; } while (appliedIds.Contains(id));
            }
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128) return Failure("命令编号为空或过长");
            if (appliedIds.Contains(id))
                return new CommandResult { Success = true, AlreadyApplied = true, Reason = "该命令已经提交", Events = new GameEvent[0] };
            if (commands.Count >= Rules.MaximumRecordedCommands) return Failure("本轮操作记录已达上限，请保存并重试本轮");
            pendingEvents = new List<GameEvent>();
            string error;
            switch (kind)
            {
                case CommandKind.StartOrResume: error = Start(); break;
                case CommandKind.Step: error = Advance(); break;
                case CommandKind.LoadIngredient: error = Load(); break;
                case CommandKind.SetDeliverable: error = Mark(a, flag, false); break;
                case CommandKind.QueueBaseSale: error = Mark(a, flag, true); break;
                case CommandKind.WashOne: error = Wash(); break;
                case CommandKind.MoveStation: error = Move(a, b); break;
                case CommandKind.SellStation: error = Sell(a); break;
                case CommandKind.BuyStation: error = Buy(a, (StationKind)b); break;
                case CommandKind.AbandonOrder: error = Abandon(); break;
                default: error = "未知命令"; break;
            }
            if (error != null) { pendingEvents = null; return Failure(error); }
            commands.Add(new CommandRecord { Id = id, Kind = kind, A = a, B = b, Flag = flag });
            appliedIds.Add(id);
            lastEvents = pendingEvents.ToArray();
            pendingEvents = null;
            return new CommandResult { Success = true, Reason = "完成", Events = CopyEvents(lastEvents) };
        }

        private static CommandResult Failure(string reason)
        {
            return new CommandResult { Success = false, Reason = reason, Events = new GameEvent[0] };
        }

        private string PrepareError()
        {
            return CanPrepare ? null : "仅准备阶段或圈边界可以修改、投料和清洗";
        }

        private string Start()
        {
            if (!CanPrepare) return "当前不是准备窗口";
            EventKind kind = state.Phase == Phase.Preparation ? EventKind.RoundStarted : EventKind.RoundResumed;
            state.Phase = Phase.Running;
            Emit(kind, -1, -1, 0, "标记与配置冻结，开始逐步传送");
            return null;
        }

        private string Load()
        {
            string error = TransactionWindowError();
            if (error != null) return error;
            if (FindRingPlate(Rules.EntrySlot) != null) return "入口已有盘，不能覆盖";
            PlateState plate = FindInventory(PlateLocation.CleanInventory);
            if (plate == null) return "没有干净盘；脏盘须先清洗";
            if (state.Wallet < Rules.IngredientCost) return "原料资金不足";
            state.Wallet -= Rules.IngredientCost;
            state.IngredientSpent += Rules.IngredientCost;
            state.LoadedPortions++;
            plate.Location = PlateLocation.Ring;
            plate.Slot = Rules.EntrySlot;
            plate.Food = FoodState.RiceBase;
            plate.AmountUnits = Rules.WholeAmountUnits;
            plate.Deliverable = true;
            plate.BaseSaleRequested = false;
            Emit(EventKind.IngredientLoaded, plate.Slot, plate.Id, Rules.IngredientCost, "原料套餐已支付，默认可交付");
            return null;
        }

        private string Mark(int plateId, bool value, bool baseSale)
        {
            string error = PrepareError();
            if (error != null) return error;
            PlateState plate = FindPlate(plateId);
            if (plate == null || (plate.Location != PlateLocation.Ring && plate.Location != PlateLocation.Staged)) return "所选盘没有料理";
            if (baseSale ? plate.BaseSaleRequested == value : plate.Deliverable == value) return "标记未改变";
            if (baseSale) plate.BaseSaleRequested = value; else plate.Deliverable = value;
            Emit(baseSale ? EventKind.BaseSaleMarked : EventKind.PlateMarked, plate.Slot, plate.Id, value ? 1 : 0,
                baseSale ? (value ? "顾客点安排基础出售" : "取消基础出售") : (value ? "允许匹配订单取走" : "保留，匹配订单也不取走"));
            return null;
        }

        private string Wash()
        {
            string error = TransactionWindowError();
            if (error != null) return error;
            PlateState plate = FindInventory(PlateLocation.DirtyInventory);
            if (plate == null) return "没有脏盘";
            if (state.Wallet < Rules.WashCost) return "清洗资金不足";
            state.Wallet -= Rules.WashCost;
            state.WashSpent += Rules.WashCost;
            plate.Location = PlateLocation.CleanInventory;
            Emit(EventKind.PlateWashed, -1, plate.Id, Rules.WashCost, "付费清洗后恢复干净盘库存");
            return null;
        }

        private string TransactionWindowError()
        {
            return state.Phase == Phase.Finished ? "本轮已经结算，不能再次投料或清洗" : null;
        }

        private string StationChangeError()
        {
            string error = PrepareError();
            if (error != null) return error;
            return state.AdjustmentsLeft > 0 ? null : "本轮改站次数已用完";
        }

        private string Move(int source, int target)
        {
            string error = StationChangeError();
            if (error != null) return error;
            if (!ValidSlot(source) || !ValidSlot(target) || source == target || state.Stations[source] == StationKind.None) return "请选择有效的来源站与不同目标槽";
            if (state.Wallet < Rules.MoveStationCost) return "改站资金不足";
            StationKind other = state.Stations[target];
            state.Stations[target] = state.Stations[source];
            state.Stations[source] = other;
            state.Wallet -= Rules.MoveStationCost;
            state.MoveSpent += Rules.MoveStationCost;
            state.AdjustmentsLeft--;
            Emit(EventKind.StationMoved, target, -1, Rules.MoveStationCost, other == StationKind.None ? "工站移入空位" : "两工站原子交换");
            return null;
        }

        private string Sell(int slot)
        {
            string error = StationChangeError();
            if (error != null) return error;
            if (!ValidSlot(slot) || state.Stations[slot] == StationKind.None) return "该槽没有可转卖工站";
            StationKind kind = state.Stations[slot];
            state.Stations[slot] = StationKind.None;
            state.Wallet += Rules.SellStationPrice;
            state.StationSaleReceipts += Rules.SellStationPrice;
            state.AdjustmentsLeft--;
            Emit(EventKind.StationSold, slot, -1, Rules.SellStationPrice, Rules.StationName(kind) + "转卖；收入不计基础经营目标");
            return null;
        }

        private string Buy(int slot, StationKind kind)
        {
            string error = StationChangeError();
            if (error != null) return error;
            if (!ValidSlot(slot) || state.Stations[slot] != StationKind.None || kind == StationKind.None || !Enum.IsDefined(typeof(StationKind), kind)) return "购置需要空工位与有效类型";
            foreach (StationKind existing in state.Stations) if (existing == kind) return "每种工站最多一件，已有工站可挪动";
            if (state.Wallet < Rules.BuyStationCost) return "购置资金不足";
            state.Stations[slot] = kind;
            state.Wallet -= Rules.BuyStationCost;
            state.StationPurchaseSpent += Rules.BuyStationCost;
            state.AdjustmentsLeft--;
            Emit(EventKind.StationBought, slot, -1, Rules.BuyStationCost, "购入" + Rules.StationName(kind));
            return null;
        }

        private string Abandon()
        {
            string error = PrepareError();
            if (error != null) return error;
            if (state.Order.Status != OrderStatus.Waiting) return "当前没有可弃订单";
            if (state.Wallet < Rules.AbandonOrderCost) return "弃单违约费不足";
            state.Wallet -= Rules.AbandonOrderCost;
            state.AbandonSpent += Rules.AbandonOrderCost;
            state.Order.Status = OrderStatus.Abandoned;
            state.Order.RemainingWait = 0;
            Emit(EventKind.OrderAbandoned, Rules.CustomerSlot, -1, Rules.AbandonOrderCost, "放弃高价合同，仍可安排基础出售");
            return null;
        }

        private string Advance()
        {
            if (state.Phase != Phase.Running || state.Step >= state.StepLimit) return "需先确认准备窗口，或本轮已结束";
            state.Step++;
            Emit(EventKind.StepStarted, -1, -1, 0, "先移动原有盘，再加工、交付和离席");
            var moved = new List<PlateState>();
            foreach (PlateState plate in state.Plates)
            {
                if (plate.Location != PlateLocation.Ring) continue;
                plate.Slot = Rules.NextSlot(plate.Slot);
                moved.Add(plate);
                Emit(EventKind.PlateMoved, plate.Slot, plate.Id, 0, "顺时针前进一槽");
            }
            // A previous split's staged plate enters now, but is not in moved and cannot process again this step.
            PlateState staged = FindInventory(PlateLocation.Staged);
            if (staged != null)
            {
                if (FindRingPlate(Rules.EntrySlot) == null)
                {
                    staged.Location = PlateLocation.Ring;
                    staged.Slot = Rules.EntrySlot;
                    Emit(EventKind.StagedEntered, staged.Slot, staged.Id, 0, "上步暂存小份进入；本步不加工");
                }
                else Emit(EventKind.EntryBlocked, Rules.EntrySlot, staged.Id, 0, "入口被原有盘占用；暂存盘等待，不覆盖");
            }
            moved.Sort(delegate(PlateState a, PlateState b) { int slot = a.Slot.CompareTo(b.Slot); return slot != 0 ? slot : a.Id.CompareTo(b.Id); });
            foreach (PlateState plate in moved) Process(plate);
            PlateState customerPlate = FindRingPlate(Rules.CustomerSlot);
            if (customerPlate != null) VisitCustomer(customerPlate);
            if (state.Order.Status != OrderStatus.Abandoned)
            {
                int wait = state.Order.Kind == OrderKind.WholeHeated ? Rules.WholeOrderWait : Rules.SmallOrderWait;
                state.Order.RemainingWait = Math.Max(0, wait - state.Step);
                if (state.Order.Status == OrderStatus.Waiting && state.Order.RemainingWait == 0)
                {
                    state.Order.Status = OrderStatus.Expired;
                    Emit(EventKind.OrderDeparted, Rules.CustomerSlot, -1, 0, "当步交付已检查，未完成订单离席");
                }
            }
            if (state.Step == state.StepLimit)
            {
                state.Phase = Phase.Finished;
                state.Outcome = state.Order.Status == OrderStatus.Fulfilled ? RoundOutcome.OrderCompleted
                    : state.BaseReceipts >= Rules.BaseTradeTarget ? RoundOutcome.BaseTradeCompleted : RoundOutcome.Failed;
                Emit(EventKind.RoundFinished, -1, -1, state.Wallet,
                    state.Outcome == RoundOutcome.OrderCompleted ? "订单成功；固定步限结束" : state.Outcome == RoundOutcome.BaseTradeCompleted ? "基础经营补救成功，未完成主订单" : "未完成订单且基础收入不足；查看拒收、阻塞和离席记录");
            }
            else if (state.Step % Rules.StepsPerCircle == 0)
            {
                state.Phase = Phase.CircleBoundary;
                Emit(EventKind.CircleOpened, -1, -1, 0, "圈边界：允许调标、付费改站、投料与清洗");
            }
            return null;
        }

        private void Process(PlateState plate)
        {
            StationKind station = state.Stations[plate.Slot];
            if (station == StationKind.None) return;
            FoodState before = plate.Food;
            switch (station)
            {
                case StationKind.Filling:
                    if (plate.Food == FoodState.RiceBase) plate.Food = FoodState.FilledRice;
                    break;
                case StationKind.Rolling:
                    if (plate.Food == FoodState.FilledRice) plate.Food = FoodState.Roll;
                    break;
                case StationKind.Heating:
                    if (plate.Food == FoodState.Roll) plate.Food = FoodState.HeatedRoll;
                    else if (plate.Food == FoodState.SmallRoll) plate.Food = FoodState.SmallHeatedRoll;
                    break;
                case StationKind.Cutting:
                    if ((plate.Food == FoodState.Roll || plate.Food == FoodState.HeatedRoll) && plate.AmountUnits == Rules.WholeAmountUnits)
                    {
                        PlateState clean = FindInventory(PlateLocation.CleanInventory);
                        if (clean == null) { Emit(EventKind.SplitBlocked, plate.Slot, plate.Id, 0, "没有额外干净盘；原料理和食材量不变"); return; }
                        if (FindInventory(PlateLocation.Staged) != null || RingCount() >= Rules.SlotCount)
                        { Emit(EventKind.SplitBlocked, plate.Slot, plate.Id, 0, "暂存入口容量不足；原料理和库存不变"); return; }
                        plate.Food = plate.Food == FoodState.Roll ? FoodState.SmallRoll : FoodState.SmallHeatedRoll;
                        plate.AmountUnits = Rules.SmallAmountUnits;
                        clean.Location = PlateLocation.Staged;
                        clean.Slot = -1;
                        clean.Food = plate.Food;
                        clean.AmountUnits = Rules.SmallAmountUnits;
                        clean.Deliverable = plate.Deliverable;
                        clean.BaseSaleRequested = plate.BaseSaleRequested;
                        Emit(EventKind.FoodSplit, plate.Slot, plate.Id, Rules.SmallAmountUnits, "整份分到两盘；总量2，新盘暂存至下一步");
                        return;
                    }
                    break;
            }
            if (before != plate.Food) Emit(EventKind.FoodProcessed, plate.Slot, plate.Id, 0, Rules.FoodName(before) + "→" + Rules.FoodName(plate.Food));
            else Emit(EventKind.ProcessingSkipped, plate.Slot, plate.Id, 0, Rules.StationName(station) + "不接受" + Rules.FoodName(plate.Food));
        }

        private void VisitCustomer(PlateState plate)
        {
            if (state.Order.Status == OrderStatus.Waiting && plate.Deliverable && plate.Food == state.Order.RequiredFood && plate.AmountUnits == state.Order.RequiredAmountUnits)
            {
                int units = plate.AmountUnits;
                int price = state.Order.UnitPrice;
                state.Order.DeliveredQuantity++;
                state.DeliveredAmountUnits += units;
                state.OrderReceipts += price;
                state.Wallet += price;
                MakeDirty(plate);
                Emit(EventKind.OrderDelivered, Rules.CustomerSlot, plate.Id, price, "订单接收匹配份量，食材交付，实体盘变脏");
                if (state.Order.DeliveredQuantity == state.Order.RequiredQuantity)
                {
                    state.Order.Status = OrderStatus.Fulfilled;
                    Emit(EventKind.OrderFulfilled, Rules.CustomerSlot, -1, 0, "本轮唯一主订单完成，不额外重复发奖");
                }
                return;
            }
            string reason = state.Order.Status != OrderStatus.Waiting ? "顾客订单已完成、离席或放弃"
                : !plate.Deliverable ? "盘标为保留；匹配也不交付"
                : plate.AmountUnits != state.Order.RequiredAmountUnits ? "份量不匹配"
                : "料理状态不匹配：需要" + Rules.FoodName(state.Order.RequiredFood);
            Emit(EventKind.OrderRejected, Rules.CustomerSlot, plate.Id, 0, reason);
            if (!plate.BaseSaleRequested) return;
            int saleUnits = plate.AmountUnits;
            int sale = saleUnits * Rules.BaseSalePerAmountUnit;
            state.BaseSoldAmountUnits += saleUnits;
            state.BaseReceipts += sale;
            state.Wallet += sale;
            MakeDirty(plate);
            Emit(EventKind.BaseSold, Rules.CustomerSlot, plate.Id, sale, "已安排基础出售；按食材量收款，盘需清洗");
        }

        private static void MakeDirty(PlateState plate)
        {
            plate.Location = PlateLocation.DirtyInventory;
            plate.Slot = -1;
            plate.Food = FoodState.RiceBase;
            plate.AmountUnits = 0;
            plate.Deliverable = false;
            plate.BaseSaleRequested = false;
        }

        private void Emit(EventKind kind, int slot, int plateId, int amount, string reason)
        {
            var item = new GameEvent { Sequence = events.Count + 1, Step = state.Step, Kind = kind, Slot = slot, PlateId = plateId, Amount = amount, Reason = reason };
            events.Add(item);
            pendingEvents.Add(item);
        }

        private static bool ValidSlot(int slot) { return slot >= 0 && slot < Rules.SlotCount; }
        private PlateState FindPlate(int id) { return id >= 0 && id < state.Plates.Length ? state.Plates[id] : null; }
        private PlateState FindRingPlate(int slot)
        {
            foreach (PlateState plate in state.Plates) if (plate.Location == PlateLocation.Ring && plate.Slot == slot) return plate;
            return null;
        }
        private PlateState FindInventory(PlateLocation location)
        {
            foreach (PlateState plate in state.Plates) if (plate.Location == location) return plate;
            return null;
        }
        private int RingCount() { int count = 0; foreach (PlateState plate in state.Plates) if (plate.Location == PlateLocation.Ring) count++; return count; }

        public RoundSnapshot ExportSnapshot()
        {
            var copy = new RoundSnapshot
            {
                SchemaVersion = state.SchemaVersion, RulesVersion = state.RulesVersion,
                Phase = state.Phase, Step = state.Step, StepLimit = state.StepLimit,
                Wallet = state.Wallet, AdjustmentsLeft = state.AdjustmentsLeft, Outcome = state.Outcome,
                Plates = new PlateState[state.Plates.Length], Stations = (StationKind[])state.Stations.Clone(), Order = state.Order.Copy(),
                LoadedPortions = state.LoadedPortions, DeliveredAmountUnits = state.DeliveredAmountUnits,
                BaseSoldAmountUnits = state.BaseSoldAmountUnits, OrderReceipts = state.OrderReceipts, BaseReceipts = state.BaseReceipts,
                IngredientSpent = state.IngredientSpent, WashSpent = state.WashSpent, MoveSpent = state.MoveSpent,
                StationPurchaseSpent = state.StationPurchaseSpent, StationSaleReceipts = state.StationSaleReceipts, AbandonSpent = state.AbandonSpent,
                Commands = new CommandRecord[commands.Count], Events = CopyEvents(events.ToArray())
            };
            for (int i = 0; i < copy.Plates.Length; i++) copy.Plates[i] = state.Plates[i].Copy();
            for (int i = 0; i < copy.Commands.Length; i++) copy.Commands[i] = commands[i].Copy();
            return copy;
        }

        private static GameEvent[] CopyEvents(GameEvent[] source)
        {
            var copy = new GameEvent[source.Length];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i].Copy();
            return copy;
        }

        public static bool TryRestore(RoundSnapshot snapshot, out Session restored, out string reason)
        {
            restored = null;
            if (!ValidateShape(snapshot, out reason)) return false;
            var candidate = new Session(snapshot.Order.Kind);
            foreach (CommandRecord command in snapshot.Commands)
            {
                if (command == null || !Enum.IsDefined(typeof(CommandKind), command.Kind) || string.IsNullOrWhiteSpace(command.Id))
                { reason = "无效命令记录"; return false; }
                CommandResult result = candidate.Execute(command.Kind, command.A, command.B, command.Flag, command.Id);
                if (!result.Success || result.AlreadyApplied) { reason = "命令重放失败或编号重复：" + result.Reason; return false; }
            }
            if (!Equivalent(snapshot, candidate.ExportSnapshot())) { reason = "快照与生产命令重放不一致，拒绝免费收益或恢复旧步骤"; return false; }
            restored = candidate;
            // Restore is data recovery, not a freshly played command or reward event.
            restored.lastEvents = new GameEvent[0];
            reason = "有效安全检查点";
            return true;
        }

        public static bool ValidateSnapshot(RoundSnapshot snapshot, out string reason)
        {
            Session ignored;
            return TryRestore(snapshot, out ignored, out reason);
        }

        private static bool ValidateShape(RoundSnapshot s, out string reason)
        {
            reason = "无效快照结构、版本或范围";
            if (s == null || s.SchemaVersion != Rules.SnapshotSchemaVersion || s.RulesVersion != Rules.Version
                || s.Plates == null || s.Plates.Length != Rules.PlateCount || s.Stations == null || s.Stations.Length != Rules.SlotCount
                || s.Order == null || !Enum.IsDefined(typeof(OrderKind), s.Order.Kind)
                || s.Commands == null || s.Commands.Length > Rules.MaximumRecordedCommands || s.Events == null || s.Events.Length > 100000
                || !Enum.IsDefined(typeof(Phase), s.Phase) || !Enum.IsDefined(typeof(RoundOutcome), s.Outcome)
                || s.StepLimit != Rules.StepLimit || s.Step < 0 || s.Step > Rules.StepLimit || s.Wallet < 0
                || s.AdjustmentsLeft < 0 || s.AdjustmentsLeft > Rules.AdjustmentsPerRound) return false;
            if ((s.Phase == Phase.Preparation && s.Step != 0)
                || (s.Phase == Phase.CircleBoundary && (s.Step == 0 || s.Step >= Rules.StepLimit || s.Step % Rules.StepsPerCircle != 0))
                || (s.Phase == Phase.Finished && s.Step != Rules.StepLimit)
                || (s.Phase != Phase.Finished && (s.Step == Rules.StepLimit || s.Outcome != RoundOutcome.None))) return false;
            var slots = new HashSet<int>();
            int stagedCount = 0;
            for (int i = 0; i < s.Plates.Length; i++)
            {
                PlateState plate = s.Plates[i];
                if (plate == null || plate.Id != i || !Enum.IsDefined(typeof(PlateLocation), plate.Location) || !Enum.IsDefined(typeof(FoodState), plate.Food)) return false;
                bool hasFood = plate.Location == PlateLocation.Ring || plate.Location == PlateLocation.Staged;
                if (hasFood)
                {
                    bool small = plate.Food == FoodState.SmallRoll || plate.Food == FoodState.SmallHeatedRoll;
                    if (plate.AmountUnits != (small ? Rules.SmallAmountUnits : Rules.WholeAmountUnits)) return false;
                    if (plate.Location == PlateLocation.Ring && (!ValidSlot(plate.Slot) || !slots.Add(plate.Slot))) return false;
                    if (plate.Location == PlateLocation.Staged && (plate.Slot != -1 || !small || ++stagedCount > 1)) return false;
                }
                else if (plate.Slot != -1 || plate.AmountUnits != 0 || plate.Food != FoodState.RiceBase || plate.Deliverable || plate.BaseSaleRequested) return false;
            }
            var kinds = new HashSet<StationKind>();
            foreach (StationKind station in s.Stations)
                if (!Enum.IsDefined(typeof(StationKind), station) || (station != StationKind.None && !kinds.Add(station))) return false;
            foreach (GameEvent item in s.Events) if (item == null || item.Reason == null) return false;
            if (s.LoadedPortions < 0 || s.DeliveredAmountUnits < 0 || s.BaseSoldAmountUnits < 0 || s.OrderReceipts < 0 || s.BaseReceipts < 0
                || s.IngredientSpent < 0 || s.WashSpent < 0 || s.MoveSpent < 0 || s.StationPurchaseSpent < 0 || s.StationSaleReceipts < 0 || s.AbandonSpent < 0) return false;
            long expectedWallet = (long)Rules.InitialWallet + s.OrderReceipts + s.BaseReceipts + s.StationSaleReceipts
                - s.IngredientSpent - s.WashSpent - s.MoveSpent - s.StationPurchaseSpent - s.AbandonSpent;
            if (expectedWallet != s.Wallet || (long)s.LoadedPortions * Rules.WholeAmountUnits != (long)s.FoodUnitsInSystem + s.DeliveredAmountUnits + s.BaseSoldAmountUnits) return false;
            reason = "结构有效，继续检查生产记录";
            return true;
        }

        private static bool Equivalent(RoundSnapshot a, RoundSnapshot b)
        {
            if (a.SchemaVersion != b.SchemaVersion || a.RulesVersion != b.RulesVersion || a.Phase != b.Phase || a.Step != b.Step
                || a.StepLimit != b.StepLimit || a.Wallet != b.Wallet || a.AdjustmentsLeft != b.AdjustmentsLeft || a.Outcome != b.Outcome
                || a.LoadedPortions != b.LoadedPortions || a.DeliveredAmountUnits != b.DeliveredAmountUnits || a.BaseSoldAmountUnits != b.BaseSoldAmountUnits
                || a.OrderReceipts != b.OrderReceipts || a.BaseReceipts != b.BaseReceipts || a.IngredientSpent != b.IngredientSpent
                || a.WashSpent != b.WashSpent || a.MoveSpent != b.MoveSpent || a.StationPurchaseSpent != b.StationPurchaseSpent
                || a.StationSaleReceipts != b.StationSaleReceipts || a.AbandonSpent != b.AbandonSpent
                || a.Commands.Length != b.Commands.Length || a.Events.Length != b.Events.Length) return false;
            OrderState x = a.Order, y = b.Order;
            if (x.Kind != y.Kind || x.RequiredFood != y.RequiredFood || x.RequiredAmountUnits != y.RequiredAmountUnits
                || x.RequiredQuantity != y.RequiredQuantity || x.DeliveredQuantity != y.DeliveredQuantity
                || x.RemainingWait != y.RemainingWait || x.UnitPrice != y.UnitPrice || x.Status != y.Status) return false;
            for (int i = 0; i < a.Plates.Length; i++)
            {
                PlateState p = a.Plates[i], q = b.Plates[i];
                if (p.Id != q.Id || p.Location != q.Location || p.Slot != q.Slot || p.Food != q.Food || p.AmountUnits != q.AmountUnits
                    || p.Deliverable != q.Deliverable || p.BaseSaleRequested != q.BaseSaleRequested || a.Stations[i] != b.Stations[i]) return false;
            }
            for (int i = 0; i < a.Commands.Length; i++)
            {
                CommandRecord p = a.Commands[i], q = b.Commands[i];
                if (p.Id != q.Id || p.Kind != q.Kind || p.A != q.A || p.B != q.B || p.Flag != q.Flag) return false;
            }
            for (int i = 0; i < a.Events.Length; i++)
            {
                GameEvent p = a.Events[i], q = b.Events[i];
                if (p.Sequence != q.Sequence || p.Step != q.Step || p.Kind != q.Kind || p.Slot != q.Slot || p.PlateId != q.PlateId || p.Amount != q.Amount || p.Reason != q.Reason) return false;
            }
            return true;
        }
    }
}
