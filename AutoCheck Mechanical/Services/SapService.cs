using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        public static dynamic Conectar()
        {
            object sapGuiAuto;

            try
            {
                sapGuiAuto = Marshal.GetActiveObject("SAPGUI");
            }
            catch (COMException)
            {
                throw new InvalidOperationException(
                    "SAP não está aberto. Abra o SAP Logon, conecte na sessão e tente novamente.");
            }

            dynamic app = ((dynamic)sapGuiAuto).GetScriptingEngine;
            dynamic connection = app.Children(0);
            dynamic session = connection.Children(0);

            if (session == null)
                throw new InvalidOperationException("Não foi possível obter a sessão do SAP GUI.");

            return session;
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
