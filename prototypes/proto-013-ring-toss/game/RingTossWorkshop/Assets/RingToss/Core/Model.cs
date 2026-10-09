using System;
using System.Collections.Generic;

namespace RingToss.Core
{
    public struct Double2
    {
        public readonly double X, Y;
        public Double2(double x, double y) { X = x; Y = y; }
        public double Length { get { return Math.Sqrt(X * X + Y * Y); } }
        public static Double2 operator +(Double2 a, Double2 b) { return new Double2(a.X + b.X, a.Y + b.Y); }
        public static Double2 operator -(Double2 a, Double2 b) { return new Double2(a.X - b.X, a.Y - b.Y); }
        public static Double2 operator *(Double2 a, double b) { return new Double2(a.X * b, a.Y * b); }
        public static double Dot(Double2 a, Double2 b) { return a.X * b.X + a.Y * b.Y; }
    }

    public static class Rules
    {
        public const double Dt = 1.0 / 120.0, Gravity = 9.8, MaxFlightTime = 6, MaxSpeed = 16;
        public const double InnerRadius = 0.32, PostRadius = 0.08, AcceptHalfWidth = 0.24, AcceptHeight = 1.2;
        public const double MinDownwardSpeed = -0.15, RearmDistance = 0.02;
        public const int MaxEffects = 6, BaseRings = 8, Adjustments = 2, RescuePrice = 18;
        public static readonly Double2 LaunchPoint = new Double2(0.6, 0.8);
        public static double SlotX(int index)
        {
            if (index < 0 || index >= 6) throw new ArgumentOutOfRangeException("index");
            return 2.2 + 1.6 * index;
        }
        public static int Price(ObjectKind kind) { return kind == ObjectKind.Fan ? 30 : 40; }
        public static int MaxDurability(ObjectKind kind) { return kind == ObjectKind.Fan ? 4 : 6; }
        public static Double2 Direction(double degrees)
        {
            double r = degrees * Math.PI / 180.0;
            return new Double2(Math.Cos(r), Math.Sin(r));
        }
        public static bool Accepts(double x, double vy, double slotX)
        {
            // Compare the public interval directly; no extra reward tolerance is added.
            return vy <= MinDownwardSpeed && x >= slotX - AcceptHalfWidth && x <= slotX + AcceptHalfWidth;
        }
        public static bool ValidAngle(ObjectKind kind, double angle)
        {
            if (double.IsNaN(angle) || double.IsInfinity(angle)) return false;
            if (kind == ObjectKind.Board) return angle >= 15 && angle <= 165 && Math.Abs(angle / 5 - Math.Round(angle / 5)) < 1e-10;
            double a = ((angle % 360) + 360) % 360;
            return a == 0 || a == 30 || a == 150 || a == 180 || a == 210 || a == 330;
        }
    }

    public struct ThrowInput
    {
        public readonly int AngleQuarterDegrees, SpeedFiftieths;
        public double AngleDegrees { get { return AngleQuarterDegrees * 0.25; } }
        public double Speed { get { return SpeedFiftieths * 0.02; } }
        public ThrowInput(double angleDegrees, double speed)
        {
            if (double.IsNaN(angleDegrees) || double.IsInfinity(angleDegrees) || angleDegrees < 20 || angleDegrees > 75)
                throw new ArgumentOutOfRangeException("angleDegrees");
            if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 4 || speed > 12)
                throw new ArgumentOutOfRangeException("speed");
            AngleQuarterDegrees = (int)Math.Round(angleDegrees * 4, MidpointRounding.AwayFromZero);
            SpeedFiftieths = (int)Math.Round(speed * 50, MidpointRounding.AwayFromZero);
        }
    }

    public enum ObjectKind { Fan, Board }
    public enum Occupancy { Prize, Mechanism }

    // Immutable objects make both live flights and replay snapshots independent.
    public sealed class SlotObject
    {
        public readonly string Id, SourceStage;
        public readonly int SlotIndex, Durability, PrizeValue;
        public readonly ObjectKind Kind;
        public readonly Occupancy Occupancy;
        public readonly double AngleDegrees;
        public readonly bool Enabled, ReceiptEligible;
        public double X { get { return Rules.SlotX(SlotIndex); } }
        public SlotObject(string id, int slotIndex, ObjectKind kind, Occupancy occupancy,
            int durability, double angleDegrees, bool enabled, int prizeValue, string sourceStage, bool receiptEligible)
        {
            Rules.SlotX(slotIndex);
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Missing item ID.");
            if ((kind != ObjectKind.Fan && kind != ObjectKind.Board) ||
                (occupancy != Occupancy.Prize && occupancy != Occupancy.Mechanism)) throw new ArgumentException("Unknown item type.");
            if (!Rules.ValidAngle(kind, angleDegrees)) throw new ArgumentOutOfRangeException("angleDegrees");
            if (durability < 0 || durability > Rules.MaxDurability(kind) || prizeValue < 0) throw new ArgumentOutOfRangeException("durability");
            Id = id; SlotIndex = slotIndex; Kind = kind; Occupancy = occupancy; Durability = durability;
            AngleDegrees = angleDegrees; Enabled = enabled; PrizeValue = prizeValue; SourceStage = sourceStage; ReceiptEligible = receiptEligible;
        }
        public SlotObject With(int slotIndex, int durability, double angle, bool enabled, Occupancy occupancy)
        {
            return new SlotObject(Id, slotIndex, Kind, occupancy, durability, angle, enabled, PrizeValue, SourceStage, ReceiptEligible);
        }
        public SlotObject Retained()
        {
            return With(SlotIndex, Rules.MaxDurability(Kind), AngleDegrees, true, Occupancy.Mechanism);
        }
    }

    public enum FlightState { Flying, HitPrize, Missed }
    public enum MissReason { None, Ground, Bounds, TimeLimit, PrizeSide, MechanismBody }
    public enum PhysicsEventKind { FanImpulse, BoardBounce, PrizeHit, Miss }
    public sealed class PhysicsEvent
    {
        public readonly PhysicsEventKind Kind;
        public readonly string ItemId;
        public readonly double Time;
        public readonly Double2 Position, VelocityBefore, VelocityAfter;
        public readonly int RemainingDurability;
        public PhysicsEvent(PhysicsEventKind kind, string itemId, double time, Double2 position, Double2 before, Double2 after, int durability)
        { Kind = kind; ItemId = itemId; Time = time; Position = position; VelocityBefore = before; VelocityAfter = after; RemainingDurability = durability; }
    }
}
