using System.Windows;
using AutoCheckMechanical.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoCheckMechanical.Checkers;
using AutoCheckMechanical.Models;
using CheckContextModel = AutoCheckMechanical.Core.CheckContext;
using AutoCheckMechanical.Services;
using Microsoft.Win32;

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
            if (escuro)
            {
                Resources["BrushTitleBar"] = new SolidColorBrush(Color.FromRgb(0x17, 0x1F, 0x26));
                Resources["BrushBanner"] = new SolidColorBrush(Color.FromRgb(0x1F, 0x2E, 0x38));
                Resources["BrushSidebar"] = new SolidColorBrush(Color.FromRgb(0x17, 0x22, 0x2A));
                Resources["BrushPanel"] = new SolidColorBrush(Color.FromRgb(0x1C, 0x28, 0x30));
                Resources["BrushCard"] = new SolidColorBrush(Color.FromRgb(0x22, 0x32, 0x3C));
                Resources["BrushBorder"] = new SolidColorBrush(Color.FromRgb(0x2E, 0x45, 0x52));
                Resources["BrushAccentTeal"] = new SolidColorBrush(Color.FromRgb(0x2F, 0xB8, 0xC4));
                Resources["BrushAccentTealHover"] = new SolidColorBrush(Color.FromRgb(0x4D, 0xD3, 0xDE));
                Resources["BrushAccentOrange"] = new SolidColorBrush(Color.FromRgb(0xD9, 0x64, 0x3A));
                Resources["BrushTextPrimary"] = new SolidColorBrush(Color.FromRgb(0xED, 0xF3, 0xF5));
                Resources["BrushTextSecondary"] = new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAD));
                Resources["BrushHoverSurface"] = new SolidColorBrush(Color.FromRgb(0x26, 0x40, 0x4C));
                Resources["BrushSwatch1"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x4E, 0x68));
                Resources["BrushSwatch2"] = new SolidColorBrush(Color.FromRgb(0x2C, 0x7A, 0x9E));
                Resources["BrushSwatch3"] = new SolidColorBrush(Color.FromRgb(0x3F, 0xA9, 0xC7));
                Resources["BrushSwatch4"] = new SolidColorBrush(Color.FromRgb(0x6F, 0xC6, 0xDE));
                Resources["BrushSwatch5"] = new SolidColorBrush(Color.FromRgb(0x2F, 0xB8, 0xC4));
                Resources["BrushSwatch6"] = new SolidColorBrush(Color.FromRgb(0x17, 0x53, 0x72));
            }
            else
            {
                Resources["BrushTitleBar"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                Resources["BrushBanner"] = new SolidColorBrush(Color.FromRgb(0xE7, 0xEE, 0xF2));
                Resources["BrushSidebar"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF2, 0xF4));
                Resources["BrushPanel"] = new SolidColorBrush(Color.FromRgb(0xF7, 0xF8, 0xFA));
                Resources["BrushCard"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                Resources["BrushBorder"] = new SolidColorBrush(Color.FromRgb(0xD8, 0xE0, 0xE5));
                Resources["BrushAccentTeal"] = new SolidColorBrush(Color.FromRgb(0x0E, 0x93, 0xA5));
                Resources["BrushAccentTealHover"] = new SolidColorBrush(Color.FromRgb(0x14, 0xAD, 0xC1));
                Resources["BrushAccentOrange"] = new SolidColorBrush(Color.FromRgb(0xC1, 0x50, 0x2E));
                Resources["BrushTextPrimary"] = new SolidColorBrush(Color.FromRgb(0x17, 0x24, 0x2B));
                Resources["BrushTextSecondary"] = new SolidColorBrush(Color.FromRgb(0x5C, 0x70, 0x79));
                Resources["BrushHoverSurface"] = new SolidColorBrush(Color.FromRgb(0xE2, 0xE9, 0xEC));
                Resources["BrushSwatch1"] = new SolidColorBrush(Color.FromRgb(0x4A, 0x7E, 0x96));
                Resources["BrushSwatch2"] = new SolidColorBrush(Color.FromRgb(0x2C, 0x7A, 0x9E));
                Resources["BrushSwatch3"] = new SolidColorBrush(Color.FromRgb(0x3F, 0xA9, 0xC7));
                Resources["BrushSwatch4"] = new SolidColorBrush(Color.FromRgb(0x7F, 0xCB, 0xE0));
                Resources["BrushSwatch5"] = new SolidColorBrush(Color.FromRgb(0x17, 0xA6, 0xB8));
                Resources["BrushSwatch6"] = new SolidColorBrush(Color.FromRgb(0x23, 0x5E, 0x78));
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

                    if (result.Success)
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

            CheckEngine engine = new CheckEngine();
            CheckerManager.Register(engine, _checkersDesativados);

            btnCheckDrawing.IsEnabled = false;
            btnVerificarArquivos.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                AddLog($"Verificando {dialog.FileNames.Length} arquivo(s)... A janela do SolidWorks vai abrir e fechar cada arquivo automaticamente, isso é esperado.");

                List<BatchFileResult> resultados = BatchCheckRunner.Run(session.Application, engine, dialog.FileNames);

                int falhas = 0;

                foreach (BatchFileResult item in resultados)
                {
                    UpsertBatchResult(item);

                    if (item.OpenFailed)
                        falhas++;
                }

                RebuildResultsGrid();
                HistoryStore.Save(_batchResults);

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
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true
            };

            BitmapImage thumbnail = CarregarThumbnail(item.ThumbnailPath);

            if (thumbnail != null)
            {
                border.Child = new Image
                {
                    Source = thumbnail,
                    Width = 88,
                    Height = 64,
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

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
                border.Child = new TextBlock
                {
                    Text = "—",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("BrushTextSecondary"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            border.MouseLeftButtonUp += (s, e) => ShowFileDetails(item);

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
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
                HorizontalAlignment = HorizontalAlignment.Center,
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

            List<string> avisos = item.Results.SelectMany(r => r.Warnings).ToList();

            border.Child = new TextBlock
            {
                Text = avisos.Count == 0 ? "—" : string.Join(" | ", avisos),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = avisos.Count == 0
                    ? (Brush)FindResource("BrushTextSecondary")
                    : (Brush)FindResource("BrushAccentOrange"),
                VerticalAlignment = VerticalAlignment.Center
            };

            border.ToolTip = avisos.Count == 0 ? null : string.Join("\n", avisos);
            border.MouseLeftButtonUp += (s, e) => ShowFileDetails(item);

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
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

            border.MouseLeftButtonUp += (s, e) => ShowFileDetails(item);

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
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
                HorizontalAlignment = HorizontalAlignment.Center,
                ClipToBounds = true
            };

            string texto;
            Brush cor;

            if (item.OpenFailed || result == null)
            {
                texto = "—";
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

                if (result.Success)
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
    }
}
