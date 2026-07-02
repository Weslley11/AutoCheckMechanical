using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace AutoCheckMechanical.Helpers
{
    public static class BalloonHelper
    {
        public static int Count(View view)
        {
            return AnnotationHelper.Count(view, swAnnotationType_e.swBOMBalloon);
        }
    }
}
