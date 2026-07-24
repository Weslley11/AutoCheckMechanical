using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Helpers
{
    public static class ScaleHelper
    {
        public static string GetScaleText(View view)
        {
            double[] scale = view.ScaleRatio as double[];

            if (scale == null || scale.Length < 2)
                return "?";

            return $"{scale[0]}:{scale[1]}";
        }
    }
}