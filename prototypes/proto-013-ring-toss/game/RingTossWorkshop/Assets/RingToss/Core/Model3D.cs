using System;

namespace RingToss.Core
{
    // Minimal common flight contract keeps the economy independent of geometry.
    public interface IStageFlight
    {
        FlightState State { get; }
        int HitSlotIndex { get; }
        void Step();
        SlotObject[] CopySlots();
    }

    public struct Double3
    {
        public readonly double X, Y, Z;
        public Double3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double Length { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
        public static Double3 operator +(Double3 a, Double3 b) { return new Double3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Double3 operator -(Double3 a, Double3 b) { return new Double3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Double3 operator *(Double3 a, double b) { return new Double3(a.X * b, a.Y * b, a.Z * b); }
        public static double Dot(Double3 a, Double3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
    }

    // v0.6 author parameters for the user's new 3D ground-stall direction.
    // These are initial suggestions, not measured difficulty or final balance.
    public static class Rules3D
    {
        public const string ParameterVersion = "0.6.0";
        public const double AcceptHeight = 0.65, AcceptRadius = 0.24, PostRadius = 0.08;
        public const double FanHalfWidth = 0.6, FanHalfDepth = 1.2, FanBottom = 0.8, FanTop = 2.4;
        public const double BoardHalfWidth = 0.5, BoardHalfSpan = 0.5;
        public static readonly Double3 LaunchPoint = new Double3(0, 0.8, 0);
        public static Double3 SlotPosition(int index)
        {
            if (index < 0 || index >= 6) throw new ArgumentOutOfRangeException("index");
            return new Double3((index % 3 - 1) * 1.6, AcceptHeight, index < 3 ? 3.4 : 5.6);
        }
        public static Double3 MechanismDirection(double degrees)
        {
            double radians = degrees * Math.PI / 180;
            return new Double3(0, Math.Sin(radians), Math.Cos(radians));
        }
        public static bool Accepts(Double3 position, double vy, Double3 center)
        {
            if (!(vy <= Rules.MinDownwardSpeed) || position.Z < center.Z - AcceptRadius || position.Z > center.Z + AcceptRadius) return false;
            // Compare the published circle's cross-section interval directly.
            // Exact axis endpoints do not acquire an extra floating-point band.
            if (position.Z == center.Z - AcceptRadius || position.Z == center.Z + AcceptRadius) return position.X == center.X;
            double dz = position.Z - center.Z;
            double halfWidth = Math.Sqrt(Math.Max(0, AcceptRadius * AcceptRadius - dz * dz));
            return position.X >= center.X - halfWidth && position.X <= center.X + halfWidth;
        }
    }

    public struct ThrowInput3D
    {
        public readonly int AzimuthQuarterDegrees, ElevationQuarterDegrees, SpeedFiftieths;
        public double AzimuthDegrees { get { return AzimuthQuarterDegrees * 0.25; } }
        public double ElevationDegrees { get { return ElevationQuarterDegrees * 0.25; } }
        public double Speed { get { return SpeedFiftieths * 0.02; } }
        public ThrowInput3D(double azimuthDegrees, double elevationDegrees, double speed)
        {
            if (!Finite(azimuthDegrees) || azimuthDegrees < -30 || azimuthDegrees > 30) throw new ArgumentOutOfRangeException("azimuthDegrees");
            if (!Finite(elevationDegrees) || elevationDegrees < 20 || elevationDegrees > 75) throw new ArgumentOutOfRangeException("elevationDegrees");
            if (!Finite(speed) || speed < 4 || speed > 12) throw new ArgumentOutOfRangeException("speed");
            AzimuthQuarterDegrees = (int)Math.Round(azimuthDegrees * 4, MidpointRounding.AwayFromZero);
            ElevationQuarterDegrees = (int)Math.Round(elevationDegrees * 4, MidpointRounding.AwayFromZero);
            SpeedFiftieths = (int)Math.Round(speed * 50, MidpointRounding.AwayFromZero);
        }
        private static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        public Double3 InitialVelocity()
        {
            if (AzimuthDegrees < -30 || AzimuthDegrees > 30 || ElevationDegrees < 20 || ElevationDegrees > 75 || Speed < 4 || Speed > 12) throw new ArgumentException("Invalid 3D throw input.");
            double azimuth = AzimuthDegrees * Math.PI / 180, elevation = ElevationDegrees * Math.PI / 180;
            double horizontal = Speed * Math.Cos(elevation);
            return new Double3(horizontal * Math.Sin(azimuth), Speed * Math.Sin(elevation), horizontal * Math.Cos(azimuth));
        }
    }

    public sealed class PhysicsEvent3D
    {
        public readonly PhysicsEventKind Kind;
        public readonly string ItemId;
        public readonly double Time;
        public readonly Double3 Position, VelocityBefore, VelocityAfter;
        public readonly int RemainingDurability;
        public PhysicsEvent3D(PhysicsEventKind kind, string itemId, double time, Double3 position, Double3 before, Double3 after, int durability)
        { Kind = kind; ItemId = itemId; Time = time; Position = position; VelocityBefore = before; VelocityAfter = after; RemainingDurability = durability; }
    }
}
