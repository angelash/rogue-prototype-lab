using System;
using System.Linq;
using NUnit.Framework;
using RingToss.Core;
using RingToss.Application;

namespace RingToss.Tests
{
    public sealed class ThreeDimensionalContractTests
    {
        private static readonly double[] Azimuths = { -25.25, 0, 25.25, -16, 0, 16 };
        private static readonly double[] Speeds = { 5.96, 5.64, 5.96, 7.46, 7.32, 7.46 };
        private static SlotObject Item(int index, ObjectKind kind, Occupancy occupancy, int durability = 0, double angle = 0)
        { return new SlotObject("3d/S" + index, index, kind, occupancy, durability, kind == ObjectKind.Board && angle == 0 ? 45 : angle, true, Rules.Price(kind), "golden", true); }
        private static void Finish(Flight3D flight)
        {
            for (int i = 0; i < 721 && flight.State == FlightState.Flying; i++) flight.Step();
            Assert.That(flight.State, Is.Not.EqualTo(FlightState.Flying));
        }
        private static void Throw(StageSession session, double azimuth, double speed, string tx, double elevation = 45)
        {
            Assert.That(session.Launch3D(new ThrowInput3D(azimuth, elevation, speed), tx).Success, Is.True);
            for (int i = 0; i < 721 && session.Phase == StagePhase.Flying; i++) session.Step();
            Assert.That(session.Phase, Is.Not.EqualTo(StagePhase.Flying));
        }

        [Test]
        public void ThreeDimensionalInputQuantizesAndDoesNotSilentlyUseTwoDimensionalLaunch()
        {
            var input = new ThrowInput3D(-16.13, 45.13, 7.459);
            Assert.That(input.AzimuthDegrees, Is.EqualTo(-16.25)); Assert.That(input.ElevationDegrees, Is.EqualTo(45.25));
            Assert.That(input.Speed, Is.EqualTo(7.46));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ThrowInput3D(-30.01, 45, 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ThrowInput3D(0, 75.01, 7));
            Assert.Throws<ArgumentException>(() => new Flight3D(default(ThrowInput3D), new SlotObject[6]));
            var session = StageSession.CreatePrototype3D("3d"); session.OpenStage("open");
            Assert.That(session.Launch(new ThrowInput(45, 7), "wrong-mode").Success, Is.False);
            Assert.That(session.RingsRemaining, Is.EqualTo(8));
        }

        [Test]
        public void EmptyThreeDimensionalFieldMatchesIndependentAnalyticCoordinates()
        {
            var flight = new Flight3D(new ThrowInput3D(16, 45, 7.46), new SlotObject[6]);
            for (int i = 0; i < 60; i++) flight.Step();
            double horizontal = 7.46 / Math.Sqrt(2), azimuth = 16 * Math.PI / 180;
            Assert.That(flight.Position.X, Is.EqualTo(horizontal * Math.Sin(azimuth) * 0.5).Within(1e-10));
            Assert.That(flight.Position.Z, Is.EqualTo(horizontal * Math.Cos(azimuth) * 0.5).Within(1e-10));
            Assert.That(flight.Position.Y, Is.EqualTo(0.8 + horizontal * 0.5 - 4.9 * 0.25).Within(1e-10));
        }

        // At elevation 45, v^2 = g*R^2/(R+0.15), R=sqrt(x^2+z^2).
        // These inputs are the independently calculated, then quantized values.
        [TestCase(0, -25.25, 5.96)]
        [TestCase(1, 0, 5.64)]
        [TestCase(2, 25.25, 5.96)]
        [TestCase(3, -16, 7.46)]
        [TestCase(4, 0, 7.32)]
        [TestCase(5, 16, 7.46)]
        public void GoldenInputReachesEverySlotInTheCompleteFreshSixSlotLayout(int slot, double azimuth, double speed)
        {
            SlotObject[] scene = StageSession.CreatePrototype3D("golden").Slots.ToArray();
            var flight = new Flight3D(new ThrowInput3D(azimuth, 45, speed), scene); Finish(flight);
            Assert.That(flight.State, Is.EqualTo(FlightState.HitPrize)); Assert.That(flight.HitSlotIndex, Is.EqualTo(slot));
            double vy = speed / Math.Sqrt(2), t = (vy + Math.Sqrt(vy * vy + 2 * 9.8 * 0.15)) / 9.8;
            Assert.That(flight.Time, Is.EqualTo(t).Within(1e-10));
            Assert.That(flight.Position.Y, Is.EqualTo(0.65).Within(1e-10));
            Double3 c = Rules3D.SlotPosition(slot);
            Assert.That(Math.Sqrt(Math.Pow(flight.Position.X - c.X, 2) + Math.Pow(flight.Position.Z - c.Z, 2)), Is.LessThan(0.015));
        }

        [Test]
        public void HorizontalMisalignmentCanPassBesidePrizeWithoutHiddenRecentering()
        {
            var scene = new SlotObject[6]; scene[0] = Item(0, ObjectKind.Fan, Occupancy.Prize);
            var flight = new Flight3D(new ThrowInput3D(0, 45, 5.96), scene); Finish(flight);
            Assert.That(flight.State, Is.EqualTo(FlightState.Missed)); Assert.That(flight.Position.X, Is.Zero);
            Assert.That(flight.Events.All(e => e.Kind != PhysicsEventKind.PrizeHit), Is.True);
        }

        [Test]
        public void AcceptanceIsACircleWithInclusiveAxisEdgesAndPublishedDownwardThreshold()
        {
            Double3 c = Rules3D.SlotPosition(1);
            Assert.That(Rules3D.Accepts(new Double3(c.X + .24, .65, c.Z), -.15, c), Is.True);
            Assert.That(Rules3D.Accepts(new Double3(c.X, .65, c.Z + .24), -.15, c), Is.True);
            Assert.That(Rules3D.Accepts(new Double3(c.X + .240001, .65, c.Z), -1, c), Is.False);
            Assert.That(Rules3D.Accepts(new Double3(c.X, .65, c.Z + .240001), -1, c), Is.False);
            Assert.That(Rules3D.Accepts(new Double3(c.X + .2, .65, c.Z + .2), -1, c), Is.False, "A square would incorrectly accept this corner.");
            Assert.That(Rules3D.Accepts(new Double3(c.X + .169, .65, c.Z + .169), -1, c), Is.True);
            Assert.That(Rules3D.Accepts(c, -.149999, c), Is.False);
        }

        [Test]
        public void VerticalTopAcceptsBeforeCylinderBodyAndFastSideCrossingCannotTunnel()
        {
            Double3 c = Rules3D.SlotPosition(1); var scene = new SlotObject[6]; scene[1] = Item(1, ObjectKind.Fan, Occupancy.Prize);
            var top = new Flight3D(scene, c + new Double3(0, .01, 0), new Double3(0, -1, 0)); Finish(top);
            Assert.That(top.State, Is.EqualTo(FlightState.HitPrize));
            var side = new Flight3D(scene, new Double3(c.X, .3, c.Z - .2), new Double3(0, 0, 16)); side.Step();
            Assert.That(side.MissReason, Is.EqualTo(MissReason.PrizeSide));
            Assert.That(side.Time, Is.EqualTo(.12 / 16).Within(1e-10));
        }

        [Test]
        public void ThreeDimensionalFanNeverPullsHorizontalVelocityTowardItsCenter()
        {
            Double3 c = Rules3D.SlotPosition(1); var scene = new SlotObject[6]; scene[1] = Item(1, ObjectKind.Fan, Occupancy.Mechanism, 4);
            var flight = new Flight3D(scene, new Double3(c.X + .2, 1.3, c.Z - 1.3), new Double3(.2, 1, 4));
            for (int i = 0; i < 20; i++) flight.Step();
            PhysicsEvent3D e = flight.Events.Single(evt => evt.Kind == PhysicsEventKind.FanImpulse);
            Assert.That(e.VelocityAfter.X, Is.EqualTo(e.VelocityBefore.X));
            Assert.That(e.VelocityAfter.Z - e.VelocityBefore.Z, Is.EqualTo(2).Within(1e-10));
            Assert.That(flight.Slots[1].Durability, Is.EqualTo(3));
        }

        [Test]
        public void FiniteThreeDimensionalBoardReflectsItsNormalAndRetainsTangentialX()
        {
            Double3 c = Rules3D.SlotPosition(1); var scene = new SlotObject[6]; scene[1] = Item(1, ObjectKind.Board, Occupancy.Mechanism, 6, 90);
            var flight = new Flight3D(scene, c + new Double3(0, 0, -.1), new Double3(1, 0, 15)); flight.Step();
            PhysicsEvent3D e = flight.Events.Single();
            Assert.That(e.Kind, Is.EqualTo(PhysicsEventKind.BoardBounce));
            Assert.That(e.VelocityAfter.X, Is.EqualTo(.95 * e.VelocityBefore.X).Within(1e-10));
            Assert.That(e.VelocityAfter.Z, Is.EqualTo(-.8 * e.VelocityBefore.Z).Within(1e-10));
            Assert.That(flight.Slots[1].Durability, Is.EqualTo(5));
            var outsideWidth = new Flight3D(scene, c + new Double3(.6, 0, -.1), new Double3(0, 0, 15)); outsideWidth.Step();
            Assert.That(outsideWidth.Events.Count, Is.Zero, "Outside the finite plate width must not collide with an infinite plane.");
        }

        [Test]
        public void CompleteSixPrizeCashRouteUsesTheSharedEconomyAndEndsAt210()
        {
            var session = StageSession.CreatePrototype3D("cash-route"); session.OpenStage("open");
            for (int i = 0; i < 6; i++)
            {
                Throw(session, Azimuths[i], Speeds[i], "throw-" + i);
                Assert.That(session.LastFlight3D.HitSlotIndex, Is.EqualTo(i));
                Assert.That(session.Cash("cash-" + i).Success, Is.True);
                Assert.That(session.Cash("cash-" + i).AlreadyApplied, Is.True);
            }
            Assert.That(session.Wallet, Is.EqualTo(210)); Assert.That(session.StageReceipts, Is.EqualTo(210));
            Assert.That(session.Target, Is.EqualTo(105)); Assert.That(session.RingsRemaining, Is.EqualTo(2));
            Assert.That(session.EndStage("end").Success, Is.True); Assert.That(session.IsSuccess, Is.True);
        }

        [TestCase(0, -25.25, 5.96, 4)]
        [TestCase(1, 0, 5.64, 6)]
        public void RetainedThreeDimensionalPrizeHasFullDurabilityAndCannotAlsoCash(int slot, double azimuth, double speed, int durability)
        {
            var session = StageSession.CreatePrototype3D("retain"); session.OpenStage("open");
            Throw(session, azimuth, speed, "throw");
            Assert.That(session.Retain("retain").Success, Is.True);
            Assert.That(session.Slots[slot].Durability, Is.EqualTo(durability));
            Assert.That(session.Slots[slot].SlotIndex, Is.EqualTo(slot));
            Assert.That(session.Slots[slot].Occupancy, Is.EqualTo(Occupancy.Mechanism));
            Assert.That(session.Wallet, Is.Zero); Assert.That(session.StageReceipts, Is.Zero);
            Assert.That(session.Cash("cash-too").Success, Is.False);
        }

        [Test]
        public void RepeatedThreeDimensionalInputsReproduceEventsWithoutMutatingFormalReplayState()
        {
            var session = StageSession.CreatePrototype3D("replay"); session.OpenStage("open");
            Throw(session, -25.25, 5.96, "first"); session.Retain("retain-fan");
            Throw(session, -16, 7.46, "second");
            int rings = session.RingsRemaining, wallet = session.Wallet, receipts = session.StageReceipts, durability = session.Slots[0].Durability;
            StagePhase phase = session.Phase; Flight3D original = session.LastFlight3D, replay = session.CreateReplayLastThrow3D(); Finish(replay);
            Assert.That(original.Events.Any(e => e.Kind == PhysicsEventKind.FanImpulse), Is.True);
            Assert.That(durability, Is.EqualTo(3)); Assert.That(replay.State, Is.EqualTo(original.State));
            Assert.That(replay.Events.Count, Is.EqualTo(original.Events.Count));
            for (int i = 0; i < original.Events.Count; i++)
            {
                Assert.That(replay.Events[i].Kind, Is.EqualTo(original.Events[i].Kind));
                Assert.That(replay.Events[i].ItemId, Is.EqualTo(original.Events[i].ItemId));
                Assert.That(replay.Events[i].Time, Is.EqualTo(original.Events[i].Time).Within(1e-10));
                Assert.That((replay.Events[i].Position - original.Events[i].Position).Length, Is.LessThan(1e-10));
            }
            Assert.That(session.RingsRemaining, Is.EqualTo(rings)); Assert.That(session.Wallet, Is.EqualTo(wallet));
            Assert.That(session.StageReceipts, Is.EqualTo(receipts)); Assert.That(session.Phase, Is.EqualTo(phase));
            Assert.That(session.Slots[0].Durability, Is.EqualTo(durability));
        }

        [Test]
        public void FinalThreeDimensionalRingDisposesBeforeEndAndDoesNotAlterTheTarget()
        {
            var session = StageSession.CreatePrototype3D("last-ring"); session.OpenStage("open");
            for (int i = 0; i < 3; i++) { Throw(session, Azimuths[i], Speeds[i], "hit-" + i); session.Cash("cash-" + i); }
            for (int i = 0; i < 4; i++) Throw(session, 0, 4, "miss-" + i, 20);
            Throw(session, -16, 7.46, "final");
            Assert.That(session.RingsRemaining, Is.Zero); Assert.That(session.Phase, Is.EqualTo(StagePhase.AwaitingDisposition));
            Assert.That(session.EndStage("too-early").Success, Is.False);
            session.Cash("final-cash"); session.EndStage("end");
            Assert.That(session.IsSuccess, Is.True); Assert.That(session.StageReceipts, Is.EqualTo(140));
            Assert.That(session.Target, Is.EqualTo(105));
        }
    }
}
