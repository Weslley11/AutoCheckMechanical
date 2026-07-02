using AutoCheckMechanical.Core;
using AutoCheckMechanical.Helpers;
using AutoCheckMechanical.Models;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Checkers
{
    public class BalloonChecker : CheckerBase
    {
        public override string Name => "Balloons";

        public override CheckResult Execute(CheckContext context)
        {
            CheckResult result = CreateResult();

            if (!context.IsDrawing)
            {
                AddError(result, "Documento não é Drawing.");
                return result;
            }

            foreach (View view in DrawingHelper.GetAllViews(context.Drawing))
            {
                if (!view.IsFlatPatternView())
                    continue;

                int qtd = BalloonHelper.Count(view);

                AddLog(result, $"Vista : {view.Name}");
                AddLog(result, $"Balões : {qtd}");

                if (qtd == 0)
                {
                    AddError(result, "Nenhum balão encontrado.");
                }
                else
                {
                    result.Message = $"{qtd} balão(ões) encontrado(s).";
                }

                return result;
            }

            AddError(result, "Flat Pattern não encontrada.");
            return result;
        }
    }
}