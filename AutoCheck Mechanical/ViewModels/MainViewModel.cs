using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AutoCheckMechanical.Checkers;
using AutoCheckMechanical.Core;
using AutoCheckMechanical.Models;
using AutoCheckMechanical.Services;
using Microsoft.Win32;
using SolidWorks.Interop.swconst;
using CheckContextModel = AutoCheckMechanical.Core.CheckContext;
using SwApp = SolidWorks.Interop.sldworks.SldWorks;
using SwModelDoc2 = SolidWorks.Interop.sldworks.ModelDoc2;
using SwFrame = SolidWorks.Interop.sldworks.IFrame;

namespace AutoCheckMechanical.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly Func<List<string>, HashSet<string>, HashSet<string>> _abrirChecksConfig;
        private readonly Action _abrirSapConexao;
        private readonly Action _minimizar;
        private readonly Action _maximizarRestaurar;
        private readonly Action _fechar;

        public List<BatchFileResult> BatchResults { get; } = new List<BatchFileResult>();

        public event EventHandler ResultsInvalidated;

        private void InvalidarResultados()
        {
            ResultsInvalidated?.Invoke(this, EventArgs.Empty);
        }

        private string _userName;
        public string UserName
        {
            get { return _userName; }
            set { _userName = value; OnPropertyChanged(); }
        }

        private string _statusText = "Pronto.";
        public string StatusText
        {
            get { return _statusText; }
            set { _statusText = value; OnPropertyChanged(); }
        }

        private string _logText = "";
        public string LogText
        {
            get { return _logText; }
            set { _logText = value; OnPropertyChanged(); }
        }

        private string _detalheTitulo = "LOG";
        public string DetalheTitulo
        {
            get { return _detalheTitulo; }
            set { _detalheTitulo = value; OnPropertyChanged(); }
        }

        private string _ecmText = "";
        public string EcmText
        {
            get { return _ecmText; }
            set { _ecmText = value; OnPropertyChanged(); }
        }

        private string _filtroTexto = "";
        public string FiltroTexto
        {
            get { return _filtroTexto; }
            set
            {
                _filtroTexto = value ?? "";
                OnPropertyChanged();
                InvalidarResultados();
            }
        }

        private string _arquivoAtual = "Nenhum arquivo carregado";
        public string ArquivoAtual
        {
            get { return _arquivoAtual; }
            set { _arquivoAtual = value; OnPropertyChanged(); }
        }

        private string _solidWorksStatusText = "SolidWorks Desconectado";
        public string SolidWorksStatusText
        {
            get { return _solidWorksStatusText; }
            set { _solidWorksStatusText = value; OnPropertyChanged(); }
        }

        private Brush _solidWorksLedBrush = Brushes.Red;
        public Brush SolidWorksLedBrush
        {
            get { return _solidWorksLedBrush; }
            set { _solidWorksLedBrush = value; OnPropertyChanged(); }
        }

        private bool _temaEscuro = true;
        public bool TemaEscuro
        {
            get { return _temaEscuro; }
            set { _temaEscuro = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private HashSet<string> _checkersDesativados;
        public HashSet<string> CheckersDesativados
        {
            get { return _checkersDesativados; }
            set { _checkersDesativados = value; OnPropertyChanged(); }
        }

        // Opção de versão pra busca de documentos por ECM (Web Service),
        // igual ao SAP GUI (CV04N): mutuamente exclusivas, feito na mão
        // porque RadioButtons de grupos diferentes não têm um binding
        // nativo de "enum selecionado" sem conversor.
        private bool _buscarUltimaVersaoLiberada = true;
        public bool BuscarUltimaVersaoLiberada
        {
            get { return _buscarUltimaVersaoLiberada; }
            set
            {
                _buscarUltimaVersaoLiberada = value;
                OnPropertyChanged();

                if (value)
                    BuscarUltimaVersao = false;
            }
        }

        private bool _buscarUltimaVersao;
        public bool BuscarUltimaVersao
        {
            get { return _buscarUltimaVersao; }
            set
            {
                _buscarUltimaVersao = value;
                OnPropertyChanged();

                if (value)
                    BuscarUltimaVersaoLiberada = false;
            }
        }

        private bool _buscandoDocumentos;
        public bool BuscandoDocumentos
        {
            get { return _buscandoDocumentos; }
            set { _buscandoDocumentos = value; OnPropertyChanged(); }
        }

        public MainViewModel(
            Func<List<string>, HashSet<string>, HashSet<string>> abrirChecksConfig,
            Action abrirSapConexao,
            Action minimizar,
            Action maximizarRestaurar,
            Action fechar)
        {
            _abrirChecksConfig = abrirChecksConfig;
            _abrirSapConexao = abrirSapConexao;
            _minimizar = minimizar;
            _maximizarRestaurar = maximizarRestaurar;
            _fechar = fechar;

            UserName = Environment.UserName;

            BatchResults.AddRange(HistoryStore.Load());
            CheckersDesativados = CheckerSettingsStore.LoadDesativados();

            TemaEscuro = ThemeStore.LoadTemaEscuro();

            // Monitora em segundo plano se o SolidWorks continua aberto,
            // pra refletir "Desconectado" na tela caso o usuário feche ele
            // com o app ainda aberto.
            DispatcherTimer timerConexao = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            timerConexao.Tick += (s, e) => VerificarConexaoSilenciosamente();
            timerConexao.Start();
        }

        public ICommand ToggleThemeCommand => new DelegateCommand(_ =>
        {
            TemaEscuro = !TemaEscuro;
            ThemeStore.Save(TemaEscuro);

            StatusText = TemaEscuro ? "Tema escuro aplicado." : "Tema claro aplicado.";
        });

        public ICommand OpenChecksConfigCommand => new DelegateCommand(_ =>
        {
            List<string> todosOsChecks = CheckerManager.GetAllCheckerNames();

            HashSet<string> resultado = _abrirChecksConfig(todosOsChecks, CheckersDesativados);

            if (resultado != null)
            {
                CheckersDesativados = resultado;
                CheckerSettingsStore.Save(CheckersDesativados);
                InvalidarResultados();

                int ativos = todosOsChecks.Count - CheckersDesativados.Count;
                StatusText = $"{ativos} de {todosOsChecks.Count} check(s) ativado(s).";
            }
        });

        // Integração SAP via RFC/BAPI (SAP .NET Connector / NCo), no mesmo
        // padrão do WBC (SapConnectionInterface.cs).
        public ICommand AbrirSapConexaoCommand => new DelegateCommand(_ => _abrirSapConexao());

        // Busca de documentos do DMS pela ECM (mesmo campo EcmText usado
        // pelo BuscarSapCommand acima), via Web Service ITF_O_S_DOCUMENT_OUTPUT
        // (SOA), mecanismo separado da macro Excel/ZTPLM025.
        public ICommand BuscarDocumentosPorEcmCommand => new DelegateCommand(_ => BuscarDocumentosPorEcm(), () => !BuscandoDocumentos);

        public ICommand GoToHistoryCommand => new DelegateCommand(_ =>
        {
            FiltroTexto = "";
            StatusText = $"Histórico: {BatchResults.Count} arquivo(s) verificado(s) ao todo.";
        });

        public ICommand ClearHistoryCommand => new DelegateCommand(_ =>
        {
            if (BatchResults.Count == 0)
                return;

            MessageBoxResult resposta = MessageBox.Show(
                "Isso vai apagar todo o histórico de verificações salvo neste computador. Deseja continuar?",
                "Limpar Histórico",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (resposta != MessageBoxResult.Yes)
                return;

            BatchResults.Clear();
            HistoryStore.Clear();
            ThumbnailStore.ClearAll();
            InvalidarResultados();

            StatusText = "Histórico apagado.";
        });

        public ICommand ReconnectCommand => new DelegateCommand(_ =>
        {
            LogText = "";
            AddLog("Verificando conexão...");

            GarantirConexao();
        });

        public ICommand RunCheckDrawingCommand => new DelegateCommand(_ => RunCheckDrawing(), () => !IsBusy);

        public ICommand VerificarArquivosCommand => new DelegateCommand(_ =>
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Desenhos SolidWorks (*.slddrw)|*.slddrw",
                Multiselect = true,
                Title = "Selecionar desenhos para verificar"
            };

            if (dialog.ShowDialog() != true)
                return;

            SolidWorksSession session = GarantirConexao();

            if (!session.IsConnected)
                return;

            VerificarArquivos(session, dialog.FileNames);
        }, () => !IsBusy);

        public ICommand TrocarPlanilhaCommand => new DelegateCommand(_ =>
        {
            OpenFileDialog dialogPlanilha = new OpenFileDialog
            {
                Filter = "Planilhas Excel (*.xlsm;*.xls;*.xlsb;*.xlsx)|*.xlsm;*.xls;*.xlsb;*.xlsx",
                Title = "Selecionar a planilha com a macro de busca no SAP"
            };

            if (dialogPlanilha.ShowDialog() != true)
                return;

            ExcelMacroSettingsStore.Save(dialogPlanilha.FileName);

            StatusText = "Planilha do SAP atualizada: " + dialogPlanilha.FileName;
        });

        public ICommand BuscarSapCommand => new DelegateCommand(_ => BuscarSap(), () => !IsBusy);

        public ICommand ExportLogCommand => new DelegateCommand(_ =>
        {
            if (string.IsNullOrWhiteSpace(LogText))
            {
                MessageBox.Show("Não há log para exportar.", "Exportar Log",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Arquivo de texto (*.txt)|*.txt|CSV para Excel (*.csv)|*.csv",
                FileName = "AutoCheckMechanical_Log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                bool exportarComoCsv = dialog.FilterIndex == 2;

                string conteudo = exportarComoCsv
                    ? ConverterLogParaCsv(LogText)
                    : LogText;

                File.WriteAllText(dialog.FileName, conteudo, Encoding.UTF8);

                StatusText = "Log exportado: " + dialog.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível salvar o arquivo:\n" + ex.Message,
                    "Exportar Log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        public ICommand ExportarRelatorioCommand => new DelegateCommand(_ =>
        {
            List<BatchFileResult> resultados = GetResultadosFiltrados();

            if (resultados.Count == 0)
            {
                MessageBox.Show("Não há resultados na tabela para exportar.", "Exportar Relatório",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Planilha do Excel (*.xlsx)|*.xlsx",
                FileName = "AutoCheckMechanical_Relatorio_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                ExcelReportService.GerarRelatorio(resultados, GetCheckerNames(), CamposTituloAtuais(), dialog.FileName);

                StatusText = "Relatório exportado: " + dialog.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível gerar o relatório:\n" + ex.Message,
                    "Exportar Relatório", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        public ICommand MinimizeCommand => new DelegateCommand(_ => _minimizar());

        public ICommand MaximizeRestoreCommand => new DelegateCommand(_ => _maximizarRestaurar());

        public ICommand CloseCommand => new DelegateCommand(_ => _fechar());

        public void AddLog(string texto)
        {
            LogText += DateTime.Now.ToString("HH:mm:ss") + "  " + texto + Environment.NewLine;
        }

        public SolidWorksSession RefreshConnectionStatus()
        {
            SolidWorksSession session = SolidWorksSession.Connect();

            if (!session.IsConnected)
            {
                AddLog("SolidWorks não encontrado.");
                StatusText = "SolidWorks desconectado.";

                SolidWorksLedBrush = Brushes.Red;
                SolidWorksStatusText = "Desconectado";

                return session;
            }

            SolidWorksLedBrush = Brushes.LimeGreen;
            SolidWorksStatusText = "Conectado";

            AddLog("SolidWorks conectado.");

            if (session.ActiveDocument == null)
            {
                AddLog("Nenhum documento aberto.");
                StatusText = "Nenhum documento.";
                return session;
            }

            ArquivoAtual = session.ActiveDocument.GetTitle();

            AddLog("Arquivo:");
            AddLog(session.ActiveDocument.GetTitle());

            return session;
        }

        // Verificação silenciosa (sem log/prompt) usada pelo monitoramento
        // periódico em segundo plano, só pra manter o LED/status atualizados
        // caso o SolidWorks seja fechado enquanto o app está aberto.
        private bool? _ultimoEstadoConectado;

        private void VerificarConexaoSilenciosamente()
        {
            SolidWorksSession session = SolidWorksSession.Connect();

            if (_ultimoEstadoConectado == session.IsConnected)
                return;

            _ultimoEstadoConectado = session.IsConnected;

            if (session.IsConnected)
            {
                SolidWorksLedBrush = Brushes.LimeGreen;
                SolidWorksStatusText = "Conectado";
                AddLog("SolidWorks conectado.");
            }
            else
            {
                SolidWorksLedBrush = Brushes.Red;
                SolidWorksStatusText = "Desconectado";
                AddLog("SolidWorks foi fechado.");
            }
        }

        // Ponto de entrada usado por toda ação que precisa do SolidWorks
        // (e também na abertura do app): se não estiver conectado, pergunta
        // se o usuário quer abrir o SolidWorks agora.
        public SolidWorksSession GarantirConexao()
        {
            SolidWorksSession session = RefreshConnectionStatus();

            _ultimoEstadoConectado = session.IsConnected;

            if (session.IsConnected)
                return session;

            MessageBoxResult resposta = MessageBox.Show(
                "O SolidWorks não está aberto. Deseja abri-lo agora?",
                "SolidWorks desconectado",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resposta != MessageBoxResult.Yes)
                return session;

            return AbrirSolidWorksEAguardarConexao();
        }

        private SolidWorksSession AbrirSolidWorksEAguardarConexao()
        {
            string caminhoSolidWorks = LocalizarSolidWorks();

            if (caminhoSolidWorks == null && !TentarAbrirSolidWorksViaShell())
            {
                caminhoSolidWorks = EscolherSolidWorksManualmente();

                if (caminhoSolidWorks == null)
                    return SolidWorksSession.Connect();
            }

            if (caminhoSolidWorks != null)
            {
                try
                {
                    Process.Start(caminhoSolidWorks);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Não foi possível abrir o SolidWorks:\n" + ex.Message,
                        "Abrir SolidWorks", MessageBoxButton.OK, MessageBoxImage.Error);
                    return SolidWorksSession.Connect();
                }
            }

            StatusText = "Abrindo o SolidWorks, aguarde...";
            AddLog("Abrindo o SolidWorks...");
            Mouse.OverrideCursor = Cursors.Wait;

            SolidWorksSession session = SolidWorksSession.Connect();
            DateTime limite = DateTime.Now.AddSeconds(120);

            while (!session.IsConnected && DateTime.Now < limite)
            {
                Thread.Sleep(1000);

                // Mantém a UI respondendo durante a espera (o app roda tudo
                // de forma síncrona na thread da UI).
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

                session = SolidWorksSession.Connect();
            }

            Mouse.OverrideCursor = null;

            if (session.IsConnected)
            {
                RefreshConnectionStatus();
                _ultimoEstadoConectado = true;
            }
            else
            {
                StatusText = "Não foi possível conectar ao SolidWorks (tempo esgotado).";
                AddLog("Tempo esgotado esperando o SolidWorks abrir.");
            }

            return session;
        }

        // Tenta localizar o SLDWORKS.exe: primeiro o caminho salvo
        // manualmente pelo usuário (se houver), depois os caminhos de
        // instalação mais comuns (mesmo padrão usado pelo HintPath do
        // projeto: "SOLIDWORKS {ano}\SOLIDWORKS\").
        private string LocalizarSolidWorks()
        {
            string caminhoSalvo = SolidWorksSettingsStore.LoadCaminho();

            if (!string.IsNullOrEmpty(caminhoSalvo) && File.Exists(caminhoSalvo))
                return caminhoSalvo;

            return GerarCaminhosConhecidosSolidWorks().FirstOrDefault(File.Exists);
        }

        private static IEnumerable<string> GerarCaminhosConhecidosSolidWorks()
        {
            for (int ano = 2026; ano >= 2018; ano--)
            {
                yield return $@"C:\Program Files\SOLIDWORKS {ano}\SOLIDWORKS\SLDWORKS.exe";
                yield return $@"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS {ano}\SOLIDWORKS\SLDWORKS.exe";
                yield return $@"C:\Program Files (x86)\SOLIDWORKS {ano}\SOLIDWORKS\SLDWORKS.exe";
            }
        }

        // Deixa o Windows resolver "SLDWORKS.exe" (App Paths / PATH), como
        // aconteceria se o usuário desse duplo clique num atalho dele.
        private bool TentarAbrirSolidWorksViaShell()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "SLDWORKS.exe",
                    UseShellExecute = true
                });

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string EscolherSolidWorksManualmente()
        {
            MessageBox.Show(
                "Não foi possível localizar o SolidWorks automaticamente.\nSelecione o executável (SLDWORKS.exe) manualmente.",
                "Abrir SolidWorks", MessageBoxButton.OK, MessageBoxImage.Information);

            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "SolidWorks (SLDWORKS.exe)|SLDWORKS.exe|Executável (*.exe)|*.exe",
                Title = "Selecionar o SLDWORKS.exe"
            };

            if (dialog.ShowDialog() != true)
                return null;

            SolidWorksSettingsStore.Save(dialog.FileName);

            return dialog.FileName;
        }

        private void RunCheckDrawing()
        {
            // Se há documentos vindos da busca por ECM ainda não checados na
            // lista, o botão CHECK DRAWING processa esse lote em vez do
            // documento ativo no SolidWorks (comportamento original, usado
            // quando a lista não tem documentos pendentes).
            List<BatchFileResult> documentosPendentes = BatchResults
                .Where(x => !string.IsNullOrEmpty(x.DocumentoNumero) && x.Results.Count == 0 && !x.OpenFailed)
                .ToList();

            if (documentosPendentes.Count > 0)
            {
                RunCheckDocumentosPendentes(documentosPendentes);
                return;
            }

            DetalheTitulo = "LOG";
            LogText = "";
            AddLog("Iniciando AutoCheck...");

            SolidWorksSession session = GarantirConexao();

            if (!session.IsConnected || session.ActiveDocument == null)
                return;

            CheckContextModel context = new CheckContextModel(session.Application, session.ActiveDocument);

            CheckEngine engine = new CheckEngine();

            CheckerManager.Register(engine, CheckersDesativados);

            IsBusy = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                List<CheckResult> results = engine.Execute(context);

                string filePath = session.ActiveDocument.GetPathName();

                UpsertBatchResult(new BatchFileResult
                {
                    FileName = session.ActiveDocument.GetTitle(),
                    FilePath = filePath,
                    SheetCount = context.SheetCount,
                    ThumbnailPath = ThumbnailStore.Generate(session.ActiveDocument, filePath),
                    Results = results
                });

                InvalidarResultados();
                HistoryStore.Save(BatchResults);

                AddLog("--------------------------------");

                foreach (CheckResult result in results)
                {
                    AddLog(result.Checker);

                    if (result.Skipped)
                    {
                        AddLog("N/A (dispensado)");

                        if (!string.IsNullOrEmpty(result.Message))
                            AddLog(result.Message);
                    }
                    else if (result.Success)
                    {
                        AddLog("OK");

                        if (!string.IsNullOrEmpty(result.Message))
                            AddLog(result.Message);
                    }
                    else
                    {
                        AddLog("ERRO");

                        foreach (string erro in result.Errors)
                            AddLog(erro);
                    }

                    foreach (string log in result.Logs)
                        AddLog(log);

                    AddLog("--------------------------------");
                }

                StatusText = "Check finalizado.";
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
            }
        }

        // Roda o check em lote nos documentos vindos da busca por ECM
        // (BuscarDocumentosPorEcmCommand) que ainda não foram checados,
        // reaproveitando o caminho do "Original" do DMS (DocumentoCaminhoOriginal)
        // como arquivo pra abrir no SolidWorks. Documentos sem original
        // vinculado no DMS são marcados como falha, sem tentar abrir nada.
        private void RunCheckDocumentosPendentes(List<BatchFileResult> documentosPendentes)
        {
            List<BatchFileResult> semArquivo = documentosPendentes
                .Where(x => string.IsNullOrEmpty(x.DocumentoCaminhoOriginal))
                .ToList();

            List<BatchFileResult> comArquivo = documentosPendentes
                .Except(semArquivo)
                .ToList();

            SolidWorksSession session = null;

            if (comArquivo.Count > 0)
            {
                session = GarantirConexao();

                if (!session.IsConnected)
                    return;
            }

            DetalheTitulo = "LOG";
            LogText = "";
            IsBusy = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                foreach (BatchFileResult pendente in semArquivo)
                {
                    pendente.OpenFailed = true;
                    pendente.OpenError = "Nenhum arquivo original vinculado a este documento no DMS.";

                    UpsertBatchResult(pendente);
                    AddLog($"{pendente.DocumentoNumero} — sem arquivo original vinculado no DMS.");
                }

                if (comArquivo.Count > 0)
                {
                    CheckEngine engine = new CheckEngine();
                    CheckerManager.Register(engine, CheckersDesativados);

                    // Loop em vez de ToDictionary: dois documentos diferentes
                    // podem, em tese, apontar pro mesmo arquivo original --
                    // ToDictionary lançaria exceção nesse caso.
                    Dictionary<string, BatchFileResult> pendentesPorCaminho = new Dictionary<string, BatchFileResult>();

                    foreach (BatchFileResult pendente in comArquivo)
                        pendentesPorCaminho[pendente.DocumentoCaminhoOriginal] = pendente;

                    AddLog($"Verificando {comArquivo.Count} documento(s) da lista... A janela do SolidWorks vai abrir e fechar cada arquivo automaticamente, isso é esperado.");

                    int concluidos = 0;

                    BatchCheckRunner.Run(session.Application, engine, comArquivo.Select(x => x.DocumentoCaminhoOriginal), item =>
                    {
                        concluidos++;

                        if (pendentesPorCaminho.TryGetValue(item.FilePath, out BatchFileResult pendente))
                        {
                            item.DocumentoNumero = pendente.DocumentoNumero;
                            item.DocumentoTipo = pendente.DocumentoTipo;
                            item.DocumentoParte = pendente.DocumentoParte;
                            item.DocumentoVersao = pendente.DocumentoVersao;
                            item.DocumentoDescricao = pendente.DocumentoDescricao;
                            item.DocumentoCaminhoOriginal = pendente.DocumentoCaminhoOriginal;
                        }

                        UpsertBatchResult(item);
                        InvalidarResultados();
                        HistoryStore.Save(BatchResults);

                        string statusItem = item.OpenFailed ? "FALHA AO ABRIR" : "OK";
                        AddLog($"[{concluidos}/{comArquivo.Count}] {item.FileName} — {statusItem}");
                        StatusText = $"Verificando... {concluidos}/{comArquivo.Count} concluído(s) ({item.FileName}).";

                        // Força o WPF a processar a fila de render agora, já que tudo
                        // roda de forma síncrona nesta thread — sem isso a tabela só
                        // apareceria atualizada quando o lote inteiro terminasse.
                        Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                    });
                }

                InvalidarResultados();
                HistoryStore.Save(BatchResults);

                StatusText = $"{documentosPendentes.Count} documento(s) verificado(s).";
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
            }
        }

        // Remove uma linha da tabela (usado pra descartar documentos que o
        // usuário não quer checar, vindos da busca por ECM, ou qualquer
        // resultado antigo do histórico).
        public void RemoverBatchResult(BatchFileResult item)
        {
            BatchResults.Remove(item);
            HistoryStore.Save(BatchResults);
        }

        // Reaproveitado pelo fluxo manual (VERIFICAR ARQUIVOS...) e pelo fluxo
        // de download automático via SAP.
        private void VerificarArquivos(SolidWorksSession session, IEnumerable<string> filePaths)
        {
            List<string> arquivos = filePaths.ToList();

            if (arquivos.Count == 0)
                return;

            CheckEngine engine = new CheckEngine();
            CheckerManager.Register(engine, CheckersDesativados);

            IsBusy = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                AddLog($"Verificando {arquivos.Count} arquivo(s)... A janela do SolidWorks vai abrir e fechar cada arquivo automaticamente, isso é esperado.");

                int concluidos = 0;

                List<BatchFileResult> resultados = BatchCheckRunner.Run(session.Application, engine, arquivos, item =>
                {
                    concluidos++;

                    UpsertBatchResult(item);
                    InvalidarResultados();
                    HistoryStore.Save(BatchResults);

                    string statusItem = item.OpenFailed ? "FALHA AO ABRIR" : "OK";
                    AddLog($"[{concluidos}/{arquivos.Count}] {item.FileName} — {statusItem}");
                    StatusText = $"Verificando... {concluidos}/{arquivos.Count} concluído(s) ({item.FileName}).";

                    // Força o WPF a processar a fila de render agora, já que tudo
                    // roda de forma síncrona nesta thread — sem isso a tabela só
                    // apareceria atualizada quando o lote inteiro terminasse.
                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                });

                int falhas = resultados.Count(r => r.OpenFailed);

                StatusText = falhas == 0
                    ? $"{resultados.Count} arquivo(s) verificado(s)."
                    : $"{resultados.Count} arquivo(s) verificado(s), {falhas} com falha ao abrir.";
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
            }
        }

        // retornarUltimaVersao (BuscarUltimaVersao) mapeia pra
        // ReturnCurrentVersion do request SOAP -- ver a ressalva em
        // DocumentSearchService.BuscarPorEcm sobre essa leitura ainda não
        // estar confirmada contra o comportamento real do SAP GUI (CV04N).
        private void BuscarDocumentosPorEcm()
        {
            string ecm = EcmText?.Trim();

            if (string.IsNullOrEmpty(ecm))
            {
                MessageBox.Show("Informe a ECM antes de buscar os documentos.", "Buscar documentos (SAP)",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BuscandoDocumentos = true;
            Mouse.OverrideCursor = Cursors.Wait;
            StatusText = $"Buscando documentos da ECM {ecm} no SAP...";

            try
            {
                List<DocumentoEncontrado> resultados = DocumentSearchService.BuscarPorEcm(ecm, Environment.UserName, BuscarUltimaVersao);

                foreach (DocumentoEncontrado documento in resultados)
                {
                    string caminhoOriginal = documento.CaminhosOriginais.FirstOrDefault();

                    UpsertBatchResult(new BatchFileResult
                    {
                        // Usa o "Original" do DMS como FilePath quando existe
                        // (é o que o RunCheckDrawing vai abrir no SolidWorks
                        // depois); sem original, vira um identificador
                        // sintético só pra distinguir a linha, não um
                        // caminho de arquivo de verdade.
                        FilePath = !string.IsNullOrEmpty(caminhoOriginal)
                            ? caminhoOriginal
                            : $"SAP:{documento.DocumentNumber}:{documento.Version}",
                        FileName = documento.DocumentNumber,
                        DocumentoNumero = documento.DocumentNumber,
                        DocumentoTipo = documento.Type,
                        DocumentoParte = documento.Part,
                        DocumentoVersao = documento.Version,
                        DocumentoDescricao = documento.Descricao,
                        DocumentoCaminhoOriginal = caminhoOriginal,
                    });
                }

                InvalidarResultados();
                HistoryStore.Save(BatchResults);

                StatusText = resultados.Count == 0
                    ? $"Nenhum documento encontrado para a ECM {ecm}."
                    : $"{resultados.Count} documento(s) encontrado(s) para a ECM {ecm}, adicionados à tabela.";
            }
            catch (Exception ex)
            {
                string mensagem = DocumentSearchService.DescreverErroCompleto(ex);
                StatusText = "Falha ao buscar documentos: " + mensagem;

                MessageBox.Show("Não foi possível buscar os documentos:\n" + mensagem,
                    "Buscar documentos (SAP)", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BuscandoDocumentos = false;
                Mouse.OverrideCursor = null;
            }
        }

        private void BuscarSap()
        {
            string ecm = EcmText?.Trim();

            if (string.IsNullOrEmpty(ecm))
            {
                MessageBox.Show("Informe a ECM antes de buscar no SAP.", "Buscar no SAP",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            Mouse.OverrideCursor = Cursors.Wait;
            LogText = "";
            DetalheTitulo = "LOG";

            try
            {
                string caminhoPlanilha = ExcelMacroSettingsStore.LoadCaminho();

                if (string.IsNullOrEmpty(caminhoPlanilha) || !File.Exists(caminhoPlanilha))
                {
                    OpenFileDialog dialogPlanilha = new OpenFileDialog
                    {
                        Filter = "Planilhas Excel (*.xlsm;*.xls;*.xlsb;*.xlsx)|*.xlsm;*.xls;*.xlsb;*.xlsx",
                        Title = "Selecionar a planilha com a macro de busca no SAP"
                    };

                    if (dialogPlanilha.ShowDialog() != true)
                        return;

                    caminhoPlanilha = dialogPlanilha.FileName;
                    ExcelMacroSettingsStore.Save(caminhoPlanilha);
                }

                AddLog($"Rodando a macro da planilha para buscar documentos da ECM {ecm}...");
                AddLog("Planilha: " + caminhoPlanilha);

                List<string> documentos;

                try
                {
                    documentos = ExcelSapService.BuscarEBaixarViaMacro(caminhoPlanilha, ecm);
                }
                catch (Exception ex)
                {
                    AddLog("Falha ao rodar a macro da planilha: " + ex.Message);
                    MessageBox.Show("Não foi possível rodar a macro da planilha:\n" + ex.Message,
                        "Buscar no SAP", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AddLog($"{documentos.Count} documento(s) SWD encontrado(s) para a ECM {ecm}:");

                foreach (string doc in documentos)
                    AddLog(" - " + doc);

                if (documentos.Count == 0)
                {
                    StatusText = $"Nenhum documento SWD encontrado para a ECM {ecm}.";
                    return;
                }

                string pastaDestino = Path.Combine("C:\\SAP_SW", ecm);

                string[] arquivosBaixados = Directory.Exists(pastaDestino)
                    ? Directory.GetFiles(pastaDestino, "*.slddrw", SearchOption.TopDirectoryOnly)
                    : new string[0];

                AddLog($"{arquivosBaixados.Length} arquivo(s) .slddrw encontrado(s) em {pastaDestino}.");

                if (arquivosBaixados.Length == 0)
                {
                    MessageBox.Show(
                        $"ECM: {ecm}\nDocumentos encontrados no SAP: {documentos.Count}\n\n" +
                        "A macro rodou, mas nenhum arquivo .slddrw apareceu em:\n" + pastaDestino,
                        "Buscar no SAP", MessageBoxButton.OK, MessageBoxImage.Information);

                    StatusText = $"{documentos.Count} documento(s) encontrado(s) no SAP, nenhum arquivo local ainda.";
                    return;
                }

                MessageBox.Show(
                    $"ECM: {ecm}\nDocumentos encontrados no SAP: {documentos.Count}\nArquivos baixados: {arquivosBaixados.Length}\n\n" +
                    "Os arquivos serão abertos no SolidWorks e verificados agora.",
                    "Buscar no SAP", MessageBoxButton.OK, MessageBoxImage.Information);

                SolidWorksSession session = GarantirConexao();

                if (!session.IsConnected)
                    return;

                VerificarArquivos(session, arquivosBaixados);

                string caminhoRelatorio = null;

                try
                {
                    caminhoRelatorio = GerarRelatorioAutomatico(arquivosBaixados, ecm, pastaDestino);
                }
                catch (Exception ex)
                {
                    AddLog("Falha ao gerar o relatório automático: " + ex.Message);
                }

                if (caminhoRelatorio != null)
                {
                    AddLog("Relatório gerado: " + caminhoRelatorio);
                    StatusText = $"Verificação concluída. Relatório: {caminhoRelatorio}";

                    MessageBox.Show(
                        $"Verificação concluída para a ECM {ecm}.\n\nRelatório detalhado salvo em:\n{caminhoRelatorio}",
                        "Buscar no SAP", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
            }
        }

        private void UpsertBatchResult(BatchFileResult item)
        {
            int existingIndex = BatchResults.FindIndex(x => x.FilePath == item.FilePath);

            if (existingIndex >= 0)
                BatchResults[existingIndex] = item;
            else
                BatchResults.Add(item);
        }

        public List<string> GetCheckerNames()
        {
            return CheckerManager.GetAllCheckerNames()
                .Where(nome => !CheckersDesativados.Contains(nome))
                .ToList();
        }

        public List<BatchFileResult> GetResultadosFiltrados()
        {
            if (string.IsNullOrWhiteSpace(FiltroTexto))
                return BatchResults;

            return BatchResults
                .Where(x => x.FileName != null &&
                            x.FileName.IndexOf(FiltroTexto, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        public void ShowFileDetails(BatchFileResult item)
        {
            DetalheTitulo = "LOG — " + item.FileName;
            LogText = "";

            if (item.OpenFailed)
            {
                AddLog("Falha ao abrir o arquivo.");
                AddLog(item.OpenError);
                return;
            }

            foreach (CheckResult result in item.Results)
            {
                AddLog(result.Checker);

                if (result.Skipped)
                {
                    AddLog("N/A (dispensado)");

                    if (!string.IsNullOrEmpty(result.Message))
                        AddLog(result.Message);
                }
                else if (result.Success)
                {
                    AddLog("OK");

                    if (!string.IsNullOrEmpty(result.Message))
                        AddLog(result.Message);
                }
                else
                {
                    AddLog("ERRO");

                    foreach (string erro in result.Errors)
                        AddLog(erro);
                }

                foreach (string log in result.Logs)
                    AddLog(log);

                AddLog("--------------------------------");
            }
        }

        // Abre o arquivo no SolidWorks (ou apenas ativa a janela, se ele já
        // estiver aberto), ao clicar no nome do arquivo na tabela.
        public void AbrirNoSolidWorks(BatchFileResult item)
        {
            if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
            {
                MessageBox.Show("Arquivo não encontrado:\n" + item.FilePath, "Abrir no SolidWorks",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SolidWorksSession session = GarantirConexao();

            if (!session.IsConnected)
                return;

            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                SwApp app = session.Application;

                SwModelDoc2 doc = app.GetOpenDocumentByName(item.FilePath) as SwModelDoc2;

                if (doc == null)
                {
                    int errors = 0;
                    int warnings = 0;

                    doc = app.OpenDoc6(
                        item.FilePath,
                        (int)swDocumentTypes_e.swDocDRAWING,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                        "",
                        ref errors,
                        ref warnings) as SwModelDoc2;

                    if (doc == null)
                    {
                        MessageBox.Show($"Não foi possível abrir o arquivo no SolidWorks (código de erro {errors}).",
                            "Abrir no SolidWorks", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                int activateErrors = 0;
                app.ActivateDoc2(doc.GetTitle(), false, ref activateErrors);

                app.Visible = true;

                try
                {
                    SwFrame frame = app.Frame() as SwFrame;
                    if (frame != null)
                        SetForegroundWindow((IntPtr)frame.GetHWnd());
                }
                catch (COMException)
                {
                }
            }
            catch (COMException ex)
            {
                MessageBox.Show("Não foi possível abrir o arquivo no SolidWorks:\n" + ex.Message,
                    "Abrir no SolidWorks", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // Abre o desenho no visualizador eDrawings (independente do
        // SolidWorks), para o usuário conferir o arquivo visualmente sem
        // precisar abrir o SolidWorks completo.
        public void AbrirNoEDrawings(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show("Arquivo não encontrado:\n" + filePath, "Abrir no eDrawings",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string caminhoEDrawings = LocalizarEDrawings();

            if (caminhoEDrawings == null)
            {
                if (!TentarAbrirViaShell(filePath))
                {
                    caminhoEDrawings = EscolherEDrawingsManualmente();

                    if (caminhoEDrawings == null)
                        return;
                }
                else
                {
                    return;
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = caminhoEDrawings,
                    Arguments = "\"" + filePath + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Não foi possível abrir o eDrawings:\n" + ex.Message,
                    "Abrir no eDrawings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Tenta localizar o eDrawings.exe: primeiro o caminho salvo
        // manualmente pelo usuário (se houver), depois os caminhos de
        // instalação mais comuns (confirmados pelo usuário: "Common Files"
        // e dentro da própria pasta do SOLIDWORKS, um por versão/ano).
        private string LocalizarEDrawings()
        {
            string caminhoSalvo = EDrawingsSettingsStore.LoadCaminho();

            if (!string.IsNullOrEmpty(caminhoSalvo) && File.Exists(caminhoSalvo))
                return caminhoSalvo;

            return GerarCaminhosConhecidosEDrawings().FirstOrDefault(File.Exists);
        }

        private static IEnumerable<string> GerarCaminhosConhecidosEDrawings()
        {
            for (int ano = 2026; ano >= 2018; ano--)
            {
                yield return $@"C:\Program Files\Common Files\eDrawings{ano}\eDrawings.exe";
                yield return $@"C:\Program Files (x86)\Common Files\eDrawings{ano}\eDrawings.exe";
                yield return $@"C:\Program Files\SOLIDWORKS {ano}\eDrawings\eDrawings.exe";
                yield return $@"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS {ano}\SOLIDWORKS eDrawings\eDrawings.exe";
            }

            yield return @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS eDrawings\eDrawings.exe";
            yield return @"C:\Program Files (x86)\SOLIDWORKS Corp\SOLIDWORKS eDrawings\eDrawings.exe";
            yield return @"C:\Program Files\eDrawings\eDrawings.exe";
            yield return @"C:\Program Files (x86)\eDrawings\eDrawings.exe";
        }

        // Deixa o Windows resolver "eDrawings.exe" (App Paths / PATH), como
        // aconteceria se o usuário desse duplo clique num atalho dele.
        private bool TentarAbrirViaShell(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "eDrawings.exe",
                    Arguments = "\"" + filePath + "\"",
                    UseShellExecute = true
                });

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string EscolherEDrawingsManualmente()
        {
            MessageBox.Show(
                "Não foi possível localizar o eDrawings automaticamente.\nSelecione o executável (eDrawings.exe) manualmente.",
                "Abrir no eDrawings", MessageBoxButton.OK, MessageBoxImage.Information);

            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "eDrawings (eDrawings.exe)|eDrawings.exe|Executável (*.exe)|*.exe",
                Title = "Selecionar o eDrawings.exe"
            };

            if (dialog.ShowDialog() != true)
                return null;

            EDrawingsSettingsStore.Save(dialog.FileName);

            return dialog.FileName;
        }

        // Permite ao usuário forçar a execução dos checks de Layer/Flat
        // Pattern/Scale para um arquivo cujo check foi dispensado
        // automaticamente (sem info de chapa), caso ele julgue necessário.
        public void ForcarChecksDeChapa(BatchFileResult item)
        {
            SolidWorksSession session = GarantirConexao();

            if (!session.IsConnected)
                return;

            CheckEngine engine = new CheckEngine();
            CheckerManager.Register(engine, CheckersDesativados);

            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                AddLog($"Forçando execução dos checks em {item.FileName}...");

                List<BatchFileResult> resultados = BatchCheckRunner.Run(
                    session.Application,
                    engine,
                    new[] { item.FilePath },
                    forcarChecksDeChapa: true);

                BatchFileResult novoResultado = resultados.FirstOrDefault();

                if (novoResultado != null)
                {
                    UpsertBatchResult(novoResultado);
                    InvalidarResultados();
                    HistoryStore.Save(BatchResults);

                    string statusItem = novoResultado.OpenFailed ? "FALHA AO ABRIR" : "OK";
                    AddLog($"{novoResultado.FileName} — {statusItem}");
                    StatusText = $"Checks executados em {novoResultado.FileName}.";
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private static string ConverterLogParaCsv(string log)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Horário;Mensagem");

            string[] linhas = log.Replace("\r\n", "\n").Split('\n');

            foreach (string linha in linhas)
            {
                if (linha.Length == 0)
                    continue;

                string horario = "";
                string mensagem = linha;

                if (linha.Length > 8 && linha[2] == ':' && linha[5] == ':')
                {
                    horario = linha.Substring(0, 8);
                    mensagem = linha.Substring(8).TrimStart();
                }

                sb.Append(CampoCsv(horario));
                sb.Append(';');
                sb.AppendLine(CampoCsv(mensagem));
            }

            return sb.ToString();
        }

        private static string CampoCsv(string valor)
        {
            if (valor.IndexOfAny(new[] { ';', '"', '\n' }) >= 0)
                return "\"" + valor.Replace("\"", "\"\"") + "\"";

            return valor;
        }

        // Gera e salva o relatório automaticamente ao final do fluxo via SAP,
        // sem precisar do clique manual no botão "Relatório".
        private string GerarRelatorioAutomatico(IEnumerable<string> arquivosDoLote, string ecm, string pastaDestino)
        {
            HashSet<string> caminhos = new HashSet<string>(arquivosDoLote, StringComparer.OrdinalIgnoreCase);

            List<BatchFileResult> resultadosDoLote = BatchResults
                .Where(x => caminhos.Contains(x.FilePath))
                .ToList();

            if (resultadosDoLote.Count == 0)
                return null;

            string nomeArquivo = $"Relatorio_{ecm}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string caminhoRelatorio = Path.Combine(pastaDestino, nomeArquivo);

            ExcelReportService.GerarRelatorio(resultadosDoLote, GetCheckerNames(), CamposTituloAtuais(), caminhoRelatorio);

            return caminhoRelatorio;
        }

        private string[] CamposTituloAtuais()
        {
            return GetCheckerNames().Contains("Bloco Legenda WAU")
                ? TitleBlockChecker.OrdemCampos
                : new string[0];
        }

        // Gera uma única linha (mesmas colunas da tabela/relatório), separada
        // por TAB em vez de ";", pra colar direto numa planilha do Excel ao
        // copiar a linha selecionada.
        public string GerarLinhaParaCopia(BatchFileResult item)
        {
            List<string> checkerNames = GetCheckerNames();
            string[] camposTitulo = CamposTituloAtuais();

            List<string> colunas = new List<string> { item.FileName };

            foreach (string nomeChecker in checkerNames)
            {
                CheckResult resultado = item.Results.Find(x => x.Checker == nomeChecker);

                string status;

                if (item.OpenFailed || resultado == null)
                    status = "";
                else if (resultado.Skipped)
                    status = "N/A";
                else if (resultado.Success)
                    status = "OK";
                else
                    status = "ERRO: " + string.Join(" | ", resultado.Errors);

                colunas.Add(status);
            }

            CheckResult resultadoBlocoTitulo = item.Results.Find(x => x.Checker == "Bloco Legenda WAU");

            foreach (string nomeCampo in camposTitulo)
            {
                string valor = null;

                if (resultadoBlocoTitulo != null)
                    resultadoBlocoTitulo.Fields.TryGetValue(nomeCampo, out valor);

                colunas.Add(valor ?? "");
            }

            colunas.Add(item.OpenFailed ? "" : item.SheetCount.ToString());

            List<string> avisos = item.Results.SelectMany(r => r.Warnings).Distinct().ToList();
            colunas.Add(string.Join(" | ", avisos));

            return string.Join("\t", colunas.Select(SanitizarParaLinhaCopiada));
        }

        private static string SanitizarParaLinhaCopiada(string valor)
        {
            return (valor ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

    }
}
