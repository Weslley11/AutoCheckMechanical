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
using WDC.SERVICES.Checkers;
using WDC.SERVICES.Core;
using WDC.MODEL;
using WDC.SERVICES;
using WDC.EDRAWINGS;
using Microsoft.Win32;
using SolidWorks.Interop.swconst;
using CheckContextModel = WDC.SERVICES.Core.CheckContext;
using SwApp = SolidWorks.Interop.sldworks.SldWorks;
using SwModelDoc2 = SolidWorks.Interop.sldworks.ModelDoc2;
using SwFrame = SolidWorks.Interop.sldworks.IFrame;
using SwDrawingDoc = SolidWorks.Interop.sldworks.DrawingDoc;

namespace WDC.VIEWMODEL
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

        // Chaves (ver ChaveDoItem) dos itens verificados/baixados NESTA
        // sessão do app -- ao abrir o programa a tela começa limpa (sem o
        // histórico salvo de sessões anteriores), mesmo com BatchResults já
        // carregado; só aparece tudo de novo se o usuário clicar em
        // "Histórico" (GoToHistoryCommand).
        private readonly HashSet<string> _chavesDestaSessao = new HashSet<string>();

        private bool _mostrarHistoricoCompleto;
        public bool MostrarHistoricoCompleto
        {
            get { return _mostrarHistoricoCompleto; }
            set { _mostrarHistoricoCompleto = value; OnPropertyChanged(); }
        }

        public event EventHandler ResultsInvalidated;

        private void InvalidarResultados()
        {
            ResultsInvalidated?.Invoke(this, EventArgs.Empty);
        }

        // Disparado ao final de uma verificação em lote (VerificarArquivos/
        // RunCheckDocumentosPendentes) -- a View usa isso pra piscar a barra
        // de tarefas, já que o lote pode demorar e o usuário costuma trocar
        // de janela enquanto espera.
        public event EventHandler VerificacaoConcluida;

        private void NotificarVerificacaoConcluida()
        {
            VerificacaoConcluida?.Invoke(this, EventArgs.Empty);
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

        private FiltroStatus _filtroStatusSelecionado = FiltroStatus.Todos;
        public FiltroStatus FiltroStatusSelecionado
        {
            get { return _filtroStatusSelecionado; }
            set
            {
                _filtroStatusSelecionado = value;
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

        private string _sapStatusText = "Desconectado";
        public string SapStatusText
        {
            get { return _sapStatusText; }
            set { _sapStatusText = value; OnPropertyChanged(); }
        }

        private Brush _sapLedBrush = Brushes.Red;
        public Brush SapLedBrush
        {
            get { return _sapLedBrush; }
            set { _sapLedBrush = value; OnPropertyChanged(); }
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

        // Progresso do check em lote (VerificarArquivos/
        // RunCheckDocumentosPendentes) -- ProgressoVisivel fica true só
        // durante o lote em si, não no check de documento único
        // (RunCheckDrawing sem pendentes), onde uma barra não faz sentido
        // (um item só).
        private bool _progressoVisivel;
        public bool ProgressoVisivel
        {
            get { return _progressoVisivel; }
            set { _progressoVisivel = value; OnPropertyChanged(); }
        }

        private int _progressoAtual;
        public int ProgressoAtual
        {
            get { return _progressoAtual; }
            set { _progressoAtual = value; OnPropertyChanged(); }
        }

        private int _progressoTotal;
        public int ProgressoTotal
        {
            get { return _progressoTotal; }
            set { _progressoTotal = value; OnPropertyChanged(); }
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

        // Últimas ECMs buscadas no SAP, mais recente primeiro -- alimenta o
        // dropdown do campo ECM (EcmHistoricoStore). A lista é reatribuída
        // (não mutada) a cada busca bem-sucedida pra disparar OnPropertyChanged
        // e o ComboBox reconhecer que o item novo entrou.
        private List<string> _ecmsRecentes = new List<string>();
        public List<string> EcmsRecentes
        {
            get { return _ecmsRecentes; }
            private set { _ecmsRecentes = value; OnPropertyChanged(); }
        }

        // Pasta padrão onde BaixarDocumentosCommand salva os arquivos
        // (dentro de uma subpasta com o nome da ECM) -- editável direto no
        // campo de texto, persistida a cada alteração.
        private string _pastaDownloadDocumentos;
        public string PastaDownloadDocumentos
        {
            get { return _pastaDownloadDocumentos; }
            set
            {
                _pastaDownloadDocumentos = value;
                OnPropertyChanged();
                DownloadFolderSettingsStore.Save(_pastaDownloadDocumentos);
            }
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
            PastaDownloadDocumentos = DownloadFolderSettingsStore.LoadCaminho() ?? @"C:\SAP_SW";
            EcmsRecentes = EcmHistoricoStore.Load();

            RefreshSapStatus();

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
        // padrão do WBC (SapConnectionInterface.cs). A janela (SapConnectionWindow)
        // já tenta reconectar automaticamente com a credencial salva ao abrir
        // (TentarReconectarAutomaticamente), então clicar aqui já cobre tanto
        // "abrir a tela" quanto "conectar quando estiver desconectado" -- só
        // precisa atualizar o LED/texto depois que a janela (modal) fechar.
        public ICommand AbrirSapConexaoCommand => new DelegateCommand(_ =>
        {
            _abrirSapConexao();
            RefreshSapStatus();
        });

        // Busca de documentos do DMS pela ECM acima, via Web Service
        // ITF_O_S_DOCUMENT_OUTPUT (SOA).
        public ICommand BuscarDocumentosPorEcmCommand => new DelegateCommand(_ => BuscarDocumentosPorEcm(), () => !BuscandoDocumentos);

        // Copia pra uma pasta local o arquivo original (DMS) de cada
        // documento vindo da busca por ECM que ainda esteja na lista.
        public ICommand BaixarDocumentosCommand => new DelegateCommand(_ => BaixarDocumentos(), () => !IsBusy);

        public ICommand SelecionarPastaDownloadCommand => new DelegateCommand(_ =>
        {
            OpenFileDialog dialogPasta = new OpenFileDialog
            {
                Title = "Selecionar a pasta de download",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Selecionar esta pasta"
            };

            if (!string.IsNullOrEmpty(PastaDownloadDocumentos) && Directory.Exists(PastaDownloadDocumentos))
                dialogPasta.InitialDirectory = PastaDownloadDocumentos;

            if (dialogPasta.ShowDialog() != true)
                return;

            PastaDownloadDocumentos = Path.GetDirectoryName(dialogPasta.FileName);
        });

        public ICommand AbrirPastaDownloadCommand => new DelegateCommand(_ =>
        {
            string pasta = PastaDownloadDocumentos?.Trim();

            if (string.IsNullOrEmpty(pasta))
            {
                MessageBox.Show("Informe a pasta de download antes de abrir.", "Abrir pasta",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Cria a pasta se ainda não existir (ex: antes do primeiro
            // download) em vez de só falhar -- é mais útil abrir uma pasta
            // vazia recém-criada do que mostrar um erro "pasta não existe"
            // pra algo que o usuário só quer configurar.
            try
            {
                Directory.CreateDirectory(pasta);
                Process.Start(new ProcessStartInfo
                {
                    FileName = pasta,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível abrir a pasta:\n" + ex.Message,
                    "Abrir pasta", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        public ICommand GoToHistoryCommand => new DelegateCommand(_ =>
        {
            FiltroTexto = "";
            MostrarHistoricoCompleto = true;
            InvalidarResultados();

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
            _chavesDestaSessao.Clear();
            MostrarHistoricoCompleto = false;
            HistoryStore.Clear();
            ThumbnailStore.ClearAll();
            InvalidarResultados();

            StatusText = "Histórico apagado.";
        });

        public ICommand ReconnectCommand => new DelegateCommand(_ =>
        {
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

        // Botão experimental (prova de conceito): abre o MESMO arquivo via
        // eDrawings (WDC.EDRAWINGS.EDrawingsMassService) e via SolidWorks,
        // cronometrando os dois, pra comparar velocidade. Só calcula
        // massa/folhas/camadas (o único subconjunto que a API do eDrawings
        // expõe) -- não roda os checks de verdade (bloco de título, cotas,
        // balões), que continuam exclusivos do SolidWorks. Isolado de
        // propósito do fluxo normal de check pra não arriscar nada que já
        // está funcionando.
        public ICommand TestarEDrawingsCommand => new DelegateCommand(_ => TestarEDrawings(), () => !IsBusy);

        private void TestarEDrawings()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Desenhos SolidWorks (*.slddrw)|*.slddrw",
                Title = "Selecionar desenho para o teste eDrawings x SolidWorks"
            };

            if (dialog.ShowDialog() != true)
                return;

            string caminho = dialog.FileName;

            DetalheTitulo = "LOG";
            AddLog($"Iniciando teste eDrawings x SolidWorks: {Path.GetFileName(caminho)}");

            IsBusy = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                AddLog("Abrindo via eDrawings (controle ActiveX)...");

                EDrawingsMassResult resultadoEDrawings;

                try
                {
                    resultadoEDrawings = EDrawingsMassService.AbrirEMedir(caminho);
                }
                catch (Exception ex)
                {
                    resultadoEDrawings = new EDrawingsMassResult
                    {
                        Sucesso = false,
                        Erro = "Exceção não tratada: " + ex.Message
                    };
                }

                if (resultadoEDrawings.Sucesso)
                {
                    AddLog($"eDrawings OK em {resultadoEDrawings.TempoDecorridoMs} ms -- " +
                        $"Massa: {resultadoEDrawings.MassaKg:0.###} kg | Folhas: {resultadoEDrawings.QuantidadeFolhas} | Camadas: {resultadoEDrawings.QuantidadeCamadas}");
                }
                else
                {
                    AddLog($"eDrawings FALHOU depois de {resultadoEDrawings.TempoDecorridoMs} ms: {resultadoEDrawings.Erro}");
                }

                AddLog("Abrindo o mesmo arquivo via SolidWorks...");

                SolidWorksSession session = GarantirConexao();

                if (!session.IsConnected)
                {
                    AddLog("SolidWorks não conectado -- não dá pra comparar.");
                    return;
                }

                Stopwatch cronometroSw = Stopwatch.StartNew();
                SwApp app = session.Application;
                SwModelDoc2 doc = app.GetOpenDocumentByName(caminho) as SwModelDoc2;
                bool jaEstavaAberto = doc != null;

                try
                {
                    if (doc == null)
                    {
                        int errors = 0;
                        int warnings = 0;

                        doc = app.OpenDoc6(
                            caminho,
                            (int)swDocumentTypes_e.swDocDRAWING,
                            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                            "",
                            ref errors,
                            ref warnings) as SwModelDoc2;
                    }

                    if (doc == null)
                    {
                        AddLog("SolidWorks não conseguiu abrir o arquivo pra comparação.");
                        return;
                    }

                    double massaSw = doc.Extension.CreateMassProperty().Mass;
                    int folhasSw = (doc as SwDrawingDoc)?.GetSheetCount() ?? 0;

                    cronometroSw.Stop();

                    AddLog($"SolidWorks OK em {cronometroSw.ElapsedMilliseconds} ms -- " +
                        $"Massa: {massaSw:0.###} kg | Folhas: {folhasSw}");
                }
                finally
                {
                    if (doc != null && !jaEstavaAberto)
                    {
                        try
                        {
                            app.CloseDoc(doc.GetTitle());
                        }
                        catch (COMException)
                        {
                        }
                    }
                }

                AddLog("--------------------------------");
                AddLog(resultadoEDrawings.Sucesso
                    ? $"Comparação: eDrawings {resultadoEDrawings.TempoDecorridoMs} ms x SolidWorks {cronometroSw.ElapsedMilliseconds} ms."
                    : "Comparação não concluída (eDrawings falhou -- veja o erro acima).");
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
            }
        }

        // Log acumula tudo (buscas, downloads, verificações) em vez de
        // limpar sozinho a cada ação -- ver comentário em VerificarArquivos/
        // RunCheckDrawing/BuscarDocumentosPorEcm. Esse botão é o jeito
        // manual de zerar quando quiser, já que o log só reinicia sozinho
        // ao abrir o app de novo (LogText começa vazio a cada nova
        // MainViewModel).
        public ICommand ClearLogCommand => new DelegateCommand(_ =>
        {
            LogText = "";
            DetalheTitulo = "LOG";
        });

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
                FileName = "WDC_Log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
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
                FileName = "WDC_Relatorio_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
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

        public void RefreshSapStatus()
        {
            if (SapRfcService.Instance.IsSapConnected)
            {
                SapLedBrush = Brushes.LimeGreen;
                SapStatusText = "Conectado (" + SapRfcService.Instance.ConnectedUser + ")";
            }
            else
            {
                SapLedBrush = Brushes.Red;
                SapStatusText = "Desconectado";
            }
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

            if (session.AvisoProcessosDuplicados != null)
                AddLog("ATENÇÃO: " + session.AvisoProcessosDuplicados);

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
            DetalheTitulo = "LOG";
            AddLog("Iniciando verificação (CHECK DRAWING)...");

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

            AddLog("Documento ativo do SolidWorks...");

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

                SalvarLogAutomaticamente("CheckDrawing");
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
            }
        }

        // Roda o check em lote nos documentos vindos da busca por ECM
        // (BuscarDocumentosPorEcmCommand) que ainda não foram checados.
        // Baixa primeiro (se ainda não estiver baixado) o original SWD de
        // cada um pra dentro de PastaDownloadDocumentos\{ECM}\ -- mesma
        // lógica/pasta usada por BaixarDocumentosCommand -- e só então abre
        // o arquivo local no SolidWorks. Documentos que não puderem ser
        // baixados são marcados como falha, sem tentar abrir nada.
        private void RunCheckDocumentosPendentes(List<BatchFileResult> documentosPendentes)
        {
            string pastaBase = PastaDownloadDocumentos?.Trim();

            if (string.IsNullOrEmpty(pastaBase))
            {
                MessageBox.Show("Informe a pasta de download antes de verificar os documentos.", "Check Drawing",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DetalheTitulo = "LOG";
            AddLog($"Verificando {documentosPendentes.Count} documento(s) pendente(s) da busca por ECM...");
            IsBusy = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                List<BatchFileResult> aBaixar = documentosPendentes
                    .Where(x => !ArquivoBaixadoPareceValido(CaminhoLocalEsperado(x, pastaBase)))
                    .ToList();

                if (aBaixar.Count > 0)
                {
                    AddLog($"Baixando {aBaixar.Count} documento(s) pendente(s) antes de checar...");
                    BaixarOriginaisSwd(aBaixar, pastaBase);
                }

                List<BatchFileResult> comArquivo = new List<BatchFileResult>();

                foreach (BatchFileResult pendente in documentosPendentes)
                {
                    string caminhoLocal = CaminhoLocalEsperado(pendente, pastaBase);

                    // De propósito File.Exists aqui, não ArquivoBaixadoPareceValido
                    // -- se o download rodou e salvou alguma coisa, deixa o
                    // SolidWorks tentar abrir de verdade em vez de pular a
                    // abertura com base na assinatura OLE. ArquivoBaixadoPareceValido
                    // continua sendo usado só pra decidir se vale a pena baixar
                    // de novo (acima, em aBaixar).
                    if (File.Exists(caminhoLocal))
                    {
                        pendente.FilePath = caminhoLocal;
                        pendente.DocumentoCaminhoOriginal = caminhoLocal;
                        comArquivo.Add(pendente);
                    }
                    else
                    {
                        pendente.OpenFailed = true;
                        pendente.OpenError = "Não foi possível baixar o arquivo original desse documento.";

                        UpsertBatchResult(pendente);
                        AddLog($"{pendente.DocumentoNumero} — não foi possível baixar o original, check pulado.");
                    }
                }

                InvalidarResultados();
                HistoryStore.Save(BatchResults);

                if (comArquivo.Count == 0)
                {
                    StatusText = "Nenhum documento pôde ser baixado para check.";
                    return;
                }

                SolidWorksSession session = GarantirConexao();

                if (!session.IsConnected)
                    return;

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

                ProgressoVisivel = true;
                ProgressoAtual = 0;
                ProgressoTotal = comArquivo.Count;

                BatchCheckRunner.Run(session, engine, comArquivo.Select(x => x.DocumentoCaminhoOriginal), item =>
                {
                    concluidos++;
                    ProgressoAtual = concluidos;

                    if (pendentesPorCaminho.TryGetValue(item.FilePath, out BatchFileResult pendente))
                    {
                        item.DocumentoNumero = pendente.DocumentoNumero;
                        item.DocumentoTipo = pendente.DocumentoTipo;
                        item.DocumentoParte = pendente.DocumentoParte;
                        item.DocumentoVersao = pendente.DocumentoVersao;
                        item.DocumentoDescricao = pendente.DocumentoDescricao;
                        item.DocumentoCaminhoOriginal = pendente.DocumentoCaminhoOriginal;
                        item.DocumentoTemPdf = pendente.DocumentoTemPdf;
                        item.DocumentoEcm = pendente.DocumentoEcm;
                        item.DocumentoUrlOriginal = pendente.DocumentoUrlOriginal;
                        item.DocumentoUrlOriginalPdf = pendente.DocumentoUrlOriginalPdf;
                    }

                    UpsertBatchResult(item);
                    InvalidarResultados();
                    HistoryStore.Save(BatchResults);

                    string statusItem = item.OpenFailed ? $"FALHA AO ABRIR ({item.OpenError})" : "OK";
                    AddLog($"[{concluidos}/{comArquivo.Count}] {item.FileName} — {statusItem}");
                    LogDetalhesErros(item);
                    StatusText = $"Verificando... {concluidos}/{comArquivo.Count} concluído(s) ({item.FileName}).";

                    // Força o WPF a processar a fila de render agora, já que tudo
                    // roda de forma síncrona nesta thread — sem isso a tabela só
                    // apareceria atualizada quando o lote inteiro terminasse.
                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                });

                InvalidarResultados();
                HistoryStore.Save(BatchResults);

                StatusText = $"{documentosPendentes.Count} documento(s) verificado(s).";

                SalvarLogAutomaticamente("CheckDrawing");

                NotificarVerificacaoConcluida();
                MostrarResumoVerificacao(documentosPendentes);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
                ProgressoVisivel = false;
            }
        }

        // Resumo final mostrado ao usuário depois de verificar os documentos
        // vindos da busca por ECM -- lê o resultado de volta em BatchResults
        // (não de documentosPendentes direto) porque BatchCheckRunner cria
        // um BatchFileResult novo pra cada item processado (não muta o
        // "pendente" original), então quem reflete o resultado real do
        // check é a linha já mesclada em BatchResults via UpsertBatchResult.
        private void MostrarResumoVerificacao(List<BatchFileResult> documentosPendentes)
        {
            HashSet<string> numerosVerificados = new HashSet<string>(
                documentosPendentes.Select(x => x.DocumentoNumero));

            List<BatchFileResult> resultadosFinais = BatchResults
                .Where(x => numerosVerificados.Contains(x.DocumentoNumero))
                .ToList();

            int totalDocumentos = documentosPendentes.Count;
            int comErro = resultadosFinais.Count(TemErro);
            int semErro = totalDocumentos - comErro;
            string ecm = documentosPendentes.FirstOrDefault()?.DocumentoEcm;

            AddLog("--------------------------------");
            AddLog($"Verificação concluída. ECM: {ecm} | Documentos: {totalDocumentos} | Sem erro: {semErro} | Com erro: {comErro}");

            MessageBox.Show(
                "Verificação concluída.\n\n" +
                $"Projeto (ECM): {ecm}\n" +
                $"Documentos verificados: {totalDocumentos}\n" +
                $"Sem erro: {semErro}\n" +
                $"Com erro: {comErro}",
                "Check Drawing",
                MessageBoxButton.OK,
                comErro > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        // Um documento conta como "com erro" tanto se o arquivo nem abriu
        // (OpenFailed) quanto se abriu mas algum check reprovou (excluindo
        // os dispensados/Skipped, que não representam falha).
        private static bool TemErro(BatchFileResult item)
        {
            return item.OpenFailed || item.Results.Any(r => !r.Skipped && !r.Success);
        }

        // O log do lote (RunCheckDocumentosPendentes/VerificarArquivos) só
        // mostrava "OK"/"FALHA AO ABRIR" -- isso é só se o arquivo abriu,
        // não se os checks passaram. Sem essa chamada, "Com erro" no resumo
        // final não tinha nenhum detalhe de qual checker reprovou e por quê
        // (só o check de documento único, via RunCheckDrawing, já mostrava
        // isso).
        private void LogDetalhesErros(BatchFileResult item)
        {
            if (item.OpenFailed)
                return;

            foreach (CheckResult resultado in item.Results)
            {
                if (resultado.Skipped || resultado.Success)
                    continue;

                AddLog($"    [{resultado.Checker}] " + string.Join(" | ", resultado.Errors));
            }
        }

        private static string CaminhoLocalEsperado(BatchFileResult item, string pastaBase)
        {
            string pastaEcm = Path.Combine(pastaBase, string.IsNullOrWhiteSpace(item.DocumentoEcm) ? "SEM_ECM" : item.DocumentoEcm);

            // Preserva a nomenclatura original do SAP quando disponível
            // (mesmo critério já usado pros componentes da estrutura, ver
            // NomeArquivoComponente) em vez de inventar "{numero}_
            // {versão}.ext" -- só cai no nome sintético quando o SAP não
            // devolveu nenhum Path de Original pra esse documento.
            string nomeArquivo = !string.IsNullOrEmpty(item.DocumentoNomeArquivoOriginal)
                ? item.DocumentoNomeArquivoOriginal
                : $"{item.DocumentoNumero}_{item.DocumentoVersao}{DocumentSearchService.ExtensaoParaTipoCad(item.DocumentoTipo)}";

            return Path.Combine(pastaEcm, nomeArquivo);
        }

        // Sem essa checagem, um arquivo ruim deixado por uma tentativa de
        // download anterior faria o CHECK DRAWING achar que "já está
        // baixado" e reusar o arquivo inválido em vez de baixar de novo.
        // A checagem em si (assinatura OLE) fica em
        // DocumentSearchService.EhArquivoSolidWorksValido, reaproveitada
        // também logo após o download por URL, pra falhar rápido com
        // diagnóstico em vez de só descobrir depois.
        private static bool ArquivoBaixadoPareceValido(string caminho)
        {
            return DocumentSearchService.EhArquivoSolidWorksValido(caminho, out _);
        }

        // Grava automaticamente o conteúdo do log (LogText) num arquivo em
        // %APPDATA%\WDC\Logs\, depois de cada verificação.
        // Falha ao salvar (disco cheio, sem permissão etc.) não deve
        // interromper o fluxo de verificação em si, por isso engole a
        // exceção.
        private void SalvarLogAutomaticamente(string prefixo)
        {
            try
            {
                string pastaLogs = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WDC", "Logs");

                Directory.CreateDirectory(pastaLogs);

                string nomeArquivo = $"{prefixo}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                File.WriteAllText(Path.Combine(pastaLogs, nomeArquivo), LogText, Encoding.UTF8);
            }
            catch (Exception)
            {
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

            DetalheTitulo = "LOG";
            AddLog("Iniciando verificação (VERIFICAR ARQUIVOS)...");

            CheckEngine engine = new CheckEngine();
            CheckerManager.Register(engine, CheckersDesativados);

            IsBusy = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                AddLog($"Verificando {arquivos.Count} arquivo(s)... A janela do SolidWorks vai abrir e fechar cada arquivo automaticamente, isso é esperado.");

                int concluidos = 0;

                ProgressoVisivel = true;
                ProgressoAtual = 0;
                ProgressoTotal = arquivos.Count;

                List<BatchFileResult> resultados = BatchCheckRunner.Run(session, engine, arquivos, item =>
                {
                    concluidos++;
                    ProgressoAtual = concluidos;

                    UpsertBatchResult(item);
                    InvalidarResultados();
                    HistoryStore.Save(BatchResults);

                    string statusItem = item.OpenFailed ? $"FALHA AO ABRIR ({item.OpenError})" : "OK";
                    AddLog($"[{concluidos}/{arquivos.Count}] {item.FileName} — {statusItem}");
                    LogDetalhesErros(item);
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

                SalvarLogAutomaticamente("VerificarArquivos");

                NotificarVerificacaoConcluida();
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
                ProgressoVisivel = false;
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

            EcmsRecentes = EcmHistoricoStore.Add(ecm);

            BuscandoDocumentos = true;
            Mouse.OverrideCursor = Cursors.Wait;
            StatusText = $"Buscando documentos da ECM {ecm} no SAP...";
            DetalheTitulo = "LOG";
            AddLog($"Iniciando verificação ECM {ecm}...");
            AddLog($"Buscando documentos da ECM {ecm} no SAP...");

            try
            {
                List<DocumentoEncontrado> resultados = DocumentSearchService.BuscarPorEcm(ecm, UsuarioSap, BuscarUltimaVersao, AddLog);

                foreach (DocumentoEncontrado documento in resultados)
                {
                    // Diagnóstico temporário: mostra o que o SAP devolveu de
                    // "Original" pra cada documento, pra descobrir por que
                    // o caminho não está vindo em alguns casos.
                    AddLog($"{documento.DocumentNumber} ({documento.Type}, versão {documento.Version}): {documento.CaminhosOriginais.Count} original(is), {documento.Estrutura.Count} componente(s) na estrutura, {documento.ComponentesEcm.Count} componente(s) SWA/SWP na ECM.");

                    AddLog(documento.DocumentoSuperior != null
                        ? $"  SuperiorDocument: {documento.DocumentoSuperior.DocumentNumber} ({documento.DocumentoSuperior.Type}, versão {documento.DocumentoSuperior.Version})"
                        : "  SuperiorDocument: (vazio)");

                    AddLog(documento.MateriaisVinculados.Count > 0
                        ? $"  MasterMaterialList: {string.Join(", ", documento.MateriaisVinculados)}"
                        : "  MasterMaterialList: (vazio)");

                    foreach (string debug in documento.OriginaisDebug)
                        AddLog("  " + debug);

                    UpsertBatchResult(new BatchFileResult
                    {
                        // O "Original" que o SAP devolve aqui (caminhoOriginal)
                        // NÃO é um caminho de arquivo de verdade -- é só a
                        // convenção de nome local que o SAP GUI/macro antigo
                        // usava ("C:\SAP_SW\{numero}{tipo}{versão}.SLDDRW"),
                        // sem relação com o arquivo que baixamos de verdade
                        // (CaminhoLocalEsperado, "{numero}_{versão}.SLDDRW"
                        // dentro da pasta de download configurada). Usar
                        // caminhoOriginal como FilePath já causou "Arquivo
                        // não encontrado" ao clicar pra abrir antes de baixar
                        // -- por isso aqui sempre fica um identificador
                        // sintético até o arquivo real existir (preenchido em
                        // BaixarDocumentos/RunCheckDocumentosPendentes).
                        FilePath = $"SAP:{documento.DocumentNumber}:{documento.Version}",
                        FileName = documento.DocumentNumber,
                        DocumentoNumero = documento.DocumentNumber,
                        DocumentoTipo = documento.Type,
                        DocumentoParte = documento.Part,
                        DocumentoVersao = documento.Version,
                        DocumentoDescricao = documento.Descricao,
                        DocumentoTemPdf = documento.TemPdf,
                        DocumentoUrlOriginal = documento.UrlOriginalNativo,
                        DocumentoUrlOriginalPdf = documento.UrlOriginalPdf,
                        DocumentoNomeArquivoOriginal = !string.IsNullOrEmpty(documento.CaminhoOriginalNativo)
                            ? Path.GetFileName(documento.CaminhoOriginalNativo)
                            : null,
                        DocumentoEcm = ecm,
                        DocumentoEstruturaRaizes = documento.Estrutura.ToList(),
                        DocumentoComponentesDiretos = documento.ComponentesEcm.ToList(),
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

        // Baixa o original SWD de cada documento da lista -- ver
        // BaixarOriginaisSwd (chama o serviço PI ITF_O_S_DOCUMENT e lê o
        // arquivo publicado direto da pasta de interface na rede).
        private void BaixarDocumentos()
        {
            List<BatchFileResult> documentos = BatchResults
                .Where(x => !string.IsNullOrEmpty(x.DocumentoNumero))
                .ToList();

            if (documentos.Count == 0)
            {
                MessageBox.Show("Não há documentos na lista para baixar.", "Baixar documentos",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string pastaBase = PastaDownloadDocumentos?.Trim();

            if (string.IsNullOrEmpty(pastaBase))
            {
                MessageBox.Show("Informe a pasta de download antes de baixar os documentos.", "Baixar documentos",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            Mouse.OverrideCursor = Cursors.Wait;
            DetalheTitulo = "LOG";
            AddLog($"Baixando {documentos.Count} documento(s) para {pastaBase}...");

            try
            {
                Dictionary<string, string> resultado = BaixarOriginaisSwd(documentos, pastaBase);

                // Sem isso, FilePath/DocumentoCaminhoOriginal continuam com o
                // identificador sintético ("SAP:...") mesmo depois do
                // download ter dado certo -- e "Abrir no SolidWorks"
                // (clicando na linha) tentaria abrir esse identificador em
                // vez do arquivo real que acabou de ser salvo.
                foreach (BatchFileResult item in documentos)
                {
                    if (resultado.TryGetValue(item.DocumentoNumero, out string caminhoLocal) && !string.IsNullOrEmpty(caminhoLocal))
                    {
                        item.FilePath = caminhoLocal;
                        item.DocumentoCaminhoOriginal = caminhoLocal;
                    }
                }

                InvalidarResultados();
                HistoryStore.Save(BatchResults);

                int copiados = resultado.Values.Count(v => !string.IsNullOrEmpty(v));
                int falhas = resultado.Count - copiados;

                StatusText = falhas == 0
                    ? $"{copiados} documento(s) baixado(s) para {pastaBase}."
                    : $"{copiados} documento(s) baixado(s), {falhas} com falha (veja o log).";
            }
            catch (Exception ex)
            {
                AddLog("Erro ao baixar documentos: " + ex.Message);
                StatusText = "Falha ao baixar documentos: " + ex.Message;
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsBusy = false;
            }
        }

        // Baixa o original SWD de cada documento em "alvo" pra dentro de
        // pastaBase\{ECM}\ -- reaproveitado tanto por BaixarDocumentosCommand
        // quanto por RunCheckDocumentosPendentes (que precisa do arquivo
        // local antes de poder abrir no SolidWorks).
        //
        // O Windows UserName ("weslley") não é um usuário SAP válido --
        // confirmado (erro real do SAP: "Usuário weslley não existe" ao
        // mandar como UserCode no ITF_O_S_DOCUMENT, que valida isso porque
        // CheckIn representa uma ação de checkout de verdade, atribuída a um
        // usuário). Usa o usuário SAP de verdade, já autenticado na tela de
        // login RFC (CONEXÃO SAP), quando disponível -- em maiúsculas, já
        // que usuário SAP é normalizado em maiúsculas internamente (SU01) e
        // a validação do UserCode aqui parece ser sensível a caixa.
        private static string UsuarioSap => (SapRfcService.Instance.IsSapConnected
            ? SapRfcService.Instance.ConnectedUser
            : Environment.UserName)?.ToUpperInvariant();

        // VOLTOU pro mecanismo via URL -- esse é o estado exato de quando
        // os arquivos abriram normalmente no SolidWorks (confirmado pelo
        // usuário). ITF_O_S_DOCUMENT (PI + leitura da pasta de rede)
        // continua implementado em DocumentSearchService.
        // BaixarOriginalViaItfDocument, só não é chamado daqui.
        //
        // Documentos sem URL (o Web Service só devolve, pro original nativo,
        // um caminho de convenção local -- "C:\SAP_SW\...", não um caminho
        // de rede copiável) ficam marcados como falha. Baixa também o PDF
        // (BaixarPdf) e a estrutura completa (BaixarEstruturaCompleta) de
        // cada documento, quando existirem -- pedido pra ter todos os
        // arquivos relacionados (SWD/SWA/SWP/PDF) disponíveis localmente,
        // não só o desenho principal.
        private Dictionary<string, string> BaixarOriginaisSwd(List<BatchFileResult> alvo, string pastaBase)
        {
            Dictionary<string, string> resultado = new Dictionary<string, string>();

            foreach (BatchFileResult item in alvo)
            {
                if (string.IsNullOrEmpty(item.DocumentoUrlOriginal))
                {
                    resultado[item.DocumentoNumero] = null;
                    AddLog($"{item.DocumentoNumero} — sem URL de download (o SAP não devolveu uma URL pra esse original).");
                    continue;
                }

                string caminhoLocal = CaminhoLocalEsperado(item, pastaBase);
                Directory.CreateDirectory(Path.GetDirectoryName(caminhoLocal));

                try
                {
                    DocumentSearchService.BaixarOriginalPorUrl(item.DocumentoUrlOriginal, caminhoLocal);

                    resultado[item.DocumentoNumero] = caminhoLocal;
                    AddLog($"{item.DocumentoNumero} — baixado via URL para {caminhoLocal}.");

                    BaixarPdf(item, caminhoLocal);
                    BaixarEstruturaCompleta(item, Path.GetDirectoryName(caminhoLocal));
                }
                catch (Exception ex)
                {
                    resultado[item.DocumentoNumero] = null;
                    AddLog($"{item.DocumentoNumero} — falha ao baixar via URL: {DocumentSearchService.DescreverErroCompleto(ex)}");
                }
            }

            return resultado;
        }

        // Melhor esforço: o PDF é um complemento do original CAD (SWD/SWA/
        // SWP), não o arquivo principal -- falha aqui não invalida o
        // download do item.
        private void BaixarPdf(BatchFileResult item, string caminhoArquivoNativo)
        {
            if (string.IsNullOrEmpty(item.DocumentoUrlOriginalPdf))
                return;

            string caminhoPdf = Path.ChangeExtension(caminhoArquivoNativo, ".pdf");

            try
            {
                DocumentSearchService.BaixarOriginalPorUrl(item.DocumentoUrlOriginalPdf, caminhoPdf);
                AddLog($"{item.DocumentoNumero} — PDF baixado para {caminhoPdf}.");
            }
            catch (Exception ex)
            {
                AddLog($"{item.DocumentoNumero} — falha ao baixar o PDF: {DocumentSearchService.DescreverErroCompleto(ex)}");
            }
        }

        // Sem os componentes (montagens/peças) que o desenho referencia no
        // disco, junto com ele, o SolidWorks não acha as referências ao
        // abrir e mostra tudo suprimido -- por isso baixa a estrutura toda
        // (recursiva, ver DocumentSearchService.ResolverEstruturaCompleta)
        // pra dentro da MESMA pasta do arquivo principal, que é onde o
        // SolidWorks procura por padrão quando não acha um componente no
        // caminho gravado na montagem. Melhor esforço: falha aqui não
        // invalida o download do arquivo principal em si, só fica registrado
        // no log (o desenho pode continuar aparecendo com componentes
        // suprimidos se algum componente não puder ser resolvido/baixado).
        private void BaixarEstruturaCompleta(BatchFileResult item, string pastaDestino)
        {
            if (item.DocumentoComponentesDiretos.Count == 0 && item.DocumentoEstruturaRaizes.Count == 0)
                return;

            // DocumentoComponentesDiretos (SWA/SWP achados na própria busca
            // por ECM, já com URL etc.) não precisa de round-trip nenhum no
            // SAP. DocumentoEstruturaRaizes (DocumentStructureList/
            // SuperiorDocument -- só chaves) continua sendo resolvido à
            // parte, caso essa informação venha preenchida em alguma ECM no
            // futuro. As duas fontes são combinadas (sem duplicar, por
            // DocumentNumber+Type+Part+Version) antes de baixar.
            List<DocumentoEncontrado> componentes = new List<DocumentoEncontrado>(item.DocumentoComponentesDiretos);

            if (item.DocumentoEstruturaRaizes.Count > 0)
            {
                AddLog($"{item.DocumentoNumero} — resolvendo estrutura adicional ({item.DocumentoEstruturaRaizes.Count} componente(s) via DocumentStructureList)...");

                try
                {
                    List<DocumentoEncontrado> resolvidos = DocumentSearchService.ResolverEstruturaCompleta(
                        item.DocumentoEstruturaRaizes, UsuarioSap, msg => AddLog("  " + msg));

                    HashSet<string> jaIncluidos = new HashSet<string>(
                        componentes.Select(ChaveComponenteEcm), StringComparer.OrdinalIgnoreCase);

                    componentes.AddRange(resolvidos.Where(r => jaIncluidos.Add(ChaveComponenteEcm(r))));
                }
                catch (Exception ex)
                {
                    AddLog($"{item.DocumentoNumero} — falha ao resolver a estrutura: {DocumentSearchService.DescreverErroCompleto(ex)}");
                }
            }

            if (componentes.Count == 0)
                return;

            AddLog($"{item.DocumentoNumero} — baixando {componentes.Count} componente(s) relacionado(s) (SWA/SWP da ECM)...");

            int disponiveis = 0;

            foreach (DocumentoEncontrado componente in componentes)
            {
                string caminhoComponente = Path.Combine(pastaDestino, NomeArquivoComponente(componente));

                // Já baixado antes (componente compartilhado por outro
                // desenho já verificado, por exemplo) -- não baixa de novo.
                if (File.Exists(caminhoComponente))
                {
                    disponiveis++;
                    continue;
                }

                if (string.IsNullOrEmpty(componente.UrlOriginalNativo))
                {
                    AddLog($"  {componente.DocumentNumber} — sem URL de download, componente pulado (pode continuar aparecendo suprimido no SolidWorks).");
                    continue;
                }

                try
                {
                    DocumentSearchService.BaixarOriginalPorUrl(componente.UrlOriginalNativo, caminhoComponente);
                    disponiveis++;
                    AddLog($"  {componente.DocumentNumber} — componente baixado para {caminhoComponente}.");

                    if (!string.IsNullOrEmpty(componente.UrlOriginalPdf))
                    {
                        string caminhoPdfComponente = Path.ChangeExtension(caminhoComponente, ".pdf");

                        try
                        {
                            DocumentSearchService.BaixarOriginalPorUrl(componente.UrlOriginalPdf, caminhoPdfComponente);
                            AddLog($"  {componente.DocumentNumber} — PDF do componente baixado para {caminhoPdfComponente}.");
                        }
                        catch (Exception ex)
                        {
                            AddLog($"  {componente.DocumentNumber} — falha ao baixar o PDF do componente: {DocumentSearchService.DescreverErroCompleto(ex)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"  {componente.DocumentNumber} — falha ao baixar componente: {DocumentSearchService.DescreverErroCompleto(ex)}");
                }
            }

            AddLog($"{item.DocumentoNumero} — estrutura: {disponiveis}/{componentes.Count} componente(s) disponível(is) localmente.");
        }

        private static string ChaveComponenteEcm(DocumentoEncontrado componente)
        {
            return $"{componente.DocumentNumber}|{componente.Type}|{componente.Part}|{componente.Version}";
        }

        // O nome do arquivo local do componente precisa bater com o que a
        // montagem espera encontrar pra resolver a referência automaticamente
        // -- não temos garantia de qual é esse nome, então usamos o Path que
        // o SAP devolve pro Original nativo do componente como melhor
        // palpite (ainda não confirmado contra uma estrutura real). Sem
        // Path nenhum, cai num nome sintético com a extensão certa pro tipo
        // (SWA/SWP/SWD, via ExtensaoParaTipoCad) só pra o arquivo existir
        // localmente (não deve resolver a referência automaticamente, mas
        // ao menos fica disponível pra "Localizar referências" manual no
        // SolidWorks).
        private static string NomeArquivoComponente(DocumentoEncontrado componente)
        {
            string nomeOriginal = !string.IsNullOrEmpty(componente.CaminhoOriginalNativo)
                ? Path.GetFileName(componente.CaminhoOriginalNativo)
                : null;

            if (!string.IsNullOrEmpty(nomeOriginal))
                return nomeOriginal;

            string extensao = DocumentSearchService.ExtensaoParaTipoCad(
                componente.TipoOriginalNativo ?? componente.Type);

            return $"{componente.DocumentNumber}_{componente.Version}{extensao}";
        }

        // Linhas vindas da busca por ECM são identificadas por DocumentoNumero
        // (estável durante todo o ciclo de vida: pendente -> baixado ->
        // checado), não por FilePath -- que muda de um identificador
        // sintético pro caminho local de verdade assim que o documento é
        // baixado/checado. Usar FilePath como chave nesse caso duplicaria a
        // linha em vez de substituí-la.
        private void UpsertBatchResult(BatchFileResult item)
        {
            int existingIndex = !string.IsNullOrEmpty(item.DocumentoNumero)
                ? BatchResults.FindIndex(x => x.DocumentoNumero == item.DocumentoNumero)
                : BatchResults.FindIndex(x => x.FilePath == item.FilePath);

            if (existingIndex >= 0)
                BatchResults[existingIndex] = item;
            else
                BatchResults.Add(item);

            _chavesDestaSessao.Add(ChaveDoItem(item));
        }

        private static string ChaveDoItem(BatchFileResult item)
        {
            return !string.IsNullOrEmpty(item.DocumentoNumero) ? "DOC:" + item.DocumentoNumero : "FILE:" + item.FilePath;
        }

        public List<string> GetCheckerNames()
        {
            return CheckerManager.GetAllCheckerNames()
                .Where(nome => !CheckersDesativados.Contains(nome))
                .ToList();
        }

        public List<BatchFileResult> GetResultadosFiltrados()
        {
            IEnumerable<BatchFileResult> baseList = MostrarHistoricoCompleto
                ? BatchResults
                : BatchResults.Where(x => _chavesDestaSessao.Contains(ChaveDoItem(x)));

            if (!string.IsNullOrWhiteSpace(FiltroTexto))
            {
                baseList = baseList.Where(x => x.FileName != null &&
                    x.FileName.IndexOf(FiltroTexto, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            switch (FiltroStatusSelecionado)
            {
                case FiltroStatus.ComErro:
                    baseList = baseList.Where(TemErro);
                    break;
                case FiltroStatus.SemErro:
                    baseList = baseList.Where(x => !EstaPendente(x) && !TemErro(x));
                    break;
                case FiltroStatus.Pendente:
                    baseList = baseList.Where(EstaPendente);
                    break;
            }

            return baseList.ToList();
        }

        // Ainda não foi aberto/checado nenhuma vez -- mesma condição usada
        // em RunCheckDrawing pra decidir se há documentos pendentes da
        // busca por ECM esperando verificação.
        private static bool EstaPendente(BatchFileResult item)
        {
            return item.Results.Count == 0 && !item.OpenFailed;
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
            // FilePath ainda é o identificador sintético ("SAP:...") de uma
            // linha vinda da busca por ECM que ainda não foi baixada -- não é
            // um caminho de arquivo de verdade, então nem faz sentido testar
            // File.Exists nele (avisa de forma específica em vez de mostrar
            // "Arquivo não encontrado: SAP:...", que parece um caminho real).
            if (!string.IsNullOrEmpty(item.FilePath) && item.FilePath.StartsWith("SAP:", StringComparison.Ordinal))
            {
                MessageBox.Show(
                    "Esse documento ainda não foi baixado.\nUse \"BAIXAR DOCUMENTOS\" ou \"CHECK DRAWING\" primeiro.",
                    "Abrir no SolidWorks", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

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

                    // swOpenDocOptions_ViewOnly abre o desenho em modo
                    // "Detailing" (o mesmo do diálogo Abrir do próprio
                    // SolidWorks: não carrega o modelo 3D referenciado, só a
                    // vista 2D já em cache) -- bem mais rápido pra só olhar o
                    // desenho, que é o único propósito deste botão (o check
                    // de massa/geometria precisa do modelo carregado de
                    // verdade, por isso essa flag NÃO é usada em
                    // BatchCheckRunner/RunCheckDrawing). Não confirmado
                    // contra a documentação oficial da API (help.solidworks.
                    // com bloqueado pela política de rede deste ambiente) --
                    // baseado em várias fontes de terceiros convergindo no
                    // mesmo nome/comportamento pra swDocDRAWING. Se abrir
                    // resolvido mesmo assim (ou der erro), é o primeiro
                    // lugar a conferir.
                    doc = app.OpenDoc6(
                        item.FilePath,
                        (int)swDocumentTypes_e.swDocDRAWING,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (int)swOpenDocOptions_e.swOpenDocOptions_ViewOnly,
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
            if (!string.IsNullOrEmpty(filePath) && filePath.StartsWith("SAP:", StringComparison.Ordinal))
            {
                MessageBox.Show(
                    "Esse documento ainda não foi baixado.\nUse \"BAIXAR DOCUMENTOS\" ou \"CHECK DRAWING\" primeiro.",
                    "Abrir no eDrawings", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

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
                    session,
                    engine,
                    new[] { item.FilePath },
                    forcarChecksDeChapa: true);

                BatchFileResult novoResultado = resultados.FirstOrDefault();

                if (novoResultado != null)
                {
                    UpsertBatchResult(novoResultado);
                    InvalidarResultados();
                    HistoryStore.Save(BatchResults);

                    string statusItem = novoResultado.OpenFailed ? $"FALHA AO ABRIR ({novoResultado.OpenError})" : "OK";
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

            List<string> colunas = new List<string>
            {
                item.FileName,
                item.DocumentoNumero ?? "",
                item.DocumentoTipo ?? "",
                item.DocumentoParte ?? "",
                item.DocumentoVersao ?? "",
                item.DocumentoDescricao ?? "",
                string.IsNullOrEmpty(item.DocumentoNumero) ? "" : (item.DocumentoTemPdf ? "OK" : "SEM PDF"),
            };

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
