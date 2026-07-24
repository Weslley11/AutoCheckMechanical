using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using WDC.SERVICES.Core;
using WDC.SERVICES.Helpers;
using WDC.MODEL;
using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Checkers
{
    public class TitleBlockChecker : CheckerBase
    {
        public override string Name => "Bloco Legenda WAU";

        // Ordem dos campos exibida na tabela de resultados (MainWindow usa esta lista).
        public static readonly string[] OrdemCampos =
        {
            "Material",
            "Matéria-Prima",
            "Tratamento",
            "Acabamento",
            "Plano de Pintura",
            "Matriz de Dobra",
            "Raio de Dobra",
            "Massa Líquida",
        };

        // Nomes reais das propriedades customizadas extraídos do BLOCO-WAU-A3_3.SLDBLK
        private static readonly KeyValuePair<string, string>[] CamposObrigatorios =
        {
            new KeyValuePair<string, string>("Matéria-Prima", "materiaPrima"),
            new KeyValuePair<string, string>("Tratamento", "tratamento"),
            new KeyValuePair<string, string>("Acabamento", "acabamento"),
            new KeyValuePair<string, string>("Plano de Pintura", "planoPintura"),
            new KeyValuePair<string, string>("Matriz de Dobra", "matrizDobra"),
            new KeyValuePair<string, string>("Raio de Dobra", "raioDobra"),
            new KeyValuePair<string, string>("Massa Líquida", "ZMASSA_LIQUIDA_01"),
        };

        // Tolerância aceita entre a Massa Líquida do bloco e a massa
        // calculada pelo SolidWorks (o valor da legenda pode estar
        // arredondado).
        private const double ToleranciaMassa = 0.05;

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
                result.Message = "Check dispensado (sem info de chapa).";
                AddLog(result, "Sem Matéria-Prima e sem vista planificada: check dispensado.");
                result.AddWarning("Sem info de chapa: check de bloco de legenda dispensado.");
                return result;
            }

            bool flatPatternEncontrada = false;

            foreach (View view in context.Views)
            {
                if (!ViewHelper.IsFlatPattern(view))
                    continue;

                flatPatternEncontrada = true;

                AddLog(result, $"Vista : {view.Name}");

                ModelDoc2 peca = view.ReferencedDocument as ModelDoc2;

                if (peca == null)
                {
                    AddLog(result, "Não foi possível acessar a peça referenciada pela vista.");
                }

                VerificarMaterial(result, peca);

                foreach (KeyValuePair<string, string> campo in CamposObrigatorios)
                {
                    string valor = PropertyHelper.GetValue(context.Model, peca, campo.Value);
                    RegistrarCampo(result, campo.Key, valor);
                }

                VerificarMassa(result, peca);
            }

            if (!flatPatternEncontrada)
            {
                AddError(result, "Flat Pattern não encontrada.");
                return result;
            }

            // Existe vista planificada -- então o bloco de legenda WAU
            // precisa estar inserido (os campos individuais, como
            // Matéria-Prima, já são checados acima via CamposObrigatorios).
            if (!WauBlockHelper.TemBlocoLegendaWau(context.Model))
            {
                AddError(result, "Vista planificada encontrada, mas o bloco de legenda WAU não está inserido.");
            }

            VerificarDivergenciaMaterial(result);

            if (result.Errors.Count == 0)
            {
                result.Message = "Todos os campos do Bloco Legenda WAU preenchidos.";
            }
            else
            {
                result.AddWarning("Está faltando informação no bloco de legenda WAU.");
            }

            return result;
        }

        private void VerificarMaterial(CheckResult result, ModelDoc2 peca)
        {
            string material = null;

            PartDoc partDoc = peca as PartDoc;

            if (partDoc != null)
            {
                try
                {
                    string configAtiva = peca.ConfigurationManager?.ActiveConfiguration?.Name ?? "";
                    string varLibProperties;

                    material = partDoc.GetMaterialPropertyName2(configAtiva, out varLibProperties);
                }
                catch (COMException)
                {
                }
            }

            if (string.IsNullOrWhiteSpace(material))
                material = PropertyHelper.GetValue(peca, "Material");

            RegistrarCampo(result, "Material", material);
        }

        // Regra: a Massa Líquida declarada no bloco de legenda WAU precisa
        // bater com a massa calculada pelo SolidWorks a partir da geometria
        // da peça (tolerância de 5%, já que o valor na legenda costuma estar
        // arredondado).
        private void VerificarMassa(CheckResult result, ModelDoc2 peca)
        {
            string massaBlocoTexto;
            result.Fields.TryGetValue("Massa Líquida", out massaBlocoTexto);

            if (string.IsNullOrWhiteSpace(massaBlocoTexto))
                return;

            double massaBloco;

            if (!TentarConverterMassa(massaBlocoTexto, out massaBloco))
            {
                AddLog(result, $"Não foi possível interpretar a Massa Líquida do bloco: \"{massaBlocoTexto}\".");
                return;
            }

            if (peca == null)
                return;

            double massaCalculada;

            try
            {
                IMassProperty propriedadesDeMassa = peca.Extension.CreateMassProperty();
                massaCalculada = propriedadesDeMassa.Mass;
            }
            catch (COMException ex)
            {
                AddLog(result, "Não foi possível calcular a massa da peça: " + ex.Message);
                return;
            }

            AddLog(result,
                $"Massa Líquida do bloco : {massaBloco:0.###} kg | Massa calculada da peça : {massaCalculada:0.###} kg");

            double diferenca = Math.Abs(massaCalculada - massaBloco);
            double toleranciaAbsoluta = Math.Max(massaCalculada, massaBloco) * ToleranciaMassa;

            if (diferenca > toleranciaAbsoluta)
            {
                // Erro clássico de configuração de template: a unidade de
                // Massa do documento (Ferramentas > Opções > Propriedades do
                // Documento > Unidades > Massa) está em gramas em vez de
                // quilogramas (ou vice-versa) -- CreateMassProperty().Mass
                // devolve o valor cru nessa unidade do documento, não
                // necessariamente kg. Detecta esse caso específico (razão
                // ~1000x entre os dois valores, característica de um mix-up
                // kg/g) pra dar um diagnóstico mais direto do que só
                // "diverge", já que a causa mais provável não é a peça em si.
                double proporcao = massaCalculada > 0 ? massaBloco / massaCalculada : 0;

                bool pareceErroDeUnidade = proporcao > 0 &&
                    (EhProximoDe(proporcao, 1000) || EhProximoDe(proporcao, 0.001));

                if (pareceErroDeUnidade)
                {
                    AddError(result,
                        $"Massa Líquida do bloco ({massaBloco:0.###}) e massa calculada da peça ({massaCalculada:0.###}) " +
                        "diferem por um fator de ~1000 -- parece erro de unidade (g em vez de kg, ou vice-versa). " +
                        "Confira Ferramentas > Opções > Propriedades do Documento > Unidades > Massa.");

                    result.CamposDivergentes.Add("Massa Líquida");
                    result.AddWarning("A unidade de Massa do documento parece estar configurada errada (g/kg trocados).");
                }
                else
                {
                    AddError(result,
                        $"Massa Líquida do bloco ({massaBloco:0.###} kg) diverge da massa calculada da peça ({massaCalculada:0.###} kg) em mais de 5%.");

                    result.CamposDivergentes.Add("Massa Líquida");
                    result.AddWarning("Divergência entre a Massa Líquida do bloco e a massa calculada da peça.");
                }
            }
            else
            {
                result.CamposVerificados.Add("Massa Líquida");
            }
        }

        // Converte um texto de massa da legenda (ex.: "2,383 kg") pra um
        // double, aceitando vírgula decimal e o sufixo "kg".
        private static bool TentarConverterMassa(string texto, out double valor)
        {
            valor = 0;

            string limpo = Regex.Replace(texto, "[^0-9,.-]", "").Replace(',', '.');

            return double.TryParse(limpo, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
        }

        // Tolerância de 5% em torno do alvo -- o bastante pra pegar o caso
        // de mix-up kg/g mesmo com arredondamento no valor do bloco, sem
        // disparar em divergências "normais" que não chegam nem perto de
        // 1000x.
        private static bool EhProximoDe(double valor, double alvo)
        {
            return Math.Abs(valor - alvo) <= Math.Abs(alvo) * 0.05;
        }

        private void RegistrarCampo(CheckResult result, string rotulo, string valor)
        {
            bool vazio = string.IsNullOrWhiteSpace(valor) ||
                valor.IndexOf("not specified", StringComparison.OrdinalIgnoreCase) >= 0;

            result.Fields[rotulo] = vazio ? "" : valor;

            AddLog(result, $"{rotulo} : {(vazio ? "(vazio)" : valor)}");

            if (vazio)
            {
                AddError(result, $"Campo \"{rotulo}\" não preenchido no Bloco Legenda WAU.");
            }
        }

        private class RegraDivergenciaMaterial
        {
            public readonly string[] GatilhosMaterial;
            public readonly string[] EsperadosMateriaPrima;

            public RegraDivergenciaMaterial(string[] gatilhosMaterial, string[] esperadosMateriaPrima)
            {
                GatilhosMaterial = gatilhosMaterial;
                EsperadosMateriaPrima = esperadosMateriaPrima;
            }
        }

        // Correspondência esperada entre o campo "Material" (propriedade
        // nativa do SolidWorks) e o campo "Matéria-Prima" (bloco de legenda
        // WAU) -- se o Material contém um dos gatilhos abaixo, a
        // Matéria-Prima precisa conter pelo menos um dos textos esperados
        // correspondentes.
        private static readonly RegraDivergenciaMaterial[] RegrasDivergenciaMaterial =
        {
            new RegraDivergenciaMaterial(new[] { "Aluminio", "Aluminio 6063-T6" }, new[] { "Alum" }),
            // "ZIN" cobre a abreviação usada na Matéria-Prima pra chapa de
            // aço zincado (ex.: "CH.ACO ZIN 2,66mm (#12)") -- sem ela, esse
            // caso batia no gatilho "ZINCADO" mas não encontrava "ACO Zn"
            // (com "Zn" de zinco) nem "GALVALUME" no texto, e acusava
            // divergência num par de campos que na verdade estava correto.
            new RegraDivergenciaMaterial(new[] { "ZINCADO" }, new[] { "ACO Zn", "ZIN", "GALVALUME" }),
            new RegraDivergenciaMaterial(new[] { "policarbonato" }, new[] { "policarbonato" }),
            new RegraDivergenciaMaterial(new[] { "Cobre" }, new[] { "COBRE" }),
            new RegraDivergenciaMaterial(new[] { "Aco inox" }, new[] { "INOX" }),
            new RegraDivergenciaMaterial(new[] { "Aco 1045", "Aco 1020" }, new[] { "aco" }),
            new RegraDivergenciaMaterial(new[] { "Galvanized Plate" }, new[] { "GALVANIZED" }),
            new RegraDivergenciaMaterial(new[] { "Aluzinc Plate" }, new[] { "ALUZINC" }),
            new RegraDivergenciaMaterial(new[] { "Steel Plate" }, new[] { "STEEL" }),
            new RegraDivergenciaMaterial(new[] { "Aluminum" }, new[] { "ALUM" }),
            new RegraDivergenciaMaterial(new[] { "Copper" }, new[] { "COPPER" }),
            new RegraDivergenciaMaterial(new[] { "Stainless Steel" }, new[] { "STAINLESS" }),
            new RegraDivergenciaMaterial(new[] { "Polycarbonate" }, new[] { "Polycarbonate" }),
        };

        // Regra: o campo Material (nativo do SolidWorks) e o campo
        // Matéria-Prima (do bloco de legenda WAU) devem descrever o mesmo
        // tipo de material. Se um bater com um dos gatilhos conhecidos mas o
        // outro não contiver o texto esperado correspondente, marca como
        // divergência (erro + aviso + destaque nas duas colunas).
        private void VerificarDivergenciaMaterial(CheckResult result)
        {
            string material;
            result.Fields.TryGetValue("Material", out material);

            string materiaPrima;
            result.Fields.TryGetValue("Matéria-Prima", out materiaPrima);

            if (string.IsNullOrWhiteSpace(material) || string.IsNullOrWhiteSpace(materiaPrima))
                return;

            foreach (RegraDivergenciaMaterial regra in RegrasDivergenciaMaterial)
            {
                if (!ContemAlgum(material, regra.GatilhosMaterial))
                    continue;

                if (ContemAlgum(materiaPrima, regra.EsperadosMateriaPrima))
                {
                    result.CamposVerificados.Add("Material");
                    result.CamposVerificados.Add("Matéria-Prima");
                    return;
                }

                AddLog(result, $"Divergência: Material = \"{material}\", Matéria-Prima = \"{materiaPrima}\".");
                AddError(result, $"Divergência entre Material (\"{material}\") e Matéria-Prima (\"{materiaPrima}\").");
                result.AddWarning("Divergência entre os campos Material e Matéria-Prima.");

                result.CamposDivergentes.Add("Material");
                result.CamposDivergentes.Add("Matéria-Prima");

                return;
            }
        }

        // Sem tirar acento, "Aço 1020" (como o SolidWorks devolve, com
        // cedilha) nunca batia com o gatilho "Aco 1020" (sem cedilha) --
        // IndexOf com OrdinalIgnoreCase ignora maiúsculas/minúsculas, mas não
        // trata "ç"/"c" ou "í"/"i" como equivalentes. Resultado: nenhuma
        // regra batia (nem a de sucesso, nem a de divergência), e o campo
        // ficava sem destaque nenhum -- nem verde, nem laranja -- em vez de
        // reconhecer que "Aço 1020" e "CH.ACO 1,90mm" são o mesmo material.
        private static bool ContemAlgum(string texto, string[] substrings)
        {
            string textoSemAcento = RemoverAcentos(texto);

            foreach (string substring in substrings)
            {
                if (textoSemAcento.IndexOf(RemoverAcentos(substring), StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string RemoverAcentos(string texto)
        {
            string decomposto = texto.Normalize(NormalizationForm.FormD);
            StringBuilder semAcento = new StringBuilder(decomposto.Length);

            foreach (char c in decomposto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    semAcento.Append(c);
            }

            return semAcento.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
