using System;
using System.Collections.Generic;

namespace RingToss.Core
{
    public sealed class Flight3D : IStageFlight
    {
        private readonly SlotObject[] slots;
        private readonly bool[] fanTriggered = new bool[6], boardArmed = new bool[6];
        private readonly int[] boardHits = new int[6];
        private readonly List<PhysicsEvent3D> events = new List<PhysicsEvent3D>();
        private int effectCount;
        public IReadOnlyList<SlotObject> Slots { get { return Array.AsReadOnly(slots); } }
        public IReadOnlyList<PhysicsEvent3D> Events { get { return events.AsReadOnly(); } }
        public Double3 Position { get; private set; }
        public Double3 Velocity { get; private set; }
        public double Time { get; private set; }
        public FlightState State { get; private set; }
        public int HitSlotIndex { get; private set; }
        public MissReason MissReason { get; private set; }
        public Flight3D(ThrowInput3D input, SlotObject[] scene) : this(scene, Rules3D.LaunchPoint, input.InitialVelocity()) { }
        public Flight3D(SlotObject[] scene, Double3 position, Double3 velocity)
        {
            if (scene == null || scene.Length != 6) throw new ArgumentException("Exactly six slots required.");
            if (!Finite(position.X) || !Finite(position.Y) || !Finite(position.Z) || !Finite(velocity.X) || !Finite(velocity.Y) || !Finite(velocity.Z)) throw new ArgumentException("Finite state required.");
            slots = (SlotObject[])scene.Clone(); var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 6; i++)
            {
                if (slots[i] != null && (slots[i].SlotIndex != i || !ids.Add(slots[i].Id))) throw new ArgumentException("Invalid slot identity.");
                boardArmed[i] = true;
            }
            Position = position; Velocity = velocity; State = FlightState.Flying; HitSlotIndex = -1;
        }
        private static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        public SlotObject[] CopySlots() { return (SlotObject[])slots.Clone(); }
        private enum Contact { Prize, PrizeBody, Fan, Board, Body, Ground, Bounds }
        private struct Candidate { public double T; public int Slot; public Contact Kind; public string Id; }
        private Candidate best;
        private void Offer(double t, int slot, Contact kind)
        {
            if (!Finite(t)) return;
            string id = slot < 0 ? "~environment" : slots[slot].Id;
            if (t < best.T - 1e-11 || (Math.Abs(t - best.T) <= 1e-11 &&
                (string.CompareOrdinal(id, best.Id) < 0 || (id == best.Id && kind == Contact.Prize && best.Kind == Contact.PrizeBody))))
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
                if (double.IsInfinity(best.T)) { Advance(remaining); break; }
                double t = Math.Max(0, Math.Min(remaining, best.T)); Advance(t); remaining -= t; Handle(best);
            }
            if (State == FlightState.Flying && Time >= Rules.MaxFlightTime - 1e-11) Miss(global::RingToss.Core.MissReason.TimeLimit, null);
        }
        private void Advance(double t)
        { Position = ContinuousGeometry3D.Position(Position, Velocity, t); Velocity = ContinuousGeometry3D.Velocity(Velocity, t); Time += t; }
        private void FindContacts(double end)
        {
            for (int i = 0; i < 6; i++)
            {
                SlotObject o = slots[i]; if (o == null) continue; Double3 center = Rules3D.SlotPosition(i);
                if (o.Occupancy == Occupancy.Prize)
                {
                    foreach (double t in ContinuousGeometry3D.Plane(Position, Velocity, center, new Double3(0, 1, 0), end))
                    {
                        Double3 q = ContinuousGeometry3D.Position(Position, Velocity, t), v = ContinuousGeometry3D.Velocity(Velocity, t);
                        if (Rules3D.Accepts(q, v.Y, center)) Offer(t, i, Contact.Prize);
                    }
                    Offer(ContinuousGeometry3D.CylinderEntry(Position, Velocity, center, Rules3D.PostRadius, Rules3D.AcceptHeight, end), i, Contact.PrizeBody);
                }
                else if (o.Kind == ObjectKind.Fan)
                {
                    if (o.Enabled && o.Durability > 0 && !fanTriggered[i] && effectCount < Rules.MaxEffects)
                        Offer(ContinuousGeometry3D.BoxEntry(Position, Velocity,
                            new Double3(center.X - Rules3D.FanHalfWidth, Rules3D.FanBottom, center.Z - Rules3D.FanHalfDepth),
                            new Double3(center.X + Rules3D.FanHalfWidth, Rules3D.FanTop, center.Z + Rules3D.FanHalfDepth), end), i, Contact.Fan);
                    Offer(ContinuousGeometry3D.SphereEntry(Position, Velocity, new Double3(center.X, 0.9, center.Z), 0.1, end), i, Contact.Body);
                }
                else
                {
                    Double3 tangent = Rules3D.MechanismDirection(o.AngleDegrees), normal = new Double3(0, tangent.Z, -tangent.Y);
                    if (!boardArmed[i] && ContinuousGeometry3D.BoardDistance(Position, center, tangent) >= Rules.RearmDistance) boardArmed[i] = true;
                    if (!boardArmed[i]) continue;
                    foreach (double t in ContinuousGeometry3D.Plane(Position, Velocity, center, normal, end))
                    {
                        Double3 q = ContinuousGeometry3D.Position(Position, Velocity, t), d = q - center;
                        if (Math.Abs(d.X) <= Rules3D.BoardHalfWidth && Math.Abs(Double3.Dot(d, tangent)) <= Rules3D.BoardHalfSpan)
                            Offer(t, i, o.Enabled && o.Durability > 0 && boardHits[i] < 2 && effectCount < Rules.MaxEffects ? Contact.Board : Contact.Body);
                    }
                }
            }
            foreach (double t in ContinuousGeometry3D.Plane(Position, Velocity, new Double3(0, 0, 0), new Double3(0, 1, 0), end))
                if (ContinuousGeometry3D.Velocity(Velocity, t).Y <= 0) Offer(t, -1, Contact.Ground);
            foreach (double x in new[] { -6.0, 6.0 })
                foreach (double t in ContinuousGeometry3D.Plane(Position, Velocity, new Double3(x, 0, 0), new Double3(1, 0, 0), end))
                    if ((x < 0 && Velocity.X < 0) || (x > 0 && Velocity.X > 0)) Offer(t, -1, Contact.Bounds);
            foreach (double z in new[] { 0.0, 12.0 })
                foreach (double t in ContinuousGeometry3D.Plane(Position, Velocity, new Double3(0, 0, z), new Double3(0, 0, 1), end))
                    if ((z == 0 && Velocity.Z < 0) || (z == 12 && Velocity.Z > 0)) Offer(t, -1, Contact.Bounds);
            foreach (double t in ContinuousGeometry3D.Plane(Position, Velocity, new Double3(0, 7, 0), new Double3(0, 1, 0), end))
                if (ContinuousGeometry3D.Velocity(Velocity, t).Y > 0) Offer(t, -1, Contact.Bounds);
        }
        private void Handle(Candidate c)
        {
            SlotObject o = c.Slot < 0 ? null : slots[c.Slot]; Double3 before = Velocity;
            if (c.Kind == Contact.Prize)
            {
                State = FlightState.HitPrize; HitSlotIndex = c.Slot;
                events.Add(new PhysicsEvent3D(PhysicsEventKind.PrizeHit, o.Id, Time, Position, before, Velocity, 0)); return;
            }
            if (c.Kind == Contact.Fan || c.Kind == Contact.Board)
            {
                if (c.Kind == Contact.Fan) { fanTriggered[c.Slot] = true; Velocity += Rules3D.MechanismDirection(o.AngleDegrees) * 2; }
                else
                {
                    boardArmed[c.Slot] = false; boardHits[c.Slot]++;
                    Double3 tangent = Rules3D.MechanismDirection(o.AngleDegrees), normal = new Double3(0, tangent.Z, -tangent.Y);
                    Double3 vn = normal * Double3.Dot(Velocity, normal), vt = Velocity - vn;
                    Velocity = vt * 0.95 - vn * 0.8;
                }
                if (Velocity.Length > Rules.MaxSpeed) Velocity = Velocity * (Rules.MaxSpeed / Velocity.Length);
                effectCount++; slots[c.Slot] = o.With(o.SlotIndex, o.Durability - 1, o.AngleDegrees, o.Enabled, o.Occupancy);
                events.Add(new PhysicsEvent3D(c.Kind == Contact.Fan ? PhysicsEventKind.FanImpulse : PhysicsEventKind.BoardBounce,
                    o.Id, Time, Position, before, Velocity, o.Durability - 1)); return;
            }
            Miss(c.Kind == Contact.PrizeBody ? global::RingToss.Core.MissReason.PrizeSide : c.Kind == Contact.Body ? global::RingToss.Core.MissReason.MechanismBody :
                c.Kind == Contact.Ground ? global::RingToss.Core.MissReason.Ground : global::RingToss.Core.MissReason.Bounds, o == null ? null : o.Id);
        }
        private void Miss(MissReason reason, string id)
        { State = FlightState.Missed; MissReason = reason; events.Add(new PhysicsEvent3D(PhysicsEventKind.Miss, id, Time, Position, Velocity, Velocity, 0)); }
    }
}
