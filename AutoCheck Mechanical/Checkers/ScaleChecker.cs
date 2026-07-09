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

            if (WauBlockHelper.DesenhoDispensaChecksDeChapa(context))
            {
                result.Skipped = true;
                result.Message = "Check dispensado (sem bloco/Matéria-Prima de chapa).";
                AddLog(result, "Bloco de legenda WAU sem Matéria-Prima e sem vista planificada: check dispensado.");
                return result;
            }

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