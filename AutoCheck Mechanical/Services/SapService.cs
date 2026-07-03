using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Windows.Threading;

namespace AutoCheckMechanical.Services
{
    // Automação da transação ZTPLM025 via SAP GUI Scripting, portada do macro VBA
    // fornecido pelo usuário. Usa "dynamic" (late binding COM) em vez de uma
    // referência COM forte, porque não precisa de nenhuma DLL/typelib adicional
    // no projeto — só precisa do SAP Logon aberto com o Scripting habilitado,
    // exatamente como o VBA original.
    public static class SapService
    {
        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable pprot);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

        public static dynamic Conectar()
        {
            // Marshal.GetActiveObject("SAPGUI") não funciona a partir de .NET: ele tenta
            // resolver "SAPGUI" como um ProgID (CLSIDFromProgID), mas o SAP GUI só registra
            // esse nome na Running Object Table como moniker de item ("!SAPGUI"), não como
            // ProgID de verdade — daí o erro CO_E_CLASSSTRING (0x800401F3). O VBA consegue
            // pegar o objeto porque o GetObject dele tem um fallback para casos assim.
            // A saída documentada pela SAP fora do VBA é o componente auxiliar
            // "SapROTWrapper", mas ele é opcional e pode não estar instalado/registrado
            // (foi o caso aqui) — então em vez de depender dele, procuramos direto na ROT
            // usando só APIs do Win32/COM (GetRunningObjectTable + enumeração de monikers).
            object sapGuiAuto = ObterSapGuiDaRot();

            if (sapGuiAuto == null)
                throw new InvalidOperationException(
                    "Não foi encontrado nenhum objeto \"!SAPGUI\" ativo na Running Object Table. " +
                    "Confirme que o SAP Logon está aberto, conectado, e que \"Habilitar script\" está marcado " +
                    "em Ajustar Layout Local > Opções > Acessibilidade e Script > Script.");

            dynamic app;
            dynamic connection;
            dynamic session;

            try
            {
                app = ((dynamic)sapGuiAuto).GetScriptingEngine;
                connection = app.Children(0);
                session = connection.Children(0);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "O objeto \"SAPGUI\" foi encontrado, mas não foi possível obter a sessão ativa. " +
                    "Confirme que há uma conexão/sessão SAP aberta em primeiro plano.\n\n" +
                    "Erro original: " + DescreverErro(ex), ex);
            }

            if (session == null)
                throw new InvalidOperationException("Não foi possível obter a sessão do SAP GUI.");

            return session;
        }

        private static object ObterSapGuiDaRot()
        {
            IRunningObjectTable rot;

            if (GetRunningObjectTable(0, out rot) != 0 || rot == null)
                return null;

            IEnumMoniker enumMoniker;
            rot.EnumRunning(out enumMoniker);

            if (enumMoniker == null)
                return null;

            IBindCtx bindCtx;
            CreateBindCtx(0, out bindCtx);

            IMoniker[] monikers = new IMoniker[1];

            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                string nome;

                try
                {
                    monikers[0].GetDisplayName(bindCtx, null, out nome);
                }
                catch (COMException)
                {
                    continue;
                }

                if (string.Equals(nome, "!SAPGUI", StringComparison.OrdinalIgnoreCase))
                {
                    object obj;
                    rot.GetObject(monikers[0], out obj);
                    return obj;
                }
            }

            return null;
        }

        private static string DescreverErro(Exception ex)
        {
            COMException comEx = ex as COMException;

            return comEx != null
                ? $"{ex.Message} (HRESULT 0x{comEx.HResult:X8})"
                : ex.Message;
        }

        // Abre a ZTPLM025, filtra por ECM + tipo de documento SWD e retorna
        // os números de documento (DOKNR) distintos encontrados na grid.
        public static List<string> BuscarDocumentosPorEcm(dynamic session, string ecm)
        {
            session.findById("wnd[0]").Maximize();

            session.findById("wnd[0]/tbar[0]/okcd").Text = "/nZTPLM025";
            session.findById("wnd[0]").SendVKey(0);

            session.findById("wnd[0]/usr/ctxtS_DOKAR-LOW").Text = "SWD";
            session.findById("wnd[0]/usr/ctxtS_AENNR-LOW").Text = ecm;
            session.findById("wnd[0]/usr/txtP_MAX").Text = "";

            session.findById("wnd[0]/tbar[1]/btn[8]").Press();

            EsperarSap(session);

            dynamic grid = ObterGrid(session);

            List<string> documentos = new List<string>();
            HashSet<string> vistos = new HashSet<string>();

            int totalLinhas = grid.RowCount;

            for (int i = 0; i < totalLinhas; i++)
            {
                string doc = ((string)grid.GetCellValue(i, "DOKNR")).Trim();

                if (!string.IsNullOrEmpty(doc) && vistos.Add(doc))
                    documentos.Add(doc);
            }

            return documentos;
        }

        // Seleciona toda a grid, marca "última versão" e aciona o botão de
        // cópia local (btn[23]), exatamente como no macro original.
        //
        // ATENÇÃO: a partir daqui o SAP abre a tela/diálogo de download
        // ("Copiar local"), e essa parte NÃO está automatizada ainda — o
        // próprio macro original também deixou isso como placeholder. Os IDs
        // dos campos dessa tela dependem da configuração da transação nesta
        // empresa e não podem ser adivinhados com segurança. Para habilitar:
        //   1. No SAP GUI: Ajustar Layout Local > Script Recording and Playback.
        //   2. Grave clicando em "Copiar local" e escolhendo uma pasta.
        //   3. Cole o código gerado aqui para preencher PreencherDialogoDownload.
        public static void SelecionarTudoEBaixar(dynamic session, string pastaDestino)
        {
            dynamic grid = ObterGrid(session);

            grid.SelectAll();

            session.findById("wnd[0]/usr/radV_BTN_MAIOR").Select();
            session.findById("wnd[0]/tbar[1]/btn[23]").Press();

            EsperarSap(session);

            PreencherDialogoDownload(session, pastaDestino);
        }

        private static void PreencherDialogoDownload(dynamic session, string pastaDestino)
        {
            throw new NotImplementedException(
                "Automação do diálogo de download (\"Copiar local\") ainda não configurada. " +
                "Grave essa ação no SAP GUI Scripting e informe os IDs dos campos para " +
                "completar SapService.PreencherDialogoDownload.");
        }

        private static dynamic ObterGrid(dynamic session)
        {
            return session.findById(
                "wnd[0]/usr/cntlC_CONTAINER/shellcont/shellcont/shell/shellcont[0]/shell");
        }

        private static void EsperarSap(dynamic session)
        {
            int tentativas = 0;

            while (session.Busy && tentativas < 300)
            {
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                Thread.Sleep(50);
                tentativas++;
            }
        }
    }
}
