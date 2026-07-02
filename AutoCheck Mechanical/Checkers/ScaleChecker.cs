using AutoCheckMechanical.Core;
using AutoCheckMechanical.Helpers;
using AutoCheckMechanical.Models;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Checkers
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

            bool encontrou = false;

            foreach (View view in DrawingHelper.GetAllViews(context.Drawing))
            {
                if (!view.IsFlatPatternView())
                    continue;

                encontrou = true;

                AddLog(result, $"Vista : {view.Name}");

                bool useSheet = ViewHelper.UseSheetScale(view);

                AddLog(result, $"Use Sheet Scale : {useSheet}");

                if (!useSheet)
                {
                    AddError(result,
                        "A Flat Pattern não utiliza a escala da folha.");
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