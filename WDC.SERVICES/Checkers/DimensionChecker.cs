using WDC.SERVICES.Core;
using WDC.SERVICES.Helpers;
using WDC.MODEL;
using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Checkers
{
    public class DimensionChecker : CheckerBase
    {
        public override string Name => "Dimensions";

        public override CheckResult Execute(CheckContext context)
        {
            CheckResult result = CreateResult();

            SelectionInspector.Dump(context.Model, result);


            if (!context.IsDrawing)
            {
                AddError(result, "Documento não é Drawing.");
                return result;
            }

            foreach (View view in context.Views)
            {
                if (!view.IsFlatPatternView())
                    continue;

                AddLog(result, $"Vista: {view.Name}");

                int total = BendLineHelper.Count(view);

                for (int i = 0; i < total; i++)
                {
                    var edges = BendLineHelper.GetRelatedEdges(view, i);

                    AddLog(result, "--------------------------------");
                    AddLog(result, $"Bend #{i + 1}");
                    AddLog(result, $"Related Edges = {edges.Count}");
                }

                if (total == 0)
                {
                    result.Message = "Peça sem linhas de dobra.";
                    return result;
                }

                int semCota = 0;

                for (int i = 0; i < total; i++)
                {
                    bool ok =
                        BendLineHelper.HasDimension(view, i);

                    if (ok)
                    {
                        AddLog(result,
                            $"Bend #{i + 1} : OK");
                    }
                    else
                    {
                        semCota++;

                        AddError(result,
                            $"Bend #{i + 1} sem cota.");
                    }
                }

                if (semCota == 0)
                {
                    result.Message =
                        "Todas as linhas de dobra possuem cota.";
                }
                else
                {
                    result.Message =
                        $"{semCota} linha(s) de dobra sem cota.";
                }

                DimensionHelper.DumpDimensions(view, result);

                return result;

            }

            AddError(result,
                "Nenhuma Flat Pattern encontrada.");

            return result;
        }
    }
}