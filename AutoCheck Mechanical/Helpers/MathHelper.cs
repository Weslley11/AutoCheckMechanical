using System;

namespace AutoCheckMechanical.Helpers
{
    public static class MathHelper
    {
        public static double Round(double value)
        {
            return Math.Round(value, 6);
        }

        public static bool IsZero(double value)
        {
            return Math.Abs(value) < 0.000001;
        }

        public static bool NearlyEqual(
            double a,
            double b)
        {
            return Math.Abs(a - b) < 0.000001;
        }
    }
}