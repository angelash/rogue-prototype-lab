using System;
using System.Collections.Generic;

namespace RingToss.Core
{
    public sealed class Flight : IStageFlight
    {
        private readonly SlotObject[] slots;
        private readonly bool[] fanTriggered = new bool[6], boardArmed = new bool[6];
        private readonly int[] boardHits = new int[6];
        private readonly List<PhysicsEvent> events = new List<PhysicsEvent>();
        private int effectCount;
        public IReadOnlyList<SlotObject> Slots { get { return Array.AsReadOnly(slots); } }
        public IReadOnlyList<PhysicsEvent> Events { get { return events.AsReadOnly(); } }
        public Double2 Position { get; private set; }
        public Double2 Velocity { get; private set; }
        public double Time { get; private set; }
        public FlightState State { get; private set; }
        public int HitSlotIndex { get; private set; }
        public MissReason MissReason { get; private set; }
        public Flight(ThrowInput input, SlotObject[] scene)
            : this(scene, Rules.LaunchPoint, InitialVelocity(input)) { }
        private static Double2 InitialVelocity(ThrowInput input)
        {
            if (input.AngleDegrees < 20 || input.AngleDegrees > 75 || input.Speed < 4 || input.Speed > 12) throw new ArgumentException("Invalid throw input.");
            return Rules.Direction(input.AngleDegrees) * input.Speed;
        }
        // Explicit initial state supports geometry golden cases and recovery.
        public Flight(SlotObject[] scene, Double2 position, Double2 velocity)
        {
            if (scene == null || scene.Length != 6) throw new ArgumentException("Exactly six slots required.");
            if (double.IsNaN(position.X) || double.IsInfinity(position.X) || double.IsNaN(position.Y) || double.IsInfinity(position.Y) ||
                double.IsNaN(velocity.X) || double.IsInfinity(velocity.X) || double.IsNaN(velocity.Y) || double.IsInfinity(velocity.Y)) throw new ArgumentException("Finite state required.");
            slots = (SlotObject[])scene.Clone();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 6; i++)
            {
                if (slots[i] != null && (slots[i].SlotIndex != i || !ids.Add(slots[i].Id))) throw new ArgumentException("Invalid slot identity.");
                boardArmed[i] = true;
            }
            Position = position; Velocity = velocity; State = FlightState.Flying; HitSlotIndex = -1;
        }
        public SlotObject[] CopySlots() { return (SlotObject[])slots.Clone(); }
        private enum Contact { Prize, PrizeBody, Fan, Board, Body, Ground, Bounds, Timeout }
        private struct Candidate { public double T; public int Slot; public Contact Kind; public string Id; }
        private Candidate best;
        private void Offer(double t, int slot, Contact kind)
        {
            if (double.IsInfinity(t) || double.IsNaN(t)) return;
            string id = slot < 0 ? "~environment" : slots[slot].Id;
            bool earlier = t < best.T - 1e-11;
            bool same = Math.Abs(t - best.T) <= 1e-11;
            // Root convergence tie tolerance is temporal only, not an acceptance band.
            if (earlier || (same && (string.CompareOrdinal(id, best.Id) < 0 ||
                (id == best.Id && kind == Contact.Prize && best.Kind == Contact.PrizeBody))))
                best = new Candidate { T = t, Slot = slot, Kind = kind, Id = id };
        }
        public void Step()
        {
            if (State != FlightState.Flying) return;
            double remaining = Math.Min(Rules.Dt, Rules.MaxFlightTime - Time);
            while (remaining > 1e-12 && State == FlightState.Flying)
            {
                best = new Candidate { T = double.PositiveInfinity, Slot = -1, Id = "~environment" };
                FindContacts(remaining);
                if (double.IsInfinity(best.T)) { Advance(remaining); remaining = 0; break; }
                double t = Math.Max(0, Math.Min(remaining, best.T));
                Advance(t); remaining -= t;
                Handle(best);
            }
            if (State == FlightState.Flying && Time >= Rules.MaxFlightTime - 1e-11) Miss(global::RingToss.Core.MissReason.TimeLimit, null);
        }
        private void Advance(double t)
        { Position = ContinuousGeometry.Position(Position, Velocity, t); Velocity = ContinuousGeometry.Velocity(Velocity, t); Time += t; }
        private void FindContacts(double end)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                SlotObject o = slots[i]; if (o == null) continue;
                if (o.Occupancy == Occupancy.Prize)
                {
                    foreach (double t in ContinuousGeometry.Plane(Position, Velocity, new Double2(o.X, Rules.AcceptHeight), new Double2(0, 1), end))
                    {
                        Double2 q = ContinuousGeometry.Position(Position, Velocity, t), v = ContinuousGeometry.Velocity(Velocity, t);
                        if (Rules.Accepts(q.X, v.Y, o.X)) Offer(t, i, Contact.Prize);
                    }
                    Offer(ContinuousGeometry.RectangleEntry(Position, Velocity, o.X - Rules.PostRadius, o.X + Rules.PostRadius, 0, Rules.AcceptHeight, end), i, Contact.PrizeBody);
                }
                else if (o.Kind == ObjectKind.Fan)
                {
                    if (o.Enabled && o.Durability > 0 && !fanTriggered[i] && effectCount < Rules.MaxEffects)
                        Offer(ContinuousGeometry.RectangleEntry(Position, Velocity, o.X - 1.2, o.X + 1.2, 0.8, 2.4, end), i, Contact.Fan);
                    Offer(ContinuousGeometry.CircleEntry(Position, Velocity, new Double2(o.X, 0.9), 0.1, end), i, Contact.Body);
                }
                else
                {
                    Double2 center = new Double2(o.X, 1.2), tangent = Rules.Direction(o.AngleDegrees);
                    if (!boardArmed[i] && ContinuousGeometry.SegmentDistance(Position, center, tangent, 0.5) >= Rules.RearmDistance) boardArmed[i] = true;
                    if (!boardArmed[i]) continue;
                    Double2 normal = new Double2(-tangent.Y, tangent.X);
                    foreach (double t in ContinuousGeometry.Plane(Position, Velocity, center, normal, end))
                    {
                        Double2 q = ContinuousGeometry.Position(Position, Velocity, t);
                        if (Math.Abs(Double2.Dot(q - center, tangent)) <= 0.5)
                            Offer(t, i, o.Enabled && o.Durability > 0 && boardHits[i] < 2 && effectCount < Rules.MaxEffects ? Contact.Board : Contact.Body);
                    }
                }
            }
            foreach (double t in ContinuousGeometry.Plane(Position, Velocity, new Double2(0, 0), new Double2(0, 1), end))
                if (ContinuousGeometry.Velocity(Velocity, t).Y <= 0) Offer(t, -1, Contact.Ground);
            foreach (double x in new[] { 0.0, 12.0 })
                foreach (double t in ContinuousGeometry.Plane(Position, Velocity, new Double2(x, 0), new Double2(1, 0), end))
                    if ((x == 0 && Velocity.X < 0) || (x == 12 && Velocity.X > 0)) Offer(t, -1, Contact.Bounds);
            foreach (double t in ContinuousGeometry.Plane(Position, Velocity, new Double2(0, 7), new Double2(0, 1), end))
                if (ContinuousGeometry.Velocity(Velocity, t).Y > 0) Offer(t, -1, Contact.Bounds);
        }
        private void Handle(Candidate c)
        {
            SlotObject o = c.Slot < 0 ? null : slots[c.Slot]; Double2 before = Velocity;
            if (c.Kind == Contact.Prize)
            {
                State = FlightState.HitPrize; HitSlotIndex = c.Slot;
                events.Add(new PhysicsEvent(PhysicsEventKind.PrizeHit, o.Id, Time, Position, before, Velocity, 0)); return;
            }
            if (c.Kind == Contact.Fan || c.Kind == Contact.Board)
            {
                if (c.Kind == Contact.Fan) { fanTriggered[c.Slot] = true; Velocity += Rules.Direction(o.AngleDegrees) * 2; }
                else
                {
                    boardHits[c.Slot]++; boardArmed[c.Slot] = false;
                    Double2 tangent = Rules.Direction(o.AngleDegrees), vt = tangent * Double2.Dot(Velocity, tangent), vn = Velocity - vt;
                    Velocity = vt * 0.95 - vn * 0.8;
                }
                if (Velocity.Length > Rules.MaxSpeed) Velocity = Velocity * (Rules.MaxSpeed / Velocity.Length);
                effectCount++;
                slots[c.Slot] = o.With(o.SlotIndex, o.Durability - 1, o.AngleDegrees, o.Enabled, o.Occupancy);
                events.Add(new PhysicsEvent(c.Kind == Contact.Fan ? PhysicsEventKind.FanImpulse : PhysicsEventKind.BoardBounce,
                    o.Id, Time, Position, before, Velocity, o.Durability - 1)); return;
            }
            Miss(c.Kind == Contact.PrizeBody ? global::RingToss.Core.MissReason.PrizeSide : c.Kind == Contact.Body ? global::RingToss.Core.MissReason.MechanismBody :
                c.Kind == Contact.Ground ? global::RingToss.Core.MissReason.Ground : global::RingToss.Core.MissReason.Bounds, o == null ? null : o.Id);
        }
        private void Miss(MissReason reason, string id)
        { State = FlightState.Missed; MissReason = reason; events.Add(new PhysicsEvent(PhysicsEventKind.Miss, id, Time, Position, Velocity, Velocity, 0)); }
    }
}
