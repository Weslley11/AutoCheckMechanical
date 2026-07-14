using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Helpers
{
    public static class SelectionHelper
    {
        public static void Clear(ModelDoc2 model)
        {
            model.ClearSelection2(true);
        }

        public static bool SelectSegment(
            ModelDoc2 model,
            View view,
            SketchSegment segment,
            bool append = false)
        {
            if (model == null)
                return false;

            if (view == null)
                return false;

            if (segment == null)
                return false;

            SelectData data =
                model.ISelectionManager.CreateSelectData();

            data.View = view;

            return segment.Select4(append, data);
        }
    }
}