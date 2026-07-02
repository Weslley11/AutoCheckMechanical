using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Helpers
{
    public static class BalloonHelper
    {
        public static int Count(View view)
        {
            return AnnotationHelper.GetBalloons(view).Count;
        }
    }
}
