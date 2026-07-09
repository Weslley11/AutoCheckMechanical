using System.Windows;
using AutoCheckMechanical.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AutoCheckMechanical.Checkers;
using AutoCheckMechanical.Models;
using CheckContextModel = AutoCheckMechanical.Core.CheckContext;
using AutoCheckMechanical.Services;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace AutoCheckMechanical
{
    public partial class MainWindow : Window
    {
        private bool _isPseudoMaximized;
        private Rect _restoreBounds;

        private readonly List<BatchFileResult> _batchResults = new List<BatchFileResult>();
        private string _filtroTexto = "";
        private bool _temaEscuro = true;
        private HashSet<string> _checkersDesativados;

        public MainWindow()
        {
            InitializeComponent();

            txtUser.Text = Environment.UserName;

            _batchResults.AddRange(HistoryStore.Load());
            _checkersDesativados = CheckerSettingsStore.LoadDesativados();

            _temaEscuro = ThemeStore.LoadTemaEscuro();
            AplicarTema(_temaEscuro);
        }

        private void AplicarTema(bool escuro)
        {
            // Paleta inspirada no app WBC (MahApps.Metro, tema BaseDark + accent Steel).
            // Os cinzas (#212121/#373737/#424242/#525252) vieram direto do XAML real do
            // WBC; o azul-acinzentado do accent "Steel" e o "IndianRed" de erro são
            // aproximações razoáveis, não os hex exatos do MahApps.
            if (escuro)
            {
                Resources["BrushTitleBar"] = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21));
                Resources["BrushBanner"] = new SolidColorBrush(Color.FromRgb(0x37, 0x37, 0x37));
                Resources["BrushSidebar"] = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42));
                Resources["BrushPanel"] = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
                Resources["BrushCard"] = new SolidColorBrush(Color.FromRgb(0x37, 0x37, 0x37));
                Resources["BrushBorder"] = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A));
                Resources["BrushAccentTeal"] = new SolidColorBrush(Color.FromRgb(0x5B, 0x7A, 0x93));
                Resources["BrushAccentTealHover"] = new SolidColorBrush(Color.FromRgb(0x71, 0x91, 0xAA));
                Resources["BrushAccentOrange"] = new SolidColorBrush(Color.FromRgb(0xCD, 0x5C, 0x5C));
                Resources["BrushTextPrimary"] = new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3));
                Resources["BrushTextSecondary"] = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
                Resources["BrushHoverSurface"] = new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x45));
            }
            else
            {
                Resources["BrushTitleBar"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                Resources["BrushBanner"] = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
                Resources["BrushSidebar"] = new SolidColorBrush(Color.FromRgb(0xE4, 0xE4, 0xE4));
                Resources["BrushPanel"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                Resources["BrushCard"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                Resources["BrushBorder"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
                Resources["BrushAccentTeal"] = new SolidColorBrush(Color.FromRgb(0x45, 0x60, 0x7A));
                Resources["BrushAccentTealHover"] = new SolidColorBrush(Color.FromRgb(0x56, 0x78, 0x92));
                Resources["BrushAccentOrange"] = new SolidColorBrush(Color.FromRgb(0xB3, 0x39, 0x39));
                Resources["BrushTextPrimary"] = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24));
                Resources["BrushTextSecondary"] = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));
                Resources["BrushHoverSurface"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            }

            _temaEscuro = escuro;

            RebuildResultsGrid();
        }

        private void BtnConfiguracoes_Click(object sender, RoutedEventArgs e)
        {
            AplicarTema(!_temaEscuro);
            ThemeStore.Save(_temaEscuro);

            txtStatus.Text = _temaEscuro ? "Tema escuro aplicado." : "Tema claro aplicado.";
        }

        private void BtnChecks_Click(object sender, RoutedEventArgs e)
        {
            List<string> todosOsChecks = CheckerManager.GetAllCheckerNames();

            ChecksConfigWindow dialog = new ChecksConfigWindow(todosOsChecks, _checkersDesativados, _temaEscuro)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                _checkersDesativados = dialog.CheckersDesativados;
                CheckerSettingsStore.Save(_checkersDesativados);
                RebuildResultsGrid();

                int ativos = todosOsChecks.Count - _checkersDesativados.Count;
                txtStatus.Text = $"{ativos} de {todosOsChecks.Count} check(s) ativado(s).";
            }
        }

        private void BtnHistorico_Click(object sender, RoutedEventArgs e)
        {
            txtFiltro.Text = "";
            gridResults.BringIntoView();

            txtStatus.Text = $"Histórico: {_batchResults.Count} arquivo(s) verificado(s) ao todo.";
        }

        private void BtnLimparHistorico_Click(object sender, RoutedEventArgs e)
        {
            if (_batchResults.Count == 0)
                return;

            MessageBoxResult resposta = MessageBox.Show(
                "Isso vai apagar todo o histórico de verificações salvo neste computador. Deseja continuar?",
                "Limpar Histórico",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (resposta != MessageBoxResult.Yes)
                return;

            _batchResults.Clear();
            HistoryStore.Clear();
            ThumbnailStore.ClearAll();
            RebuildResultsGrid();

            txtStatus.Text = "Histórico apagado.";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SolidWorksSession session = SolidWorksSession.Connect();

            if (!session.IsConnected)
            {
                MessageBox.Show("SolidWorks não encontrado.");
                return;
            }

            if (session.ActiveDocument == null)
            {
                MessageBox.Show("Nenhum documento aberto.");
                return;
            }

            MessageBox.Show("Conectado!\n\n" + session.ActiveDocument.GetTitle());
        }

        private void AddLog(string texto)
        {
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + texto + Environment.NewLine);
            txtLog.ScrollToEnd();
        }

        private SolidWorksSession RefreshConnectionStatus()
        {
            SolidWorksSession session = SolidWorksSession.Connect();

            if (!session.IsConnected)
            {
                AddLog("SolidWorks não encontrado.");
                txtStatus.Text = "SolidWorks desconectado.";

                ledSolidWorks.Fill = Brushes.Red;
                txtSolidWorks.Text = "Desconectado";

                return session;
            }

            ledSolidWorks.Fill = Brushes.LimeGreen;
            txtSolidWorks.Text = "Conectado";

            AddLog("SolidWorks conectado.");

            if (session.ActiveDocument == null)
            {
                AddLog("Nenhum documento aberto.");
                txtStatus.Text = "Nenhum documento.";
                return session;
            }

            txtArquivo.Text = session.ActiveDocument.GetTitle();

            AddLog("Arquivo:");
            AddLog(session.ActiveDocument.GetTitle());

            return session;
        }

        private void BtnReconnect_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
            AddLog("Verificando conexão...");

            RefreshConnectionStatus();
        }

        private void BtnCheckDrawing_Click(object sender, RoutedEventArgs e)
        {
            txtDetalheTitulo.Text = "LOG";
            txtLog.Clear();
            AddLog("Iniciando AutoCheck...");

            SolidWorksSession session = RefreshConnectionStatus();

            if (!session.IsConnected || session.ActiveDocument == null)
                return;

            CheckContextModel context = new CheckContextModel(session.Application, session.ActiveDocument);

            CheckEngine engine = new CheckEngine();

            CheckerManager.Register(engine, _checkersDesativados);

            btnCheckDrawing.IsEnabled = false;
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

                RebuildResultsGrid();
                HistoryStore.Save(_batchResults);

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

                txtStatus.Text = "Check finalizado.";
            }
            finally
            {
                Mouse.OverrideCursor = null;
                btnCheckDrawing.IsEnabled = true;
            }
        }

        private void BtnVerificarArquivos_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Desenhos SolidWorks (*.slddrw)|*.slddrw",
                Multiselect = true,
                Title = "Selecionar desenhos para verificar"
            };

            if (dialog.ShowDialog() != true)
                return;

            SolidWorksSession session = RefreshConnectionStatus();

            if (!session.IsConnected)
                return;

            VerificarArquivos(session, dialog.FileNames);
        }

        // Reaproveitado pelo fluxo manual (VERIFICAR ARQUIVOS...) e pelo fluxo
        // de download automático via SAP.
        private void VerificarArquivos(SolidWorksSession session, IEnumerable<string> filePaths)
        {
            List<string> arquivos = filePaths.ToList();

            if (arquivos.Count == 0)
                return;

            CheckEngine engine = new CheckEngine();
            CheckerManager.Register(engine, _checkersDesativados);

            btnCheckDrawing.IsEnabled = false;
            btnVerificarArquivos.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                AddLog($"Verificando {arquivos.Count} arquivo(s)... A janela do SolidWorks vai abrir e fechar cada arquivo automaticamente, isso é esperado.");

                int concluidos = 0;

                List<BatchFileResult> resultados = BatchCheckRunner.Run(session.Application, engine, arquivos, item =>
                {
                    concluidos++;

                    UpsertBatchResult(item);
                    RebuildResultsGrid();
                    HistoryStore.Save(_batchResults);

                    string statusItem = item.OpenFailed ? "FALHA AO ABRIR" : "OK";
                    AddLog($"[{concluidos}/{arquivos.Count}] {item.FileName} — {statusItem}");
                    txtStatus.Text = $"Verificando... {concluidos}/{arquivos.Count} concluído(s) ({item.FileName}).";

                    // Força o WPF a processar a fila de render agora, já que tudo
                    // roda de forma síncrona nesta thread — sem isso a tabela só
                    // apareceria atualizada quando o lote inteiro terminasse.
                    Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
                });

                int falhas = resultados.Count(r => r.OpenFailed);

                txtStatus.Text = falhas == 0
                    ? $"{resultados.Count} arquivo(s) verificado(s)."
                    : $"{resultados.Count} arquivo(s) verificado(s), {falhas} com falha ao abrir.";
            }
            finally
            {
                Mouse.OverrideCursor = null;
                btnCheckDrawing.IsEnabled = true;
                btnVerificarArquivos.IsEnabled = true;
            }
        }

        private void BtnTrocarPlanilha_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialogPlanilha = new OpenFileDialog
            {
                Filter = "Planilhas Excel (*.xlsm;*.xls;*.xlsb;*.xlsx)|*.xlsm;*.xls;*.xlsb;*.xlsx",
                Title = "Selecionar a planilha com a macro de busca no SAP"
            };

            if (dialogPlanilha.ShowDialog() != true)
                return;

            ExcelMacroSettingsStore.Save(dialogPlanilha.FileName);

            txtStatus.Text = "Planilha do SAP atualizada: " + dialogPlanilha.FileName;
        }

        private void BtnBuscarSap_Click(object sender, RoutedEventArgs e)
        {
            string ecm = txtEcm.Text?.Trim();

            if (string.IsNullOrEmpty(ecm))
            {
                MessageBox.Show("Informe a ECM antes de buscar no SAP.", "Buscar no SAP",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnBuscarSap.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            txtLog.Clear();
            txtDetalheTitulo.Text = "LOG";

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
                    txtStatus.Text = $"Nenhum documento SWD encontrado para a ECM {ecm}.";
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

                    txtStatus.Text = $"{documentos.Count} documento(s) encontrado(s) no SAP, nenhum arquivo local ainda.";
                    return;
                }

                MessageBox.Show(
                    $"ECM: {ecm}\nDocumentos encontrados no SAP: {documentos.Count}\nArquivos baixados: {arquivosBaixados.Length}\n\n" +
                    "Os arquivos serão abertos no SolidWorks e verificados agora.",
                    "Buscar no SAP", MessageBoxButton.OK, MessageBoxImage.Information);

                SolidWorksSession session = RefreshConnectionStatus();

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
                    txtStatus.Text = $"Verificação concluída. Relatório: {caminhoRelatorio}";

                    MessageBox.Show(
                        $"Verificação concluída para a ECM {ecm}.\n\nRelatório detalhado salvo em:\n{caminhoRelatorio}",
                        "Buscar no SAP", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
                btnBuscarSap.IsEnabled = true;
            }
        }

        private void UpsertBatchResult(BatchFileResult item)
        {
            int existingIndex = _batchResults.FindIndex(x => x.FilePath == item.FilePath);

            if (existingIndex >= 0)
                _batchResults[existingIndex] = item;
            else
                _batchResults.Add(item);
        }

        private List<string> GetCheckerNames()
        {
            return CheckerManager.GetAllCheckerNames()
                .Where(nome => !_checkersDesativados.Contains(nome))
                .ToList();
        }

        private void TxtFiltro_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filtroTexto = txtFiltro.Text ?? "";
            RebuildResultsGrid();
        }

        private List<BatchFileResult> GetResultadosFiltrados()
        {
            if (string.IsNullOrWhiteSpace(_filtroTexto))
                return _batchResults;

            return _batchResults
                .Where(x => x.FileName != null &&
                            x.FileName.IndexOf(_filtroTexto, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void RebuildResultsGrid()
        {
            gridResults.RowDefinitions.Clear();
            gridResults.ColumnDefinitions.Clear();
            gridResults.Children.Clear();

            List<string> checkerNames = GetCheckerNames();
            List<BatchFileResult> resultados = GetResultadosFiltrados();

            string[] camposTitulo = checkerNames.Contains("Bloco de Título")
                ? TitleBlockChecker.OrdemCampos
                : new string[0];

            const int colPreview = 0;
            const int colArquivo = 1;
            int colCheckerStart = 2;
            int colTituloStart = colCheckerStart + checkerNames.Count;
            int colFolhas = colTituloStart + camposTitulo.Length;
            int colObservacao = colFolhas + 1;

            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            foreach (string _ in checkerNames)
                gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            foreach (string _ in camposTitulo)
                gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });

            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });

            gridResults.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });

            AddHeaderCell("PRÉVIA", colPreview, 0);
            AddHeaderCell("ARQUIVO", colArquivo, 0, centralizado: false);

            for (int c = 0; c < checkerNames.Count; c++)
                AddHeaderCell(checkerNames[c].ToUpper(), colCheckerStart + c, 0);

            for (int c = 0; c < camposTitulo.Length; c++)
                AddHeaderCell(camposTitulo[c].ToUpper(), colTituloStart + c, 0);

            AddHeaderCell("FOLHAS", colFolhas, 0);
            AddHeaderCell("OBSERVAÇÃO", colObservacao, 0, centralizado: false);

            for (int r = 0; r < resultados.Count; r++)
            {
                gridResults.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });

                BatchFileResult item = resultados[r];
                int rowIndex = r + 1;

                AddPreviewCell(item, colPreview, rowIndex);
                AddFileNameCell(item, colArquivo, rowIndex);

                for (int c = 0; c < checkerNames.Count; c++)
                {
                    CheckResult result = item.Results.Find(x => x.Checker == checkerNames[c]);
                    AddStatusCell(item, result, colCheckerStart + c, rowIndex);
                }

                CheckResult resultadoBlocoTitulo = item.Results.Find(x => x.Checker == "Bloco de Título");

                for (int c = 0; c < camposTitulo.Length; c++)
                    AddFieldValueCell(item, resultadoBlocoTitulo, camposTitulo[c], colTituloStart + c, rowIndex);

                AddSheetCountCell(item, colFolhas, rowIndex);
                AddObservationCell(item, colObservacao, rowIndex);
            }
        }

        private void AddFieldValueCell(BatchFileResult item, CheckResult resultadoBlocoTitulo, string nomeCampo, int column, int row)
        {
            Border border = new Border
            {
                BorderBrush = (Brush)FindResource("BrushBorder"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            string valor = null;

            resultadoBlocoTitulo?.Fields.TryGetValue(nomeCampo, out valor);

            bool preenchido = !string.IsNullOrWhiteSpace(valor);

            border.Child = new TextBlock
            {
                Text = preenchido ? valor : "—",
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = preenchido
                    ? (Brush)FindResource("BrushTextPrimary")
                    : (Brush)FindResource("BrushAccentOrange")
            };

            border.ToolTip = preenchido ? valor : $"Campo \"{nomeCampo}\" vazio.";
            border.MouseLeftButtonUp += (s, e) => ShowFileDetails(item);

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }

        private static BitmapImage CarregarThumbnail(string caminho)
        {
            if (string.IsNullOrEmpty(caminho) || !File.Exists(caminho))
                return null;

            try
            {
                BitmapImage bitmap = new BitmapImage();

                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(caminho, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void AddPreviewCell(BatchFileResult item, int column, int row)
        {
            Border border = new Border
            {
                BorderBrush = (Brush)FindResource("BrushBorder"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(4),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            BitmapImage thumbnail = CarregarThumbnail(item.ThumbnailPath);

            Grid conteudo = new Grid();

            if (thumbnail != null)
            {
                conteudo.Children.Add(new Image
                {
                    Source = thumbnail,
                    Width = 88,
                    Height = 64,
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });

                border.ToolTip = new Image
                {
                    Source = thumbnail,
                    Width = 400,
                    Height = 300,
                    Stretch = Stretch.Uniform
                };
            }
            else
            {
                conteudo.Children.Add(new TextBlock
                {
                    Text = "—",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("BrushTextSecondary"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            if (!string.IsNullOrEmpty(item.FilePath))
            {
                Button btnEDrawings = new Button
                {
                    Content = "",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 11,
                    Width = 20,
                    Height = 20,
                    Padding = new Thickness(0),
                    Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 2, 2),
                    ToolTip = "Abrir no eDrawings"
                };

                btnEDrawings.Click += (s, e) => AbrirNoEDrawings(item.FilePath);

                conteudo.Children.Add(btnEDrawings);
            }

            border.Child = conteudo;
            border.MouseLeftButtonUp += (s, e) => ShowFileDetails(item);

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }

        // Abre o desenho no visualizador eDrawings (independente do
        // SolidWorks), para o usuário conferir o arquivo visualmente sem
        // precisar abrir o SolidWorks completo.
        private void AbrirNoEDrawings(string filePath)
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

        private void AddSheetCountCell(BatchFileResult item, int column, int row)
        {
            Border border = new Border
            {
                BorderBrush = (Brush)FindResource("BrushBorder"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            border.Child = new TextBlock
            {
                Text = item.OpenFailed ? "—" : item.SheetCount.ToString(),
                FontSize = 12,
                FontWeight = item.SheetCount > 1 ? FontWeights.Bold : FontWeights.Normal,
                Foreground = item.SheetCount > 1
                    ? (Brush)FindResource("BrushAccentOrange")
                    : (Brush)FindResource("BrushTextPrimary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            border.MouseLeftButtonUp += (s, e) => ShowFileDetails(item);

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }

        private void AddObservationCell(BatchFileResult item, int column, int row)
        {
            Border border = new Border
            {
                BorderBrush = (Brush)FindResource("BrushBorder"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            List<string> avisos = item.Results.SelectMany(r => r.Warnings).Distinct().ToList();
            bool temChecksDispensados = item.Results.Any(r => r.Skipped);

            TextBlock texto = new TextBlock
            {
                Text = avisos.Count == 0 ? "—" : string.Join(" | ", avisos),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = avisos.Count == 0
                    ? (Brush)FindResource("BrushTextSecondary")
                    : (Brush)FindResource("BrushAccentOrange"),
                VerticalAlignment = VerticalAlignment.Center
            };

            texto.MouseLeftButtonUp += (s, e) => ShowFileDetails(item);

            border.ToolTip = avisos.Count == 0 ? null : string.Join("\n", avisos);

            if (temChecksDispensados)
            {
                Button btnExecutarMesmoAssim = new Button
                {
                    Content = "EXECUTAR MESMO ASSIM",
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    FontSize = 10,
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 4, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                btnExecutarMesmoAssim.Click += (s, e) => ForcarChecksDeChapa(item);

                StackPanel painel = new StackPanel { Orientation = Orientation.Vertical };
                painel.Children.Add(texto);
                painel.Children.Add(btnExecutarMesmoAssim);

                border.Child = painel;
            }
            else
            {
                border.Child = texto;
                border.MouseLeftButtonUp += (s, e) => ShowFileDetails(item);
            }

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }

        // Permite ao usuário forçar a execução dos checks de Layer/Flat
        // Pattern/Scale para um arquivo cujo check foi dispensado
        // automaticamente (sem info de chapa), caso ele julgue necessário.
        private void ForcarChecksDeChapa(BatchFileResult item)
        {
            SolidWorksSession session = RefreshConnectionStatus();

            if (!session.IsConnected)
                return;

            CheckEngine engine = new CheckEngine();
            CheckerManager.Register(engine, _checkersDesativados);

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
                    RebuildResultsGrid();
                    HistoryStore.Save(_batchResults);

                    string statusItem = novoResultado.OpenFailed ? "FALHA AO ABRIR" : "OK";
                    AddLog($"{novoResultado.FileName} — {statusItem}");
                    txtStatus.Text = $"Checks executados em {novoResultado.FileName}.";
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void AddHeaderCell(string text, int column, int row, bool centralizado = true)
        {
            Border border = new Border
            {
                Background = (Brush)FindResource("BrushSidebar"),
                BorderBrush = (Brush)FindResource("BrushBorder"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(6, 6, 6, 6),
                ClipToBounds = true,
                MinHeight = 32
            };

            border.Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("BrushTextSecondary"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = centralizado ? TextAlignment.Center : TextAlignment.Left,
                HorizontalAlignment = centralizado ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }

        private void AddFileNameCell(BatchFileResult item, int column, int row)
        {
            Border border = new Border
            {
                BorderBrush = (Brush)FindResource("BrushBorder"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            border.Child = new TextBlock
            {
                Text = item.FileName,
                Foreground = (Brush)FindResource("BrushTextPrimary"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            BitmapImage thumbnail = CarregarThumbnail(item.ThumbnailPath);

            if (thumbnail != null)
            {
                border.ToolTip = new Image
                {
                    Source = thumbnail,
                    Width = 400,
                    Height = 300,
                    Stretch = Stretch.Uniform
                };
            }

            border.MouseLeftButtonUp += (s, e) => AbrirNoSolidWorks(item);
            border.ToolTip = border.ToolTip ?? "Clique para abrir no SolidWorks.";

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        // Abre o arquivo no SolidWorks (ou apenas ativa a janela, se ele já
        // estiver aberto), ao clicar no nome do arquivo na tabela.
        private void AbrirNoSolidWorks(BatchFileResult item)
        {
            if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
            {
                MessageBox.Show("Arquivo não encontrado:\n" + item.FilePath, "Abrir no SolidWorks",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SolidWorksSession session = RefreshConnectionStatus();

            if (!session.IsConnected)
                return;

            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                SldWorks app = session.Application;

                ModelDoc2 doc = app.GetOpenDocumentByName(item.FilePath) as ModelDoc2;

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
                        ref warnings) as ModelDoc2;

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
                    IFrame frame = app.Frame() as IFrame;
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

        private void AddStatusCell(BatchFileResult item, CheckResult result, int column, int row)
        {
            Border border = new Border
            {
                BorderBrush = (Brush)FindResource("BrushBorder"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            string texto;
            Brush cor;

            if (item.OpenFailed || result == null)
            {
                texto = "—";
                cor = (Brush)FindResource("BrushTextSecondary");
            }
            else if (result.Skipped)
            {
                texto = "N/A";
                cor = (Brush)FindResource("BrushTextSecondary");
            }
            else if (result.Success)
            {
                texto = "OK";
                cor = Brushes.LimeGreen;
            }
            else
            {
                texto = "ERRO";
                cor = (Brush)FindResource("BrushAccentOrange");
            }

            border.Child = new TextBlock
            {
                Text = texto,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = cor,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            border.MouseLeftButtonUp += (s, e) => ShowFileDetails(item);

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }

        private void ShowFileDetails(BatchFileResult item)
        {
            txtDetalheTitulo.Text = "LOG — " + item.FileName;
            txtLog.Clear();

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

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (_isPseudoMaximized)
            {
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;

                _isPseudoMaximized = false;
            }
            else
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);

                Rect workArea = SystemParameters.WorkArea;

                Left = workArea.Left;
                Top = workArea.Top;
                Width = workArea.Width;
                Height = workArea.Height;

                _isPseudoMaximized = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnExportLog_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLog.Text))
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
                    ? ConverterLogParaCsv(txtLog.Text)
                    : txtLog.Text;

                File.WriteAllText(dialog.FileName, conteudo, Encoding.UTF8);

                txtStatus.Text = "Log exportado: " + dialog.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível salvar o arquivo:\n" + ex.Message,
                    "Exportar Log", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void BtnExportarRelatorio_Click(object sender, RoutedEventArgs e)
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
                Filter = "CSV para Excel (*.csv)|*.csv",
                FileName = "AutoCheckMechanical_Relatorio_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                File.WriteAllText(dialog.FileName, GerarCsvRelatorio(resultados), Encoding.UTF8);

                txtStatus.Text = "Relatório exportado: " + dialog.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível salvar o arquivo:\n" + ex.Message,
                    "Exportar Relatório", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Gera e salva o relatório automaticamente ao final do fluxo via SAP,
        // sem precisar do clique manual no botão "Relatório".
        private string GerarRelatorioAutomatico(IEnumerable<string> arquivosDoLote, string ecm, string pastaDestino)
        {
            HashSet<string> caminhos = new HashSet<string>(arquivosDoLote, StringComparer.OrdinalIgnoreCase);

            List<BatchFileResult> resultadosDoLote = _batchResults
                .Where(x => caminhos.Contains(x.FilePath))
                .ToList();

            if (resultadosDoLote.Count == 0)
                return null;

            string nomeArquivo = $"Relatorio_{ecm}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string caminhoRelatorio = Path.Combine(pastaDestino, nomeArquivo);

            File.WriteAllText(caminhoRelatorio, GerarCsvRelatorio(resultadosDoLote), Encoding.UTF8);

            return caminhoRelatorio;
        }

        private string GerarCsvRelatorio(List<BatchFileResult> resultados)
        {
            List<string> checkerNames = GetCheckerNames();

            string[] camposTitulo = checkerNames.Contains("Bloco de Título")
                ? TitleBlockChecker.OrdemCampos
                : new string[0];

            StringBuilder sb = new StringBuilder();

            List<string> cabecalho = new List<string> { "Arquivo" };
            cabecalho.AddRange(checkerNames);
            cabecalho.AddRange(camposTitulo);
            cabecalho.Add("Folhas");
            cabecalho.Add("Observação");

            sb.AppendLine(string.Join(";", cabecalho.Select(CampoCsv)));

            foreach (BatchFileResult item in resultados)
            {
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

                CheckResult resultadoBlocoTitulo = item.Results.Find(x => x.Checker == "Bloco de Título");

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

                sb.AppendLine(string.Join(";", colunas.Select(CampoCsv)));
            }

            return sb.ToString();
        }
    }
}
