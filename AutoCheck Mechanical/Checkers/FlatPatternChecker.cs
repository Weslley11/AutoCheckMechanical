using System;
using AutoCheckMechanical.Core;
using AutoCheckMechanical.Helpers;
using AutoCheckMechanical.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace AutoCheckMechanical.Checkers
{
    public class FlatPatternChecker : CheckerBase
    {
        private const string NomeBlocoLegenda = "WAU";
        private const string NomePropriedadeMateriaPrima = "materiaPrima";

        public override string Name => "Flat Pattern";

        public override CheckResult Execute(CheckContext context)
        {
            CheckResult result = CreateResult();

            if (!context.IsDrawing)
            {
                AddError(result, "O documento ativo não é um Drawing.");
                return result;
            }

            foreach (View view in context.Views)
            {
                if (!ViewHelper.IsFlatPattern(view))
                    continue;

                result.Message = "Flat Pattern encontrada.";

                AddLog(result, $"Vista: {view.Name}");
                AddLog(result, $"Configuração: {view.ReferencedConfiguration}");

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

        // Regra: se o bloco de legenda WAU está inserido no desenho e o campo
        // Matéria-Prima está preenchido (indício de peça de chapa/dobra), é
        // esperado que exista alguma vista com a planificada. Como chegamos
        // aqui é porque nenhuma foi encontrada -- gera um aviso na coluna
        // OBSERVAÇÃO em vez de um erro, já que pode ser um caso legítimo
        // (peça ainda sem planificada gerada, por exemplo).
        private void VerificarBlocoLegendaSemFlatPattern(CheckContext context, CheckResult result)
        {
            if (!TemBlocoLegendaWau(context.Model))
                return;

            string materiaPrima = PropertyHelper.GetValue(context.Model, NomePropriedadeMateriaPrima);

            if (string.IsNullOrWhiteSpace(materiaPrima))
            {
                foreach (View view in context.Views)
                {
                    ModelDoc2 peca = view.ReferencedDocument as ModelDoc2;
                    materiaPrima = PropertyHelper.GetValue(peca, NomePropriedadeMateriaPrima);

                    if (!string.IsNullOrWhiteSpace(materiaPrima))
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(materiaPrima))
                return;

            AddLog(result, $"Bloco de legenda WAU encontrado, Matéria-Prima = {materiaPrima}, mas sem vista planificada.");

            result.AddWarning(
                $"Bloco de legenda WAU inserido e Matéria-Prima preenchida ({materiaPrima}), " +
                "mas o desenho não tem nenhuma vista com a planificada.");
        }

        // NÃO CONFIRMADO na prática: em desenhos, definições de bloco aparecem
        // como features na árvore (documentação oficial da SOLIDWORKS), então
        // percorremos as features procurando uma ISketchBlockDefinition cujo
        // arquivo de origem contenha "WAU" no nome. O cast "as" é seguro --
        // se a API não bater exatamente assim, isso só retorna false, não quebra.
        private static bool TemBlocoLegendaWau(ModelDoc2 modelo)
        {
            if (modelo == null)
                return false;

            Feature feature = modelo.FirstFeature() as Feature;

            while (feature != null)
            {
                SketchBlockDefinition bloco = feature.GetSpecificFeature2() as SketchBlockDefinition;

                if (bloco != null &&
                    !string.IsNullOrEmpty(bloco.FileName) &&
                    bloco.FileName.IndexOf(NomeBlocoLegenda, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                feature = feature.GetNextFeature() as Feature;
            }

            return false;
        }
    }
}