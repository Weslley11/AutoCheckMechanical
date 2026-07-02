using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Helpers
{
    public static class DrawingHelper
    {
        public static List<View> GetAllViews(DrawingDoc drawing)
        {
            List<View> views = new List<View>();

            if (drawing == null)
                return views;

            string[] sheetNames = drawing.GetSheetNames() as string[];

            if (sheetNames == null)
                return views;

            foreach (string sheetName in sheetNames)
            {
                drawing.ActivateSheet(sheetName);

                Sheet sheet = drawing.GetCurrentSheet();

                object[] sheetViews = sheet.GetViews() as object[];

                if (sheetViews == null)
                    continue;

                foreach (View view in sheetViews)
                {
                    views.Add(view);
                }
            }

            return views;
        }
    }
}