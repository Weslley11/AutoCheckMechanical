using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AutoCheckMechanical.Services
{
    // Em vez de automatizar o SAP GUI direto (SapService.cs, cheio de
    // particularidades da API de scripting do SAP), delega a busca/download
    // para a planilha Excel com a macro VBA do usuário — que já sabe fazer
    // isso de verdade. Este serviço só abre/ativa a planilha, escreve a ECM,
    // roda a macro e lê de volta a lista de documentos que ela escreve na
    // coluna F.
    //
    // Assim como SapService, usa Type.InvokeMember (reflection sobre o RCW)
    // em vez de "dynamic", pelo mesmo motivo: evita depender de type library
    // e das particularidades do binder do C# para objetos COM.
    public static class ExcelSapService
    {
        private const string NomePlanilha = "Planilha1";
        private const string CelulaEcm = "F4";
        private const string NomeMacro = "Check";
        private const int PrimeiraLinhaLista = 7;
        private const int UltimaLinhaLista = 5000;

        // Abre (ou reaproveita) a planilha, escreve a ECM, roda a macro Check()
        // e devolve os documentos que ela escreveu em Planilha1!F7:F5000.
        //
        // ATENÇÃO: se a macro tiver um MsgBox no final (o "Processo concluído!"
        // do script original), essa chamada trava esperando alguém clicar OK,
        // porque Application.Run só retorna quando a Sub termina. Remova ou
        // comente esse MsgBox na macro para rodar via automação.
        public static List<string> BuscarEBaixarViaMacro(string caminhoPlanilha, string ecm)
        {
            object excel = ObterOuAbrirExcel();
            object workbook = AbrirOuAtivarPlanilha(excel, caminhoPlanilha);

            object planilha = Chamar(Get(workbook, "Sheets"), "Item", NomePlanilha);

            Definir(Chamar(planilha, "Range", CelulaEcm), "Value", ecm);

            string nomeArquivo = System.IO.Path.GetFileName(caminhoPlanilha);

            try
            {
                Chamar(excel, "Run", "'" + nomeArquivo + "'!" + NomeMacro);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Não foi possível rodar a macro \"" + NomeMacro + "\" em \"" + nomeArquivo + "\". " +
                    "Confirme o nome da macro e se ela não está travada esperando um MsgBox/InputBox ser fechado.\n\n" +
                    "Erro original: " + DescreverErro(ex), ex);
            }

            List<string> documentos = new List<string>();

            for (int linha = PrimeiraLinhaLista; linha <= UltimaLinhaLista; linha++)
            {
                object celula = Chamar(planilha, "Range", "F" + linha);
                object valor = Get(celula, "Value");
                string texto = valor?.ToString().Trim();

                if (string.IsNullOrEmpty(texto))
                    break;

                documentos.Add(texto);
            }

            return documentos;
        }

        private static object ObterOuAbrirExcel()
        {
            try
            {
                return Marshal.GetActiveObject("Excel.Application");
            }
            catch (COMException)
            {
                Type tipoExcel = Type.GetTypeFromProgID("Excel.Application");

                if (tipoExcel == null)
                    throw new InvalidOperationException("Excel não está instalado nesta máquina.");

                object excel = Activator.CreateInstance(tipoExcel);
                Definir(excel, "Visible", true);
                return excel;
            }
        }

        private static object AbrirOuAtivarPlanilha(object excel, string caminhoPlanilha)
        {
            if (!System.IO.File.Exists(caminhoPlanilha))
                throw new InvalidOperationException("Planilha não encontrada: " + caminhoPlanilha);

            object workbooks = Get(excel, "Workbooks");
            string nomeArquivo = System.IO.Path.GetFileName(caminhoPlanilha);

            int total = (int)Get(workbooks, "Count");

            for (int i = 1; i <= total; i++)
            {
                object workbookAberto = Chamar(workbooks, "Item", i);
                string nome = (string)Get(workbookAberto, "Name");

                if (string.Equals(nome, nomeArquivo, StringComparison.OrdinalIgnoreCase))
                    return workbookAberto;
            }

            return Chamar(workbooks, "Open", caminhoPlanilha);
        }

        private static string DescreverErro(Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;

            COMException comEx = ex as COMException;

            return comEx != null
                ? $"{ex.Message} (HRESULT 0x{comEx.HResult:X8})"
                : $"{ex.GetType().Name}: {ex.Message}";
        }

        // Lê propriedade OU chama método sem args (combina as duas flags porque
        // alguns membros COM são ambíguos dependendo de como foram declarados).
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

        // Para métodos/propriedades parametrizadas (Range("F4"), Sheets.Item(nome),
        // Workbooks.Open(caminho), Application.Run(macro)).
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
