using AutoCheckMechanical.Core;
using AutoCheckMechanical.Helpers;
using AutoCheckMechanical.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace AutoCheckMechanical.Checkers
{
    public class FlatPatternChecker : CheckerBase
    {

        public override string Name => "Flat Pattern";

        public override CheckResult Execute(CheckContext context)
        {
            CheckResult result = CreateResult();

            if (!context.IsDrawing)
            {
                AddError(result, "O documento ativo não é um Drawing.");
                return result;
            }

            foreach (View view in DrawingHelper.GetAllViews(context.Drawing))
            {
                if (!ViewHelper.IsFlatPattern(view))
                    continue;

                result.Message = "Flat Pattern encontrada.";

                AddLog(result, $"Vista: {view.Name}");
                AddLog(result, $"Configuração: {view.ReferencedConfiguration}");

                // ===== TESTE DAS BEND LINES =====
                foreach (var bend in ViewHelper.GetBendLinesInfo(view))
                {
                    AddLog(result, "====================================");
                    AddLog(result, $"Bend Line : {bend.Name}");

                    SketchSegment seg = bend.Segment;

                    AddLog(result, $"Tipo Segmento: {seg.GetType()}");

                    System.Type clrType = ((object)seg).GetType();

                    AddLog(result, clrType.FullName);

                    foreach (var m in clrType.GetMethods())
                    {
                        AddLog(result, m.Name);
                    }

                    SketchRelation[] relations = seg.GetRelations() as SketchRelation[];

                    if (relations == null)
                    {
                        AddLog(result, "Sem relações.");
                    }
                    else
                    {
                        AddLog(result, $"Qtd Relações : {relations.Length}");

                        foreach (SketchRelation rel in relations)
                        {
                            AddLog(result,
                                $"Relation Type : {rel.GetRelationType()}");
                        }
                    }
                }
                // ================================

                return result;
            }

            AddError(result, "Nenhuma Flat Pattern encontrada.");

            return result;
        }
    }
}