using System;
using System.Linq;
using NUnit.Framework;
using RingToss.Core;

namespace RingToss.Tests
{
    public sealed class PhysicsContractTests
    {
        private static SlotObject Item(int index, ObjectKind kind, Occupancy occupancy, int durability = 0, bool enabled = true, double angle = 0)
        {
            return new SlotObject("golden/S" + index, index, kind, occupancy, durability,
                kind == ObjectKind.Board && angle == 0 ? 45 : angle, enabled, Rules.Price(kind), "golden", true);
        }
        private static void Finish(Flight flight)
        {
            for (int i = 0; i < 721 && flight.State == FlightState.Flying; i++) flight.Step();
            Assert.That(flight.State, Is.Not.EqualTo(FlightState.Flying), "Flight must terminate within its six-second bound.");
        }

        [Test]
        public void InputQuantizesBeforeSimulationAndRejectsInvalidValues()
        {
            var input = new ThrowInput(45.13, 8.179);
            Assert.That(input.AngleDegrees, Is.EqualTo(45.25));
            Assert.That(input.Speed, Is.EqualTo(8.18));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ThrowInput(19.999, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ThrowInput(45, double.NaN));
            Assert.Throws<ArgumentException>(() => new Flight(default(ThrowInput), new SlotObject[6]));
        }

        [Test]
        public void EmptyFieldMatchesIndependentAnalyticArcAfterSixtyTicks()
        {
            var input = new ThrowInput(45, 8);
            var flight = new Flight(input, new SlotObject[6]);
            for (int i = 0; i < 60; i++) flight.Step();
            double t = 0.5, component = 8 / Math.Sqrt(2);
            Assert.That(flight.Position.X, Is.EqualTo(0.6 + component * t).Within(1e-10));
            Assert.That(flight.Position.Y, Is.EqualTo(0.8 + component * t - 4.9 * t * t).Within(1e-10));
            Assert.That(flight.Velocity.Y, Is.EqualTo(component - 9.8 * t).Within(1e-10));
        }

        [TestCase(0, 4.58)]
        [TestCase(1, 5.98)]
        [TestCase(2, 7.16)]
        [TestCase(3, 8.18)]
        [TestCase(4, 9.08)]
        [TestCase(5, 9.90)]
        public void QuantizedOrdinaryThrowHasAReachableRouteToEachSlot(int slot, double speed)
        {
            var scene = new SlotObject[6]; scene[slot] = Item(slot, ObjectKind.Fan, Occupancy.Prize);
            var flight = new Flight(new ThrowInput(45, speed), scene); Finish(flight);
            Assert.That(flight.State, Is.EqualTo(FlightState.HitPrize));
            Assert.That(flight.HitSlotIndex, Is.EqualTo(slot));
            Assert.That(flight.Position.Y, Is.EqualTo(1.2).Within(1e-10));
            Assert.That(Math.Abs(flight.Position.X - Rules.SlotX(slot)), Is.LessThanOrEqualTo(0.24));
        }

        [Test]
        public void AcceptanceIncludesPublishedEdgesAndRejectsUpwardOrOutsideInputs()
        {
            double x = Rules.SlotX(2);
            Assert.That(Rules.Accepts(x - 0.24, -0.15, x), Is.True);
            Assert.That(Rules.Accepts(x + 0.24, -0.15, x), Is.True);
            Assert.That(Rules.Accepts(x - 0.240001, -1, x), Is.False);
            Assert.That(Rules.Accepts(x + 0.240001, -1, x), Is.False);
            Assert.That(Rules.Accepts(x, -0.149999, x), Is.False);
            Assert.That(Rules.Accepts(x, 1, x), Is.False);
        }

        [Test]
        public void DownwardTopHitWinsOverPillarBodyAtSameContact()
        {
            var scene = new SlotObject[6]; scene[0] = Item(0, ObjectKind.Fan, Occupancy.Prize);
            var flight = new Flight(scene, new Double2(2.2, 1.21), new Double2(0, -1)); Finish(flight);
            Assert.That(flight.State, Is.EqualTo(FlightState.HitPrize));
            Assert.That(flight.Events.Single().Kind, Is.EqualTo(PhysicsEventKind.PrizeHit));
        }

        [Test]
        public void CrossingPillarSideWithinOneTickDoesNotTunnelOrAwardPrize()
        {
            var scene = new SlotObject[6]; scene[0] = Item(0, ObjectKind.Fan, Occupancy.Prize);
            var flight = new Flight(scene, new Double2(2.0, 0.6), new Double2(16, 0)); flight.Step();
            Assert.That(flight.State, Is.EqualTo(FlightState.Missed));
            Assert.That(flight.MissReason, Is.EqualTo(MissReason.PrizeSide));
            Assert.That(flight.Time, Is.EqualTo(0.12 / 16).Within(1e-10));
        }

        [TestCase(2.1, 16)]
        [TestCase(2.3, -16)]
        public void ThinBoardReflectsFromEitherSideAtContinuousContact(double x, double vx)
        {
            var scene = new SlotObject[6]; scene[0] = Item(0, ObjectKind.Board, Occupancy.Mechanism, 6, true, 90);
            var flight = new Flight(scene, new Double2(x, 1.2), new Double2(vx, 0)); flight.Step();
            PhysicsEvent e = flight.Events.Single();
            Assert.That(e.Kind, Is.EqualTo(PhysicsEventKind.BoardBounce));
            Assert.That(e.Time, Is.EqualTo(0.1 / 16).Within(1e-10));
            Assert.That(e.VelocityAfter.X, Is.EqualTo(-0.8 * e.VelocityBefore.X).Within(1e-10));
            Assert.That(e.VelocityAfter.Y, Is.EqualTo(0.95 * e.VelocityBefore.Y).Within(1e-10));
            Assert.That(flight.Slots[0].Durability, Is.EqualTo(5));
            for (int i = 0; i < 10; i++) flight.Step();
            Assert.That(flight.Events.Count(e2 => e2.Kind == PhysicsEventKind.BoardBounce), Is.EqualTo(1), "The same face must not rebounce before leaving by 0.02m.");
        }

        [Test]
        public void SameBoardAllowsTwoSeparatedBouncesThenItsFaceTerminatesTheThrow()
        {
            var scene = new SlotObject[6]; scene[0] = Item(0, ObjectKind.Board, Occupancy.Mechanism, 6, true, 15);
            Double2 tangent = Rules.Direction(15), normal = new Double2(-tangent.Y, tangent.X);
            var flight = new Flight(scene, new Double2(2.2, 1.2) + normal * 0.02, normal * -1.5); Finish(flight);
            Assert.That(flight.Events.Count(e => e.Kind == PhysicsEventKind.BoardBounce), Is.EqualTo(2));
            Assert.That(flight.MissReason, Is.EqualTo(MissReason.MechanismBody));
            Assert.That(flight.Slots[0].Durability, Is.EqualTo(4));
            Assert.That(flight.Events.Last().Time - flight.Events[1].Time, Is.GreaterThan(0.1), "This is a third real return, not an immediate face duplicate.");
        }

        [Test]
        public void OneFanTriggersOncePerThrowAndOnlyItsOwnDurabilityChanges()
        {
            var scene = new SlotObject[6]; scene[0] = Item(0, ObjectKind.Fan, Occupancy.Mechanism, 4);
            var original = scene[0];
            var flight = new Flight(scene, new Double2(0.9, 1.3), new Double2(4, 1));
            scene[0] = null; // The flight must own its array snapshot.
            for (int i = 0; i < 20; i++) flight.Step();
            PhysicsEvent e = flight.Events.Single(e2 => e2.Kind == PhysicsEventKind.FanImpulse);
            Assert.That(e.VelocityAfter.X - e.VelocityBefore.X, Is.EqualTo(2).Within(1e-10));
            Assert.That(flight.Slots[0].Durability, Is.EqualTo(3));
            Assert.That(original.Durability, Is.EqualTo(4));
        }

        [Test]
        public void InactiveFanKeepsSupportBodyAndInactiveBoardKeepsItsFace()
        {
            var fanScene = new SlotObject[6]; fanScene[0] = Item(0, ObjectKind.Fan, Occupancy.Mechanism, 4, false);
            var fan = new Flight(fanScene, new Double2(2.0, 0.9), new Double2(16, 0)); fan.Step();
            Assert.That(fan.MissReason, Is.EqualTo(MissReason.MechanismBody));
            Assert.That(fan.Slots[0].Durability, Is.EqualTo(4));
            var boardScene = new SlotObject[6]; boardScene[0] = Item(0, ObjectKind.Board, Occupancy.Mechanism, 0, true, 90);
            var board = new Flight(boardScene, new Double2(2.1, 1.2), new Double2(16, 0)); board.Step();
            Assert.That(board.MissReason, Is.EqualTo(MissReason.MechanismBody));
            Assert.That(board.Events.Single().Kind, Is.EqualTo(PhysicsEventKind.Miss));
        }

        [Test]
        public void SixFanChainHonorsSpeedAndEffectCaps()
        {
            var scene = new SlotObject[6];
            for (int i = 0; i < 6; i++) scene[i] = Item(i, ObjectKind.Fan, Occupancy.Mechanism, 4);
            var flight = new Flight(scene, new Double2(2.2, 2.3), new Double2(16, 0)); Finish(flight);
            var effects = flight.Events.Where(e => e.Kind == PhysicsEventKind.FanImpulse).ToArray();
            Assert.That(effects.Length, Is.EqualTo(6));
            Assert.That(effects.All(e => e.VelocityAfter.Length <= 16 + 1e-10), Is.True);
            Assert.That(flight.Slots.All(o => o.Durability == 3), Is.True);
        }

        [Test]
        public void SimultaneousOverlappingFansUseStableItemIdsRatherThanArrayOrder()
        {
            var scene = new SlotObject[6];
            scene[0] = new SlotObject("z-item", 0, ObjectKind.Fan, Occupancy.Mechanism, 4, 0, true, 30, "old", false);
            scene[1] = new SlotObject("a-item", 1, ObjectKind.Fan, Occupancy.Mechanism, 4, 0, true, 30, "old", false);
            var flight = new Flight(scene, new Double2(3, 1.6), new Double2(4, 0)); flight.Step();
            Assert.That(flight.Events.Count, Is.EqualTo(2));
            Assert.That(flight.Events[0].ItemId, Is.EqualTo("a-item"));
            Assert.That(flight.Events[1].ItemId, Is.EqualTo("z-item"));
            Assert.That(flight.Events[0].Time, Is.Zero); Assert.That(flight.Events[1].Time, Is.Zero);
            Assert.That(flight.Events[1].VelocityAfter.X, Is.EqualTo(8));
        }

        [Test]
        public void IdenticalInputsAndSnapshotsProduceIdenticalOrderedEvents()
        {
            var scene = new SlotObject[6]; scene[0] = Item(0, ObjectKind.Fan, Occupancy.Mechanism, 4);
            scene[3] = Item(3, ObjectKind.Board, Occupancy.Prize);
            var a = new Flight(new ThrowInput(45, 8.18), scene);
            var b = new Flight(new ThrowInput(45, 8.18), scene); Finish(a); Finish(b);
            Assert.That(b.State, Is.EqualTo(a.State)); Assert.That(b.Events.Count, Is.EqualTo(a.Events.Count));
            for (int i = 0; i < a.Events.Count; i++)
            {
                Assert.That(b.Events[i].Kind, Is.EqualTo(a.Events[i].Kind));
                Assert.That(b.Events[i].ItemId, Is.EqualTo(a.Events[i].ItemId));
                Assert.That(b.Events[i].Time, Is.EqualTo(a.Events[i].Time).Within(1e-10));
                Assert.That(b.Events[i].Position.X, Is.EqualTo(a.Events[i].Position.X).Within(1e-10));
                Assert.That(b.Events[i].Position.Y, Is.EqualTo(a.Events[i].Position.Y).Within(1e-10));
            }
        }
    }
}
