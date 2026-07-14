using WDC.MODEL;
using WDC.SERVICES.Helpers;
using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Readers
{
    public class DrawingReader
    {
        public DrawingDocument Read(DrawingDoc drawing)
        {
            DrawingDocument document =
                new DrawingDocument();

            if (drawing == null)
                return document;

            ModelDoc2 model =
                drawing as ModelDoc2;

            document.FileName =
                model.GetTitle();

            document.FilePath =
                model.GetPathName();

            foreach (View view in DrawingHelper.GetAllViews(drawing))
            {
                DrawingView drawingView =
                    new DrawingView();

                drawingView.View = view;

                drawingView.Name =
                    view.Name;

                drawingView.Configuration =
                    view.ReferencedConfiguration;

                drawingView.Layer =
                    ViewHelper.GetLayer(view);

                drawingView.IsFlatPattern =
                    ViewHelper.IsFlatPattern(view);

                drawingView.UseSheetScale =
                    ViewHelper.UseSheetScale(view);

                if (drawingView.IsFlatPattern)
                    document.FlatPattern = drawingView;
            }

            return document;
        }
    }
}