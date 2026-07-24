using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using WDC.SERVICES.Core;

namespace WDC.SERVICES.Helpers
{
    public static class WauBlockHelper
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

        // Confirmado com a macro de inserção do bloco da própria empresa: o
        // bloco é selecionável por nome via SelectByID2 com o tipo
        // "SUBSKETCHDEF" (é assim que a macro localiza o bloco antigo antes
        // de apagar e inserir a versão atual).
        public static bool TemBlocoLegendaWau(ModelDoc2 modelo)
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

        public static string GetMateriaPrima(CheckContext context)
        {
            string materiaPrima = PropertyHelper.GetValue(context.Model, NomePropriedadeMateriaPrima);

            if (!string.IsNullOrWhiteSpace(materiaPrima))
                return materiaPrima;

            foreach (View view in context.Views)
            {
                ModelDoc2 peca = view.ReferencedDocument as ModelDoc2;
                materiaPrima = PropertyHelper.GetValue(peca, NomePropriedadeMateriaPrima);

                if (!string.IsNullOrWhiteSpace(materiaPrima))
                    return materiaPrima;
            }

            return materiaPrima;
        }

        public static bool TemVistaPlanificada(CheckContext context)
        {
            foreach (View view in context.Views)
            {
                if (view.IsFlatPatternView())
                    return true;
            }

            return false;
        }

        // Um desenho é "de montagem" quando pelo menos uma das vistas
        // principais referencia um documento do tipo swDocASSEMBLY.
        // Diferente de checar Matéria-Prima vazia (que é só uma pista
        // indireta), isso confirma diretamente que o modelo referenciado é
        // uma montagem -- e montagem estruturalmente nunca tem planificação
        // (flat pattern é conceito de peça de chapa), então os checks de
        // chapa não fazem sentido nenhum aqui, independente do preenchimento
        // de Matéria-Prima no desenho.
        public static bool EhDesenhoDeMontagem(CheckContext context)
        {
            foreach (View view in context.Views)
            {
                ModelDoc2 modeloReferenciado = view.ReferencedDocument as ModelDoc2;

                if (modeloReferenciado != null && modeloReferenciado.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                    return true;
            }

            return false;
        }

        // Regra: desenho de montagem (confirmado pelo tipo do modelo
        // referenciado) OU Matéria-Prima NÃO preenchida + nenhuma vista
        // planificada (com ou sem o bloco de legenda WAU inserido) =>
        // provavelmente o desenho não tem obrigatoriedade de informações de
        // chapa/dobra. Nesse caso os checks de Layer, Flat Pattern e Scale
        // devem ser dispensados (não é "OK" e nem "ERRO"). O usuário pode
        // forçar a execução mesmo assim (context.ForcarChecksDeChapa) através
        // de um botão na tela.
        public static bool DesenhoDispensaChecksDeChapa(CheckContext context)
        {
            if (context.ForcarChecksDeChapa)
                return false;

            if (!context.IsDrawing)
                return false;

            if (EhDesenhoDeMontagem(context))
                return true;

            if (!string.IsNullOrWhiteSpace(GetMateriaPrima(context)))
                return false;

            if (TemVistaPlanificada(context))
                return false;

            return true;
        }
    }
}
