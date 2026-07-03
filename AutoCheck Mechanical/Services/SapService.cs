using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Windows.Threading;

namespace AutoCheckMechanical.Services
{
    // Automação da transação ZTPLM025 via SAP GUI Scripting, portada do macro VBA
    // fornecido pelo usuário.
    //
    // Não usamos "dynamic" aqui: o binder do C# tenta carregar a type library
    // do objeto COM para montar uma ligação mais rica, e isso pode falhar com
    // TYPE_E_CANTLOADLIBRARY (0x80029C4A) dependendo de como o SAP GUI registrou
    // sua typelib (foi o caso em produção). Em vez disso, chamamos tudo via
    // Type.InvokeMember (reflection clássica sobre IDispatch), que é o
    // equivalente mais próximo do que o runtime do VBA faz por baixo dos panos.
    public static class SapService
    {
        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable pprot);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

        public static object Conectar()
        {
            // Marshal.GetActiveObject("SAPGUI") não funciona a partir de .NET: ele tenta
            // resolver "SAPGUI" como um ProgID (CLSIDFromProgID), mas o SAP GUI só registra
            // esse nome na Running Object Table como moniker de item, não como ProgID de
            // verdade — daí o erro CO_E_CLASSSTRING (0x800401F3) visto anteriormente. Por
            // isso procuramos direto na ROT usando só APIs do Win32/COM.
            List<string> nomesEncontrados;
            object sapGuiAuto = ObterSapGuiDaRot(out nomesEncontrados);

            if (sapGuiAuto == null)
            {
                string listagem = nomesEncontrados.Count == 0
                    ? "(a Running Object Table está vazia agora — nenhum objeto COM ativo encontrado)"
                    : "Objetos ativos encontrados na ROT agora:\n - " + string.Join("\n - ", nomesEncontrados);

                throw new InvalidOperationException(
                    "Não foi encontrado nenhum objeto do SAP GUI ativo na Running Object Table.\n\n" +
                    listagem + "\n\n" +
                    "Confirme que o SAP Logon está aberto, conectado, e que \"Habilitar script\" está marcado " +
                    "em Ajustar Layout Local > Opções > Acessibilidade e Script > Script.");
            }

            object session;

            try
            {
                object app = Get(sapGuiAuto, "GetScriptingEngine");
                object connection = Invoke(app, "Children", 0);
                session = Invoke(connection, "Children", 0);
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

        // Retorna o objeto SAPGUI ativo na Running Object Table, se houver. Em
        // "nomesEncontrados" devolve o nome de exibição de TODO objeto ativo
        // encontrado na ROT (mesmo quando não é o SAPGUI), para diagnóstico —
        // assim dá pra ver de verdade o que está registrado em vez de adivinhar.
        private static object ObterSapGuiDaRot(out List<string> nomesEncontrados)
        {
            nomesEncontrados = new List<string>();

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
            object encontrado = null;

            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                string nome;

                try
                {
                    monikers[0].GetDisplayName(bindCtx, null, out nome);
                }
                catch (COMException ex)
                {
                    nomesEncontrados.Add("(erro ao ler nome: " + DescreverErro(ex) + ")");
                    continue;
                }

                nomesEncontrados.Add(nome);

                if (encontrado == null && nome.IndexOf("SAPGUI", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    object obj;
                    rot.GetObject(monikers[0], out obj);
                    encontrado = obj;
                }
            }

            return encontrado;
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
        public static List<string> BuscarDocumentosPorEcm(object session, string ecm)
        {
            Invoke(FindById(session, "wnd[0]"), "Maximize");

            Set(FindById(session, "wnd[0]/tbar[0]/okcd"), "Text", "/nZTPLM025");
            Invoke(FindById(session, "wnd[0]"), "SendVKey", 0);

            Set(FindById(session, "wnd[0]/usr/ctxtS_DOKAR-LOW"), "Text", "SWD");
            Set(FindById(session, "wnd[0]/usr/ctxtS_AENNR-LOW"), "Text", ecm);
            Set(FindById(session, "wnd[0]/usr/txtP_MAX"), "Text", "");

            Invoke(FindById(session, "wnd[0]/tbar[1]/btn[8]"), "Press");

            EsperarSap(session);

            object grid = ObterGrid(session);

            List<string> documentos = new List<string>();
            HashSet<string> vistos = new HashSet<string>();

            int totalLinhas = (int)Get(grid, "RowCount");

            for (int i = 0; i < totalLinhas; i++)
            {
                string doc = ((string)Invoke(grid, "GetCellValue", i, "DOKNR")).Trim();

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
        public static void SelecionarTudoEBaixar(object session, string pastaDestino)
        {
            object grid = ObterGrid(session);

            Invoke(grid, "SelectAll");

            Invoke(FindById(session, "wnd[0]/usr/radV_BTN_MAIOR"), "Select");
            Invoke(FindById(session, "wnd[0]/tbar[1]/btn[23]"), "Press");

            EsperarSap(session);

            PreencherDialogoDownload(session, pastaDestino);
        }

        private static void PreencherDialogoDownload(object session, string pastaDestino)
        {
            throw new NotImplementedException(
                "Automação do diálogo de download (\"Copiar local\") ainda não configurada. " +
                "Grave essa ação no SAP GUI Scripting e informe os IDs dos campos para " +
                "completar SapService.PreencherDialogoDownload.");
        }

        private static object ObterGrid(object session)
        {
            return FindById(session, "wnd[0]/usr/cntlC_CONTAINER/shellcont/shellcont/shell/shellcont[0]/shell");
        }

        private static object FindById(object session, string id)
        {
            return Invoke(session, "findById", id);
        }

        private static void EsperarSap(object session)
        {
            int tentativas = 0;

            while ((bool)Get(session, "Busy") && tentativas < 300)
            {
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                Thread.Sleep(50);
                tentativas++;
            }
        }

        // Lê uma propriedade OU chama um método sem argumentos — alguns objetos
        // do SAP GUI Scripting expõem membros como método (DISPATCH_METHOD) em vez
        // de propriedade (DISPATCH_PROPERTYGET); combinar as duas flags deixa o
        // IDispatch decidir, igual o VBA faz de forma transparente.
        private static object Get(object alvo, string nome)
        {
            return alvo.GetType().InvokeMember(
                nome,
                BindingFlags.GetProperty | BindingFlags.InvokeMethod,
                null,
                alvo,
                new object[0]);
        }

        private static void Set(object alvo, string nome, object valor)
        {
            alvo.GetType().InvokeMember(
                nome,
                BindingFlags.SetProperty,
                null,
                alvo,
                new[] { valor });
        }

        private static object Invoke(object alvo, string nome, params object[] args)
        {
            return alvo.GetType().InvokeMember(
                nome,
                BindingFlags.InvokeMethod,
                null,
                alvo,
                args ?? new object[0]);
        }
    }
}
