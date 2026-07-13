using System;
using System.Collections.Generic;
using System.IO;
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
                // "Children" é uma propriedade que devolve uma GuiComponentCollection,
                // não um método que aceita índice — o "Children(0)" do VBA só funciona
                // por causa do açúcar sintático de "membro padrão" de coleção do VBA.
                // Em .NET isso precisa ser explícito via ElementAt(index).
                object app = Get(sapGuiAuto, "GetScriptingEngine");
                object connection = Invoke(Get(app, "Children"), "ElementAt", 0);
                session = Invoke(Get(connection, "Children"), "ElementAt", 0);
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

        // Type.InvokeMember (via TargetInvocationException) e chamadas encadeadas
        // costumam embrulhar o erro real dentro de InnerException — sem isso a
        // mensagem mostrada é só "Exception has been thrown by the target of an
        // invocation.", que não ajuda em nada a diagnosticar. Aqui descemos até
        // achar a causa raiz.
        private static string DescreverErro(Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;

            COMException comEx = ex as COMException;

            return comEx != null
                ? $"{ex.Message} (HRESULT 0x{comEx.HResult:X8})"
                : $"{ex.GetType().Name}: {ex.Message}";
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

        // Baixa o original SWD de cada documento via CV04N (SAP GUI Scripting),
        // portado de uma gravação real do usuário: busca os documentos da ECM
        // (tipo SWD) na CV04N, abre o detalhe de cada um, localiza o original
        // SWD na lista de originais e usa a exportação (CF_EXP_COPY) pra copiar
        // direto pro caminho de destino -- pulando os diálogos de navegação de
        // pasta que apareceram na gravação manual (F4 em cascata): aqui a gente
        // já escreve o caminho final direto no campo de destino.
        //
        // Devolve, por número de documento: o caminho final se deu certo, ou
        // uma mensagem de erro/motivo se não deu.
        public static Dictionary<string, string> BaixarOriginaisSwd(
            object session, string ecm, IEnumerable<string> documentos, string pastaDestino)
        {
            Dictionary<string, string> resultado = new Dictionary<string, string>();

            object grid = AbrirCv04nEBuscar(session, ecm);

            foreach (string documentNumber in documentos)
            {
                try
                {
                    int linha = EncontrarLinhaPorDocumento(grid, documentNumber);

                    if (linha < 0)
                    {
                        resultado[documentNumber] = "Documento não encontrado na grid de resultados da CV04N.";
                        continue;
                    }

                    Invoke(grid, "setCurrentCell", linha, "DOKNR");
                    Invoke(grid, "doubleClickCurrentCell");
                    EsperarSap(session);

                    string caminhoFinal = ExportarOriginalSwd(session, pastaDestino);

                    resultado[documentNumber] = caminhoFinal
                        ?? "Não achei um original SWD na lista de originais desse documento.";

                    // Volta pra lista de resultados (F3), igual ao fluxo gravado
                    // manualmente (BaixarOriginais.vb), pra processar o próximo.
                    Invoke(FindById(session, "wnd[0]"), "SendVKey", 3);
                    EsperarSap(session);
                }
                catch (Exception ex)
                {
                    resultado[documentNumber] = "Erro: " + DescreverErro(ex);

                    // Tenta voltar pra lista mesmo com erro, pra não travar o
                    // processamento dos documentos seguintes.
                    try
                    {
                        Invoke(FindById(session, "wnd[0]"), "SendVKey", 3);
                        EsperarSap(session);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return resultado;
        }

        // Campos confirmados numa gravação real do SAP GUI Scripting do
        // usuário (busca por tipo SWD + ECM na CV04N).
        private static object AbrirCv04nEBuscar(object session, string ecm)
        {
            Set(FindById(session, "wnd[0]/tbar[0]/okcd"), "Text", "cv04n");
            Invoke(FindById(session, "wnd[0]"), "SendVKey", 0);

            const string prefixoTela =
                "wnd[0]/usr/tabsMAINSTRIP/tabpTAB1/ssubSUBSCRN:SAPLCV100:0401/subSCR_MAIN:SAPLCV100:0402/";

            Set(FindById(session, prefixoTela + "ctxtSTDOKAR-LOW"), "Text", "SWD");
            Set(FindById(session, prefixoTela + "txtRESTRICT"), "Text", "");
            Set(FindById(session, prefixoTela + "ctxtSTAENNR-LOW"), "Text", ecm);

            Invoke(FindById(session, "wnd[0]/tbar[1]/btn[8]"), "Press");
            EsperarSap(session);

            return FindById(session, "wnd[0]/usr/cntlGRID1/shellcont/shell");
        }

        // Varre as linhas da grid de resultados da CV04N procurando o número
        // de documento -- mesmo padrão de varredura já usado em
        // BuscarDocumentosPorEcm pra ZTPLM025, mas na coluna DOKNR desta
        // grid (não confirmado numa gravação, mas é o nome técnico padrão do
        // campo "Número do documento" em qualquer tela DMS do SAP).
        private static int EncontrarLinhaPorDocumento(object grid, string documentNumber)
        {
            int totalLinhas = (int)Get(grid, "RowCount");

            for (int i = 0; i < totalLinhas; i++)
            {
                string doc = ((string)Invoke(grid, "GetCellValue", i, "DOKNR")).Trim();

                if (string.Equals(doc, documentNumber, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        // Na tela de detalhe do documento (CV04N), tenta cada nó da lista de
        // originais até achar o que é o SWD -- identificado pelo nome que o
        // SAP propõe no campo de destino da exportação terminar em .SLDDRW.
        // A ordem dos nós não é garantida (já vimos SWD e PDF em ordens
        // diferentes dependendo do documento, no diagnóstico da busca via
        // Web Service), por isso testa em vez de assumir uma posição fixa.
        // Devolve o caminho final se achou e copiou, ou null se não achou
        // nenhum original SWD entre os nós testados.
        private static string ExportarOriginalSwd(object session, string pastaDestino)
        {
            const string prefixoOriginais =
                "wnd[0]/usr/tabsTAB_MAIN/tabpTSMAIN/ssubSCR_MAIN:SAPLCV110:0102/cntlCTL_FILES1/shellcont/shell/shellcont[1]/shell";

            object arvoreOriginais = FindById(session, prefixoOriginais);

            // Nenhum documento visto até agora (via diagnóstico do Web
            // Service) teve mais de 2 originais (SWD + PDF), mas testamos
            // mais alguns nós por segurança.
            for (int i = 1; i <= 5; i++)
            {
                string chaveNode = i.ToString().PadLeft(11);

                try
                {
                    Invoke(arvoreOriginais, "selectNode", chaveNode);
                }
                catch (Exception)
                {
                    // Não existe mais nó nessa posição -- para de tentar.
                    break;
                }

                Invoke(arvoreOriginais, "nodeContextMenu", chaveNode);
                Invoke(arvoreOriginais, "selectContextMenuItem", "CF_EXP_COPY");
                EsperarSap(session);

                object campoDestino = FindById(session, "wnd[1]/usr/ctxtDRAW-FILEP");
                string nomeProposto = ((string)Get(campoDestino, "Text") ?? "").Trim();

                if (nomeProposto.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase))
                {
                    string caminhoFinal = Path.Combine(pastaDestino, Path.GetFileName(nomeProposto));

                    Set(campoDestino, "Text", caminhoFinal);
                    Invoke(FindById(session, "wnd[1]/tbar[0]/btn[0]"), "Press");
                    EsperarSap(session);

                    return caminhoFinal;
                }

                // Não era o original SWD -- cancela esse diálogo (F12) e
                // tenta o próximo nó.
                Invoke(FindById(session, "wnd[1]"), "SendVKey", 12);
                EsperarSap(session);
            }

            return null;
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
