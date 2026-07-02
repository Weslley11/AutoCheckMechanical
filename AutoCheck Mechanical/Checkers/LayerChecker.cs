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

            bool flatPatternEncontrada = false;

            foreach (View view in context.Views)
            {
                bool isFlatPattern = ViewHelper.IsFlatPattern(view);
                string layer = ViewHelper.GetLayer(view);

                AddLog(result, $"Vista : {view.Name}");
                AddLog(result, $"Layer : {layer}");

                bool estaNoLayerPlanificado = !string.IsNullOrEmpty(layer) &&
                    string.Equals(layer, CheckSettings.FlatPatternLayer, StringComparison.OrdinalIgnoreCase);

                if (isFlatPattern)
                {
                    flatPatternEncontrada = true;

                    if (string.IsNullOrEmpty(layer))
                    {
                        AddError(result, $"Flat Pattern ({view.Name}) sem Layer.");
                    }
                    else if (!estaNoLayerPlanificado)
                    {
                        AddError(result,
                            $"Flat Pattern ({view.Name}) com layer incorreta ({layer}).");
                    }
                }
                else if (estaNoLayerPlanificado)
                {
                    string aviso = $"Vista \"{view.Name}\" não é Flat Pattern mas está no layer {CheckSettings.FlatPatternLayer}.";

                    result.AddWarning(aviso);
                    AddLog(result, "OBSERVAÇÃO: " + aviso);
                }
            }

            if (!flatPatternEncontrada)
            {
                AddError(result, "Flat Pattern não encontrada.");
            }

            if (result.Errors.Count == 0)
            {
                result.Message = "Layer correta.";
            }

            return result;
        }
    }
}