using System;
using System.Collections.Generic;
using RingToss.Core;

namespace RingToss.Application
{
    public enum StagePhase { Preparation, Aiming, Flying, AwaitingDisposition, BetweenThrows, Ended }
    public enum StageEventKind { Opened, Launched, Cashed, Retained, Salvaged, Adjusted, RescueBought, Settled }
    public sealed class StageEvent
    {
        public readonly StageEventKind Kind;
        public readonly string ItemId;
        public readonly int WalletDelta, ReceiptDelta;
        public StageEvent(StageEventKind kind, string itemId, int walletDelta = 0, int receiptDelta = 0)
        { Kind = kind; ItemId = itemId; WalletDelta = walletDelta; ReceiptDelta = receiptDelta; }
    }
    public sealed class CommandResult
    {
        public readonly bool Success, AlreadyApplied;
        public readonly string Message;
        public readonly StageEvent Event;
        public CommandResult(bool success, string message, StageEvent stageEvent = null, bool alreadyApplied = false)
        { Success = success; Message = message; Event = stageEvent; AlreadyApplied = alreadyApplied; }
    }

    // One P0 stall. Persistence and the multi-stall run belong to the host adapter.
    public sealed class StageSession
    {
        private SlotObject[] slots;
        private readonly Dictionary<string, CommandResult> commands = new Dictionary<string, CommandResult>(StringComparer.Ordinal);
        private SlotObject[] pendingInitialSlots, replayInitialSlots;
        private ThrowInput pendingInput;
        private ThrowInput3D pendingInput3D;
        private IStageFlight lastSimulation;
        private int baseRemaining = Rules.BaseRings, rescueRemaining;
        private bool rescueBought;
        public string StageId { get; private set; }
        public StagePhase Phase { get; private set; }
        public int Wallet { get; private set; }
        public int StageReceipts { get; private set; }
        public int Target { get; private set; }
        public int RingsRemaining { get { return baseRemaining + rescueRemaining; } }
        public int AdjustmentsRemaining { get; private set; }
        public Flight ActiveFlight { get; private set; }
        public Flight LastFlight { get; private set; }
        public Flight3D ActiveFlight3D { get; private set; }
        public Flight3D LastFlight3D { get; private set; }
        public bool IsThreeDimensional { get; private set; }
        public ThrowInput LastInput { get; private set; }
        public ThrowInput3D LastInput3D { get; private set; }
        public int LastThrowNumber { get; private set; }
        public bool IsSuccess { get; private set; }
        public IReadOnlyList<SlotObject> Slots { get { return ActiveFlight3D != null ? ActiveFlight3D.Slots : ActiveFlight != null ? ActiveFlight.Slots : Array.AsReadOnly(slots); } }
        public int TheoreticalMaxReceipts
        {
            get
            {
                int value = StageReceipts;
                foreach (SlotObject o in slots)
                {
                    if (o == null) continue;
                    if (o.Occupancy == Occupancy.Prize) value += o.PrizeValue;
                    else if (o.SourceStage == StageId && o.ReceiptEligible) value += o.PrizeValue / 2;
                }
                return value;
            }
        }
        public bool CanBuyRescue
        {
            get
            {
                if (!CanAct || lastSimulation == null || rescueBought || Wallet < Rules.RescuePrice || TheoreticalMaxReceipts < Target) return false;
                foreach (SlotObject o in slots) if (o != null && o.Occupancy == Occupancy.Prize) return true;
                return false;
            }
        }
        private bool CanAct { get { return Phase == StagePhase.Aiming || Phase == StagePhase.BetweenThrows; } }
        public static StageSession CreatePrototype(string runId)
        { return CreatePrototypeForMode(runId, false); }
        public static StageSession CreatePrototype3D(string runId)
        { return CreatePrototypeForMode(runId, true); }
        private static StageSession CreatePrototypeForMode(string runId, bool threeDimensional)
        {
            if (string.IsNullOrEmpty(runId)) throw new ArgumentException("Missing run ID.");
            string stageId = runId + "/stage-01";
            var scene = new SlotObject[6];
            for (int i = 0; i < 6; i++)
            {
                ObjectKind kind = i % 2 == 0 ? ObjectKind.Fan : ObjectKind.Board;
                scene[i] = new SlotObject(stageId + "/S" + (i + 1) + "/0", i, kind, Occupancy.Prize,
                    0, kind == ObjectKind.Fan ? 0 : 45, false, Rules.Price(kind), stageId, true);
            }
            return new StageSession(stageId, scene, 0, threeDimensional);
        }
        public StageSession(string stageId, SlotObject[] initialSlots, int initialWallet = 0, bool threeDimensional = false)
        {
            if (string.IsNullOrEmpty(stageId) || initialWallet < 0 || initialSlots == null || initialSlots.Length != 6) throw new ArgumentException("Invalid stage.");
            slots = (SlotObject[])initialSlots.Clone();
            var ids = new HashSet<string>(StringComparer.Ordinal); int oldCount = 0, freshCount = 0, value = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                SlotObject o = slots[i]; if (o == null) continue;
                if (o.SlotIndex != i || !ids.Add(o.Id)) throw new ArgumentException("Invalid slot identity.");
                if (o.Occupancy == Occupancy.Prize)
                {
                    if (o.SourceStage != stageId || !o.ReceiptEligible || o.PrizeValue != Rules.Price(o.Kind)) throw new ArgumentException("Invalid fresh prize.");
                    value += o.PrizeValue;
                    freshCount++;
                }
                else
                {
                    if (o.Durability == 0 || o.SourceStage == stageId) throw new ArgumentException("Invalid preinstalled item.");
                    oldCount++;
                }
            }
            if (oldCount > 2 || freshCount + oldCount != 6) throw new ArgumentException("Six candidates are required, with at most two preinstalled items.");
            StageId = stageId; Wallet = initialWallet; Phase = StagePhase.Preparation; IsThreeDimensional = threeDimensional;
            AdjustmentsRemaining = Rules.Adjustments;
            Target = 5 * (value / 10); // alpha=0.5 with the chapter-one five-coin floor.
        }
        private CommandResult Prior(string id)
        {
            if (string.IsNullOrEmpty(id)) return Fail("缺少事务编号。");
            CommandResult r;
            return commands.TryGetValue(id, out r) ? new CommandResult(true, "此操作已经处理。", null, true) : null;
        }
        private CommandResult Commit(string id, string message, StageEvent e)
        { var r = new CommandResult(true, message, e); commands.Add(id, r); return r; }
        private static CommandResult Fail(string message) { return new CommandResult(false, message); }
        public CommandResult OpenStage(string tx)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (Phase != StagePhase.Preparation) return Fail("本摊已经开摊。");
            Phase = StagePhase.Aiming;
            return Commit(tx, "开摊：目标和布置已冻结。", new StageEvent(StageEventKind.Opened, null));
        }
        public CommandResult Launch(ThrowInput input, string tx)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (IsThreeDimensional) return Fail("本摊使用三维投掷输入。");
            if (!CanAct || RingsRemaining <= 0) return Fail("当前不能投掷。");
            // Reject default(struct), which bypasses ThrowInput's constructor.
            if (input.AngleDegrees < 20 || input.AngleDegrees > 75 || input.Speed < 4 || input.Speed > 12) return Fail("投掷输入无效。");
            pendingInput = input;
            ActiveFlight = new Flight(input, slots);
            BeginThrow();
            return Commit(tx, "已投出一个圈。", new StageEvent(StageEventKind.Launched, null));
        }
        public CommandResult Launch3D(ThrowInput3D input, string tx)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (!IsThreeDimensional) return Fail("本摊使用二维投掷输入。");
            if (!CanAct || RingsRemaining <= 0) return Fail("当前不能投掷。");
            if (input.AzimuthDegrees < -30 || input.AzimuthDegrees > 30 || input.ElevationDegrees < 20 || input.ElevationDegrees > 75 || input.Speed < 4 || input.Speed > 12) return Fail("三维投掷输入无效。");
            pendingInput3D = input; ActiveFlight3D = new Flight3D(input, slots); BeginThrow();
            return Commit(tx, "已向摊位投出一个圈。", new StageEvent(StageEventKind.Launched, null));
        }
        private void BeginThrow()
        {
            pendingInitialSlots = (SlotObject[])slots.Clone();
            if (baseRemaining > 0) baseRemaining--; else rescueRemaining--;
            LastThrowNumber++; Phase = StagePhase.Flying;
        }
        public void Step()
        {
            IStageFlight flight = IsThreeDimensional ? (IStageFlight)ActiveFlight3D : ActiveFlight;
            if (Phase != StagePhase.Flying || flight == null) return;
            flight.Step(); if (flight.State == FlightState.Flying) return;
            lastSimulation = flight; replayInitialSlots = pendingInitialSlots; slots = flight.CopySlots();
            if (IsThreeDimensional) { LastFlight3D = ActiveFlight3D; LastInput3D = pendingInput3D; ActiveFlight3D = null; }
            else { LastFlight = ActiveFlight; LastInput = pendingInput; ActiveFlight = null; }
            Phase = flight.State == FlightState.HitPrize ? StagePhase.AwaitingDisposition : StagePhase.BetweenThrows;
        }
        public CommandResult Cash(string tx) { return Dispose(tx, false); }
        public CommandResult Retain(string tx) { return Dispose(tx, true); }
        private CommandResult Dispose(string tx, bool retain)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (Phase != StagePhase.AwaitingDisposition || lastSimulation == null) return Fail("没有待处置奖品。");
            int index = lastSimulation.HitSlotIndex; SlotObject o = slots[index];
            if (o == null || o.Occupancy != Occupancy.Prize) return Fail("奖品已经处置。");
            int amount = retain ? 0 : o.PrizeValue;
            if (retain) slots[index] = o.Retained(); else slots[index] = null;
            Wallet += amount; StageReceipts += amount; Phase = StagePhase.BetweenThrows;
            return Commit(tx, retain ? "留场：本次回款不增加。" : "已兑现回款。",
                new StageEvent(retain ? StageEventKind.Retained : StageEventKind.Cashed, o.Id, amount, amount));
        }
        private bool ValidMechanism(int index)
        { return index >= 0 && index < 6 && slots[index] != null && slots[index].Occupancy == Occupancy.Mechanism; }
        private bool CanAdjust { get { return Phase == StagePhase.Preparation || (CanAct && AdjustmentsRemaining > 0); } }
        public CommandResult Toggle(int index, string tx)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (!CanAdjust || !ValidMechanism(index)) return Fail("无法调整这件机关。");
            SlotObject o = slots[index]; slots[index] = o.With(index, o.Durability, o.AngleDegrees, !o.Enabled, o.Occupancy);
            return Adjusted(tx, o.Id);
        }
        public CommandResult Rotate(int index, double angle, string tx)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (!CanAdjust || !ValidMechanism(index) || !Rules.ValidAngle(slots[index].Kind, angle)) return Fail("朝向或调整资格无效。");
            if (((slots[index].AngleDegrees % 360) + 360) % 360 == ((angle % 360) + 360) % 360) return Fail("朝向没有改变。");
            SlotObject o = slots[index]; slots[index] = o.With(index, o.Durability, angle, o.Enabled, o.Occupancy);
            return Adjusted(tx, o.Id);
        }
        public CommandResult Move(int from, int to, string tx)
        { return Move(from, to, ValidMechanism(from) ? slots[from].AngleDegrees : 0, tx); }
        // A relocation can include its destination direction in one adjustment.
        public CommandResult Move(int from, int to, double angle, string tx)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (!CanAdjust || !ValidMechanism(from) || to < 0 || to >= 6 || slots[to] != null || !Rules.ValidAngle(slots[from].Kind, angle)) return Fail("只能以合法朝向移入空槽。");
            SlotObject o = slots[from]; slots[to] = o.With(to, o.Durability, angle, o.Enabled, o.Occupancy); slots[from] = null;
            return Adjusted(tx, o.Id);
        }
        public CommandResult Swap(int a, int b, string tx)
        { return Swap(a, b, ValidMechanism(a) ? slots[a].AngleDegrees : 0, ValidMechanism(b) ? slots[b].AngleDegrees : 0, tx); }
        public CommandResult Swap(int a, int b, double angleForA, double angleForB, string tx)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (!CanAdjust || a == b || !ValidMechanism(a) || !ValidMechanism(b) ||
                !Rules.ValidAngle(slots[a].Kind, angleForA) || !Rules.ValidAngle(slots[b].Kind, angleForB)) return Fail("只能以合法朝向交换两件机关。");
            SlotObject x = slots[a], y = slots[b];
            slots[a] = y.With(a, y.Durability, angleForB, y.Enabled, y.Occupancy);
            slots[b] = x.With(b, x.Durability, angleForA, x.Enabled, x.Occupancy);
            return Adjusted(tx, x.Id);
        }
        private CommandResult Adjusted(string tx, string itemId)
        {
            bool free = Phase == StagePhase.Preparation;
            if (!free) AdjustmentsRemaining--;
            return Commit(tx, free ? "准备布置已调整，不扣次数。" : "已使用一次机关调整。", new StageEvent(StageEventKind.Adjusted, itemId));
        }
        public CommandResult Salvage(int index, string tx)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (!CanAct || !ValidMechanism(index)) return Fail("无法拆卸这件机关。");
            SlotObject o = slots[index]; int amount = o.PrizeValue / 2, receipt = o.SourceStage == StageId && o.ReceiptEligible ? amount : 0;
            slots[index] = null; Wallet += amount; StageReceipts += receipt;
            return Commit(tx, "已按获取时价格的一半拆卸。", new StageEvent(StageEventKind.Salvaged, o.Id, amount, receipt));
        }
        public CommandResult BuyRescue(string tx)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (!CanBuyRescue) return Fail("当前没有补圈资格或资金。");
            Wallet -= Rules.RescuePrice; rescueRemaining += 2; rescueBought = true;
            return Commit(tx, "购买两个普通圈。", new StageEvent(StageEventKind.RescueBought, null, -Rules.RescuePrice, 0));
        }
        public CommandResult EndStage(string tx)
        {
            var prior = Prior(tx); if (prior != null) return prior;
            if (!CanAct) return Fail("先完成投掷和奖品处置。");
            IsSuccess = StageReceipts >= Target; Phase = StagePhase.Ended;
            return Commit(tx, IsSuccess ? "本摊达标。剩圈不变现。" : "本摊回款不足，本局结束。", new StageEvent(StageEventKind.Settled, null));
        }
        public Flight CreateReplayLastThrow()
        {
            if (IsThreeDimensional || replayInitialSlots == null) throw new InvalidOperationException("No completed 2D throw to replay.");
            return new Flight(LastInput, replayInitialSlots);
        }
        public Flight3D CreateReplayLastThrow3D()
        {
            if (!IsThreeDimensional || replayInitialSlots == null) throw new InvalidOperationException("No completed 3D throw to replay.");
            return new Flight3D(LastInput3D, replayInitialSlots);
        }
    }
}
