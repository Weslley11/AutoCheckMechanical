using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AutoCheckMechanical.Core;
using AutoCheckMechanical.Helpers;
using AutoCheckMechanical.Models;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Checkers
{
    public class TitleBlockChecker : CheckerBase
    {
        public override string Name => "Bloco de Título";

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
        };

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
            }

            if (!flatPatternEncontrada)
            {
                AddError(result, "Flat Pattern não encontrada.");
                return result;
            }

            if (result.Errors.Count == 0)
            {
                result.Message = "Todos os campos do bloco de título preenchidos.";
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
                    object varLibProperties;

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

        private void RegistrarCampo(CheckResult result, string rotulo, string valor)
        {
            bool vazio = string.IsNullOrWhiteSpace(valor) ||
                valor.IndexOf("not specified", StringComparison.OrdinalIgnoreCase) >= 0;

            result.Fields[rotulo] = vazio ? "" : valor;

            AddLog(result, $"{rotulo} : {(vazio ? "(vazio)" : valor)}");

            if (vazio)
            {
                AddError(result, $"Campo \"{rotulo}\" não preenchido no bloco de título.");
            }
        }
    }
}
