using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Helpers
{
    public static class ScaleHelper
    {
        public static double GetScale(View view)
        {
            if (view == null)
                return 0;

            double[] scale = view.ScaleRatio as double[];

            if (scale == null || scale.Length < 2)
                return 0;

            if (scale[1] == 0)
                return 0;

            return scale[0] / scale[1];
        }

        public static bool IsOneToOne(View view)
        {
            return System.Math.Abs(GetScale(view) - 1.0) < 0.000001;
        }

        public static string GetScaleText(View view)
        {
            double[] scale = view.ScaleRatio as double[];

            if (scale == null || scale.Length < 2)
                return "?";

            return $"{scale[0]}:{scale[1]}";
        }
    }
}