using System;
using System.Collections.Generic;

namespace RingToss.Core
{
    internal static class ContinuousGeometry3D
    {
        internal static Double3 Position(Double3 p, Double3 v, double t)
        { return p + v * t + new Double3(0, -Rules.Gravity * t * t * 0.5, 0); }
        internal static Double3 Velocity(Double3 v, double t) { return v + new Double3(0, -Rules.Gravity * t, 0); }
        internal static List<double> Plane(Double3 p, Double3 v, Double3 center, Double3 normal, double end)
        { return ContinuousGeometry.Roots(new[] { Double3.Dot(p - center, normal), Double3.Dot(v, normal), -Rules.Gravity * normal.Y * 0.5 }, end); }
        private static bool InBox(Double3 p, Double3 min, Double3 max)
        { return p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y && p.Z >= min.Z && p.Z <= max.Z; }
        internal static double BoxEntry(Double3 p, Double3 v, Double3 min, Double3 max, double end)
        {
            if (InBox(p, min, max)) return 0;
            double best = double.PositiveInfinity;
            foreach (double x in new[] { min.X, max.X })
                foreach (double t in Plane(p, v, new Double3(x, 0, 0), new Double3(1, 0, 0), end))
                { Double3 q = Position(p, v, t); if (q.Y >= min.Y && q.Y <= max.Y && q.Z >= min.Z && q.Z <= max.Z) best = Math.Min(best, t); }
            foreach (double y in new[] { min.Y, max.Y })
                foreach (double t in Plane(p, v, new Double3(0, y, 0), new Double3(0, 1, 0), end))
                { Double3 q = Position(p, v, t); if (q.X >= min.X && q.X <= max.X && q.Z >= min.Z && q.Z <= max.Z) best = Math.Min(best, t); }
            foreach (double z in new[] { min.Z, max.Z })
                foreach (double t in Plane(p, v, new Double3(0, 0, z), new Double3(0, 0, 1), end))
                { Double3 q = Position(p, v, t); if (q.X >= min.X && q.X <= max.X && q.Y >= min.Y && q.Y <= max.Y) best = Math.Min(best, t); }
            return best;
        }
        private static bool InCylinder(Double3 p, Double3 center, double radius, double top)
        {
            double dx = p.X - center.X, dz = p.Z - center.Z;
            return dx * dx + dz * dz <= radius * radius && p.Y >= 0 && p.Y <= top;
        }
        internal static double CylinderEntry(Double3 p, Double3 v, Double3 center, double radius, double top, double end)
        {
            if (InCylinder(p, center, radius, top)) return 0;
            double dx = p.X - center.X, dz = p.Z - center.Z, best = double.PositiveInfinity;
            foreach (double t in ContinuousGeometry.Roots(new[] { dx * dx + dz * dz - radius * radius,
                2 * (dx * v.X + dz * v.Z), v.X * v.X + v.Z * v.Z }, end))
            { Double3 q = Position(p, v, t); if (q.Y >= 0 && q.Y <= top) best = Math.Min(best, t); }
            foreach (double y in new[] { 0.0, top })
                foreach (double t in Plane(p, v, new Double3(0, y, 0), new Double3(0, 1, 0), end))
                { Double3 q = Position(p, v, t); double x = q.X - center.X, z = q.Z - center.Z; if (x * x + z * z <= radius * radius) best = Math.Min(best, t); }
            return best;
        }
        internal static double SphereEntry(Double3 p, Double3 v, Double3 center, double radius, double end)
        {
            Double3 d = p - center; double ay = -Rules.Gravity * 0.5;
            if (Double3.Dot(d, d) <= radius * radius) return 0;
            foreach (double t in ContinuousGeometry.Roots(new[] { Double3.Dot(d, d) - radius * radius,
                2 * Double3.Dot(d, v), Double3.Dot(v, v) + 2 * d.Y * ay, 2 * v.Y * ay, ay * ay }, end)) return t;
            return double.PositiveInfinity;
        }
        internal static double BoardDistance(Double3 p, Double3 center, Double3 tangent)
        {
            Double3 delta = p - center;
            double x = Math.Max(-Rules3D.BoardHalfWidth, Math.Min(Rules3D.BoardHalfWidth, delta.X));
            double span = Math.Max(-Rules3D.BoardHalfSpan, Math.Min(Rules3D.BoardHalfSpan, Double3.Dot(delta, tangent)));
            return (p - (center + new Double3(x, 0, 0) + tangent * span)).Length;
        }
    }
}
