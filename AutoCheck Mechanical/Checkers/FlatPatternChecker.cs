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

            if (WauBlockHelper.DesenhoDispensaChecksDeChapa(context))
            {
                result.Skipped = true;
                result.Message = "Check dispensado (sem info de chapa).";
                AddLog(result, "Sem Matéria-Prima e sem vista planificada: check dispensado.");
                result.AddWarning("Sem info de chapa: checks de layer/planificado dispensados.");
                return result;
            }

            foreach (View view in context.Views)
            {
                if (!ViewHelper.IsFlatPattern(view))
                    continue;

                result.Message = "Flat Pattern encontrada.";

                AddLog(result, $"Vista: {view.Name}");
                AddLog(result, $"Configuração: {view.ReferencedConfiguration}");

                VerificarBlocoEMateriaPrimaComFlatPattern(context, result);

                foreach (var bend in ViewHelper.GetBendLinesInfo(view))
                {
                    AddLog(result, "====================================");
                    AddLog(result, $"Bend Line : {bend.Name}");

                    SketchSegment seg = bend.Segment;

                    if (seg == null)
                    {
                        AddLog(result, "Segmento inválido.");
                        continue;
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

                return result;
            }

            VerificarBlocoLegendaSemFlatPattern(context, result);

            AddError(result, "Nenhuma Flat Pattern encontrada.");

            return result;
        }

        // Regra: se existe vista planificada, o desenho é de chapa/dobra --
        // logo o bloco de legenda WAU precisa estar inserido e a
        // Matéria-Prima precisa estar preenchida. Faltando qualquer um dos
        // dois, é erro (diferente da regra inversa abaixo, que é só aviso).
        private void VerificarBlocoEMateriaPrimaComFlatPattern(CheckContext context, CheckResult result)
        {
            if (!WauBlockHelper.TemBlocoLegendaWau(context.Model))
                AddError(result, "Vista planificada encontrada, mas o bloco de legenda WAU não está inserido.");

            string materiaPrima = WauBlockHelper.GetMateriaPrima(context);

            if (string.IsNullOrWhiteSpace(materiaPrima))
                AddError(result, "Vista planificada encontrada, mas o campo Matéria-Prima não está preenchido.");
        }

        // Regra: se o bloco de legenda WAU está inserido no desenho e o campo
        // Matéria-Prima está preenchido (indício de peça de chapa/dobra), é
        // esperado que exista alguma vista com a planificada. Como chegamos
        // aqui é porque nenhuma foi encontrada -- gera um aviso na coluna
        // OBSERVAÇÃO em vez de um erro, já que pode ser um caso legítimo
        // (peça ainda sem planificada gerada, por exemplo).
        private void VerificarBlocoLegendaSemFlatPattern(CheckContext context, CheckResult result)
        {
            if (!WauBlockHelper.TemBlocoLegendaWau(context.Model))
                return;

            string materiaPrima = WauBlockHelper.GetMateriaPrima(context);

            if (string.IsNullOrWhiteSpace(materiaPrima))
                return;

            AddLog(result, $"Bloco de legenda WAU encontrado, Matéria-Prima = {materiaPrima}, mas sem vista planificada.");

            result.AddWarning("Falta vista planificada.");
        }
    }
}
