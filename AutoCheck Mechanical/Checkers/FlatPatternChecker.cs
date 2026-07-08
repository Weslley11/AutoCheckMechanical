using AutoCheckMechanical.Core;
using AutoCheckMechanical.Helpers;
using AutoCheckMechanical.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace AutoCheckMechanical.Checkers
{
    public class FlatPatternChecker : CheckerBase
    {
        private const string NomePropriedadeMateriaPrima = "materiaPrima";

        // Nomes de definição do bloco de legenda WAU, um por tamanho de folha,
        // conforme a macro de inserção da própria empresa (SelectByID2 com o
        // nome exato do bloco e tipo "SUBSKETCHDEF").
        private static readonly string[] NomesBlocoWau =
        {
            "BLOCO-WAU-A0_3",
            "BLOCO-WAU-A1_3",
            "BLOCO-WAU-A2_3",
            "BLOCO-WAU-A3_3",
            "BLOCO-WAU-A4_3",
        };

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

            result.AddWarning("Falta vista planificada.");
        }

        // Confirmado com a macro de inserção do bloco da própria empresa: o
        // bloco é selecionável por nome via SelectByID2 com o tipo
        // "SUBSKETCHDEF" (é assim que a macro localiza o bloco antigo antes
        // de apagar e inserir a versão atual).
        private static bool TemBlocoLegendaWau(ModelDoc2 modelo)
        {
            if (modelo == null)
                return false;

            bool encontrado = false;

            foreach (string nomeBloco in NomesBlocoWau)
            {
                bool selecionado = modelo.Extension.SelectByID2(
                    nomeBloco, "SUBSKETCHDEF", 0, 0, 0, false, 0, null, 0);

                if (selecionado)
                {
                    encontrado = true;
                    break;
                }
            }

            modelo.ClearSelection2(true);

            return encontrado;
        }
    }
}