using System;
using System.Collections.Generic;

namespace RingToss.Core
{
    internal static class ContinuousGeometry
    {
        // Recursively isolate roots at derivative critical points. Circle/parabola
        // intersection is quartic, so endpoints alone would miss a short contact.
        internal static List<double> Roots(double[] c, double end)
        {
            int n = c.Length - 1;
            while (n > 0 && Math.Abs(c[n]) < 1e-14) n--;
            var result = new List<double>();
            if (n == 0) return result;
            if (n == 1)
            {
                double t = -c[0] / c[1];
                if (t >= -1e-12 && t <= end + 1e-12) result.Add(Math.Max(0, Math.Min(end, t)));
                return result;
            }
            var derivative = new double[n];
            for (int i = 1; i <= n; i++) derivative[i - 1] = c[i] * i;
            var points = Roots(derivative, end);
            points.Insert(0, 0); points.Add(end);
            for (int i = 0; i < points.Count; i++)
            {
                double a = points[i], fa = Evaluate(c, n, a);
                if (Math.Abs(fa) < 1e-12) Add(result, a);
                if (i == points.Count - 1) continue;
                double b = points[i + 1], fb = Evaluate(c, n, b);
                if (fa * fb >= 0) continue;
                for (int iteration = 0; iteration < 64; iteration++)
                {
                    double m = (a + b) * 0.5, fm = Evaluate(c, n, m);
                    if (fa * fm <= 0) { b = m; fb = fm; }
                    else { a = m; fa = fm; }
                }
                Add(result, (a + b) * 0.5);
            }
            result.Sort(); return result;
        }
        private static double Evaluate(double[] c, int n, double t)
        { double r = c[n]; for (int i = n - 1; i >= 0; i--) r = r * t + c[i]; return r; }
        private static void Add(List<double> result, double t)
        { foreach (double v in result) if (Math.Abs(v - t) < 1e-11) return; result.Add(t); }
        internal static Double2 Position(Double2 p, Double2 v, double t)
        { return p + v * t + new Double2(0, -Rules.Gravity * t * t * 0.5); }
        internal static Double2 Velocity(Double2 v, double t) { return v + new Double2(0, -Rules.Gravity * t); }
        internal static List<double> Plane(Double2 p, Double2 v, Double2 center, Double2 normal, double end)
        { return Roots(new[] { Double2.Dot(p - center, normal), Double2.Dot(v, normal), -Rules.Gravity * normal.Y * 0.5 }, end); }
        internal static bool Inside(Double2 p, double left, double right, double bottom, double top)
        { return p.X >= left && p.X <= right && p.Y >= bottom && p.Y <= top; }
        internal static double RectangleEntry(Double2 p, Double2 v, double left, double right, double bottom, double top, double end)
        {
            if (Inside(p, left, right, bottom, top)) return 0;
            double best = double.PositiveInfinity;
            foreach (double x in new[] { left, right })
                foreach (double t in Roots(new[] { p.X - x, v.X }, end))
                { Double2 q = Position(p, v, t); if (q.Y >= bottom && q.Y <= top) best = Math.Min(best, t); }
            foreach (double y in new[] { bottom, top })
                foreach (double t in Roots(new[] { p.Y - y, v.Y, -Rules.Gravity * 0.5 }, end))
                { Double2 q = Position(p, v, t); if (q.X >= left && q.X <= right) best = Math.Min(best, t); }
            return best;
        }
        internal static double CircleEntry(Double2 p, Double2 v, Double2 center, double radius, double end)
        {
            Double2 d = p - center; double ay = -Rules.Gravity * 0.5;
            if (Double2.Dot(d, d) <= radius * radius) return 0;
            foreach (double t in Roots(new[] { Double2.Dot(d, d) - radius * radius,
                2 * Double2.Dot(d, v), Double2.Dot(v, v) + 2 * d.Y * ay, 2 * v.Y * ay, ay * ay }, end)) return t;
            return double.PositiveInfinity;
        }
        internal static double SegmentDistance(Double2 p, Double2 center, Double2 tangent, double halfLength)
        {
            double s = Double2.Dot(p - center, tangent);
            s = Math.Max(-halfLength, Math.Min(halfLength, s));
            return (p - (center + tangent * s)).Length;
        }
    }
}
