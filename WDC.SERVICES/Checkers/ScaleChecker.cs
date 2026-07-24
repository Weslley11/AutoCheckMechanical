using WDC.SERVICES.Core;
using WDC.SERVICES.Helpers;
using WDC.MODEL;
using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Checkers
{
    public class ScaleChecker : CheckerBase
    {
        public override string Name => "Scale";

        public override CheckResult Execute(CheckContext context)
        {
            CheckResult result = CreateResult();

            if (!context.IsDrawing)
            {
                AddError(result, "Documento não é Drawing.");
                return result;
            }

            if (TrySkipSemInfoDeChapa(context, result))
                return result;

            bool encontrou = false;

            foreach (View view in context.Views)
            {
                if (!view.IsFlatPatternView())
                    continue;

                encontrou = true;

                AddLog(result, $"Vista : {view.Name}");

                bool useSheet = ViewHelper.UseSheetScale(view);

                AddLog(result, $"Use Sheet Scale : {useSheet}");

                if (!useSheet)
                {
                    AddLog(result, $"Escala da vista : {ScaleHelper.GetScaleText(view)}");

                    AddError(result,
                        $"A Flat Pattern deve utilizar \"Use Sheet Scale\", e não uma escala manual ({ScaleHelper.GetScaleText(view)}).");
                }
            }

            if (!encontrou)
            {
                AddError(result,
                    "Nenhuma Flat Pattern encontrada.");
            }

            if (result.Errors.Count == 0)
            {
                result.Message = "Escala correta.";
            }

            return result;
        }
    }
}