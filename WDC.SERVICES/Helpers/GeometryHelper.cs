using System;

namespace WDC.SERVICES.Helpers
{
    public static class GeometryHelper
    {
        public static double Distance(
            double x1,
            double y1,
            double x2,
            double y2)
        {
            return Math.Sqrt(
                Math.Pow(x2 - x1, 2) +
                Math.Pow(y2 - y1, 2));
        }

        public static bool Equals(
            double a,
            double b,
            double tolerance = 0.000001)
        {
            return Math.Abs(a - b) <= tolerance;
        }

        public static double MidPoint(double a, double b)
        {
            return (a + b) / 2.0;
        }
    }
}