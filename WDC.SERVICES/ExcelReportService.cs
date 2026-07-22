using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using WDC.MODEL;

namespace WDC.SERVICES
{
    // Gera o relatório de verificação como uma planilha Excel de verdade
    // (.xlsx), formatada (cabeçalho, cores por status, campos divergentes em
    // vermelho, colunas ajustadas), em vez de um CSV simples.
    //
    // Usa Type.InvokeMember (reflection sobre o RCW) em vez de "dynamic",
    // pelo mesmo motivo já resolvido antes: TYPE_E_CANTLOADLIBRARY ao usar
    // "dynamic" com a lib de interop do Excel.
    //
    // Abre uma instância própria e invisível do Excel (não reaproveita a
    // instância da planilha de macro do SAP), gera o relatório e fecha tudo
    // no final.
    public static class ExcelReportService
    {
        private const int XlOpenXmlWorkbook = 51; // formato .xlsx
        private const int XlContinuous = 1;

        public static void GerarRelatorio(
            List<BatchFileResult> resultados,
            List<string> checkerNames,
            string[] camposTitulo,
            string caminhoDestino)
        {
            if (resultados.Count == 0)
                return;

            object excel = null;
            object workbook = null;

            try
            {
                Type tipoExcel = Type.GetTypeFromProgID("Excel.Application");

                if (tipoExcel == null)
                    throw new InvalidOperationException("Excel não está instalado nesta máquina.");

                excel = Activator.CreateInstance(tipoExcel);
                Definir(excel, "Visible", false);
                Definir(excel, "DisplayAlerts", false);

                object workbooks = Get(excel, "Workbooks");
                workbook = Chamar(workbooks, "Add");

                object planilha = Get(workbook, "ActiveSheet");

                EscreverCabecalho(planilha, checkerNames, camposTitulo);
                EscreverLinhas(planilha, resultados, checkerNames, camposTitulo);
                Formatar(excel, planilha);

                Chamar(workbook, "SaveAs", caminhoDestino, XlOpenXmlWorkbook);
            }
            finally
            {
                if (workbook != null)
                {
                    try { Chamar(workbook, "Close", false); } catch (Exception) { }
                    Marshal.ReleaseComObject(workbook);
                }

                if (excel != null)
                {
                    try { Chamar(excel, "Quit"); } catch (Exception) { }
                    Marshal.ReleaseComObject(excel);
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private static void EscreverCabecalho(object planilha, List<string> checkerNames, string[] camposTitulo)
        {
            List<string> cabecalho = new List<string> { "ARQUIVO", "DOCUMENTO", "TIPO", "PARTE", "VERSÃO", "DESCRIÇÃO", "PDF" };
            cabecalho.AddRange(checkerNames.Select(n => n.ToUpper()));
            cabecalho.AddRange(camposTitulo.Select(n => n.ToUpper()));
            cabecalho.Add("FOLHAS");
            cabecalho.Add("OBSERVAÇÃO");
            cabecalho.Add("ERROS DETALHADOS");

            for (int c = 0; c < cabecalho.Count; c++)
            {
                object celula = EscreverCelula(planilha, 1, c + 1, cabecalho[c]);

                Definir(Get(celula, "Font"), "Bold", true);
                Definir(Get(celula, "Font"), "Color", Bgr(255, 255, 255));
                Definir(Get(celula, "Interior"), "Color", Bgr(0x2F, 0x4F, 0x64));
            }
        }

        private static void EscreverLinhas(
            object planilha,
            List<BatchFileResult> resultados,
            List<string> checkerNames,
            string[] camposTitulo)
        {
            int linha = 2;

            foreach (BatchFileResult item in resultados)
            {
                int coluna = 1;

                EscreverCelula(planilha, linha, coluna++, item.FileName);
                EscreverCelula(planilha, linha, coluna++, item.DocumentoNumero);
                EscreverCelula(planilha, linha, coluna++, item.DocumentoTipo);
                EscreverCelula(planilha, linha, coluna++, item.DocumentoParte);
                EscreverCelula(planilha, linha, coluna++, item.DocumentoVersao);
                EscreverCelula(planilha, linha, coluna++, item.DocumentoDescricao);
                EscreverCelula(planilha, linha, coluna++,
                    string.IsNullOrEmpty(item.DocumentoNumero) ? "" : (item.DocumentoTemPdf ? "OK" : "SEM PDF"));

                List<string> errosDetalhados = new List<string>();

                foreach (string nomeChecker in checkerNames)
                {
                    CheckResult resultado = item.Results.Find(x => x.Checker == nomeChecker);

                    EscreverStatusChecker(planilha, linha, coluna++, item, resultado, errosDetalhados, nomeChecker);
                }

                CheckResult resultadoBlocoTitulo = item.Results.Find(x => x.Checker == "Bloco Legenda WAU");

                foreach (string nomeCampo in camposTitulo)
                {
                    string valor = null;
                    bool divergente = false;
                    bool verificado = false;

                    if (resultadoBlocoTitulo != null)
                    {
                        resultadoBlocoTitulo.Fields.TryGetValue(nomeCampo, out valor);
                        divergente = resultadoBlocoTitulo.CamposDivergentes.Contains(nomeCampo);
                        verificado = resultadoBlocoTitulo.CamposVerificados.Contains(nomeCampo);
                    }

                    object celula = EscreverCelula(planilha, linha, coluna++, valor ?? "");

                    if (divergente)
                    {
                        Definir(Get(celula, "Font"), "Bold", true);
                        Definir(Get(celula, "Font"), "Color", Bgr(156, 0, 6));
                    }
                    else if (verificado)
                    {
                        Definir(Get(celula, "Font"), "Bold", true);
                        Definir(Get(celula, "Font"), "Color", Bgr(0, 128, 0));
                    }
                }

                EscreverCelula(planilha, linha, coluna++, item.OpenFailed ? "" : item.SheetCount.ToString());

                List<string> avisos = item.Results.SelectMany(r => r.Warnings).Distinct().ToList();
                EscreverCelula(planilha, linha, coluna++, string.Join(" | ", avisos));

                EscreverCelula(planilha, linha, coluna++, string.Join(" | ", errosDetalhados));

                linha++;
            }
        }

        private static void EscreverStatusChecker(
            object planilha,
            int linha,
            int coluna,
            BatchFileResult item,
            CheckResult resultado,
            List<string> errosDetalhados,
            string nomeChecker)
        {
            string texto;
            int corFundo;
            int corFonte;

            if (item.OpenFailed || resultado == null)
            {
                texto = "—";
                corFundo = Bgr(230, 230, 230);
                corFonte = Bgr(120, 120, 120);
            }
            else if (resultado.Skipped)
            {
                texto = "N/A";
                corFundo = Bgr(230, 230, 230);
                corFonte = Bgr(120, 120, 120);
            }
            else if (resultado.Success)
            {
                texto = "OK";
                corFundo = Bgr(198, 239, 206);
                corFonte = Bgr(0, 97, 0);
            }
            else
            {
                texto = "ERRO";
                corFundo = Bgr(255, 199, 206);
                corFonte = Bgr(156, 0, 6);

                errosDetalhados.AddRange(resultado.Errors.Select(erro => $"{nomeChecker}: {erro}"));
            }

            object celula = EscreverCelula(planilha, linha, coluna, texto);

            Definir(Get(celula, "Font"), "Bold", true);
            Definir(Get(celula, "Font"), "Color", corFonte);
            Definir(Get(celula, "Interior"), "Color", corFundo);
            Definir(celula, "HorizontalAlignment", -4108); // xlCenter
        }

        private static void Formatar(object excel, object planilha)
        {
            object intervaloUsado = Get(planilha, "UsedRange");

            Definir(Get(intervaloUsado, "Borders"), "LineStyle", XlContinuous);

            Chamar(Get(intervaloUsado, "Columns"), "AutoFit");

            object janela = Get(excel, "ActiveWindow");
            Definir(janela, "SplitColumn", 0);
            Definir(janela, "SplitRow", 1);
            Definir(janela, "FreezePanes", true);
        }

        private static object EscreverCelula(object planilha, int linha, int coluna, string valor)
        {
            object celula = Chamar(planilha, "Cells", linha, coluna);
            Definir(celula, "Value", valor ?? "");
            return celula;
        }

        // Cor no formato que o Excel espera (BGR empacotado num inteiro,
        // igual a função RGB() do VBA), não RGB direto.
        private static int Bgr(int r, int g, int b)
        {
            return r + (g << 8) + (b << 16);
        }

        private static object Get(object alvo, string nome)
        {
            return alvo.GetType().InvokeMember(
                nome,
                BindingFlags.GetProperty | BindingFlags.InvokeMethod,
                null,
                alvo,
                new object[0]);
        }

        private static void Definir(object alvo, string nome, object valor)
        {
            alvo.GetType().InvokeMember(
                nome,
                BindingFlags.SetProperty,
                null,
                alvo,
                new[] { valor });
        }

        private static object Chamar(object alvo, string nome, params object[] args)
        {
            return alvo.GetType().InvokeMember(
                nome,
                BindingFlags.GetProperty | BindingFlags.InvokeMethod,
                null,
                alvo,
                args ?? new object[0]);
        }
    }
}
