using System;
using System.Linq;
using AutoCheckMechanical.Configuration;
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
                    double scale = ScaleHelper.GetScale(view);

                    bool allowed = CheckSettings.AllowedScales
                        .Any(s => Math.Abs(s - scale) < 0.0001);

                    AddLog(result, $"Escala da vista : {ScaleHelper.GetScaleText(view)}");

                    if (!allowed)
                    {
                        AddError(result,
                            $"A Flat Pattern não utiliza a escala da folha nem uma escala permitida ({ScaleHelper.GetScaleText(view)}).");
                    }
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