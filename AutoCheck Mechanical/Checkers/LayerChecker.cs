using System;
using AutoCheckMechanical.Configuration;
using AutoCheckMechanical.Core;
using AutoCheckMechanical.Helpers;
using AutoCheckMechanical.Models;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Checkers
{
    public class LayerChecker : CheckerBase
    {
        public override string Name => "Layer";

        public override CheckResult Execute(CheckContext context)
        {
            CheckResult result = CreateResult();

            if (!context.IsDrawing)
            {
                AddError(result, "Documento não é Drawing.");
                return result;
            }

            foreach (View view in context.Views)
            {
                if (!ViewHelper.IsFlatPattern(view))
                    continue;

                string layer = ViewHelper.GetLayer(view);

                AddLog(result, $"Vista : {view.Name}");
                AddLog(result, $"Layer : {layer}");

                if (string.IsNullOrEmpty(layer))
                {
                    AddError(result, "Flat Pattern sem Layer.");
                    return result;
                }

                if (!string.Equals(layer, CheckSettings.FlatPatternLayer, StringComparison.OrdinalIgnoreCase))
                {
                    AddError(result,
                        $"Layer incorreta ({layer})");
                    return result;
                }

                result.Message = "Layer correta.";

                return result;
            }

            AddError(result,
                "Flat Pattern não encontrada.");

            return result;
        }
    }
}