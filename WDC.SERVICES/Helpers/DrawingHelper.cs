using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Helpers
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

            string originalSheetName = (drawing.GetCurrentSheet() as Sheet)?.GetName();

            // try/finally pra garantir que a aba original volta a ser a
            // ativa mesmo se ActivateSheet/GetViews falhar no meio (COM do
            // SolidWorks já se mostrou instável nesta sessão -- ex.:
            // RPC_E_SERVERFAULT em OpenDoc6) -- sem isso, uma falha parcial
            // deixava o desenho na aba errada pro resto do check.
            try
            {
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
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalSheetName))
                    drawing.ActivateSheet(originalSheetName);
            }

            return views;
        }
    }
}