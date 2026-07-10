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
using AutoCheckMechanical.ViewModels;
using Microsoft.Win32;
using SolidWorks.Interop.swconst;
using SwApp = SolidWorks.Interop.sldworks.SldWorks;
using SwModelDoc2 = SolidWorks.Interop.sldworks.ModelDoc2;
using SwFrame = SolidWorks.Interop.sldworks.IFrame;

namespace AutoCheckMechanical
{
    public partial class MainWindow : Window
    {
        private bool _isPseudoMaximized;
        private Rect _restoreBounds;

        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new MainViewModel(
                abrirChecksConfig: (todosOsChecks, desativadosAtuais) =>
                {
                    ChecksConfigWindow dialog = new ChecksConfigWindow(todosOsChecks, desativadosAtuais, ViewModel.TemaEscuro)
                    {
                        Owner = this
                    };

                    return dialog.ShowDialog() == true ? dialog.CheckersDesativados : null;
                },
                minimizar: () => WindowState = WindowState.Minimized,
                maximizarRestaurar: AlternarMaximizarRestaurar,
                fechar: Close);

            DataContext = ViewModel;

            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.LogText))
                    txtLog.ScrollToEnd();
                else if (e.PropertyName == nameof(MainViewModel.TemaEscuro))
                    AplicarTema(ViewModel.TemaEscuro);
            };

            ViewModel.ResultsInvalidated += (s, e) => RebuildResultsGrid();

            AplicarTema(ViewModel.TemaEscuro);
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

            RebuildResultsGrid();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.GarantirConexao();
        }

        private void BtnHistorico_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GoToHistoryCommand.Execute(null);
            gridResults.BringIntoView();
        }

        private void AlternarMaximizarRestaurar()
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

        private void RebuildResultsGrid()
        {
            gridResults.RowDefinitions.Clear();
            gridResults.ColumnDefinitions.Clear();
            gridResults.Children.Clear();

            List<string> checkerNames = ViewModel.GetCheckerNames();
            List<BatchFileResult> resultados = ViewModel.GetResultadosFiltrados();

            string[] camposTitulo = checkerNames.Contains("Bloco Legenda WAU")
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

                CheckResult resultadoBlocoTitulo = item.Results.Find(x => x.Checker == "Bloco Legenda WAU");

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
            bool divergente = resultadoBlocoTitulo != null && resultadoBlocoTitulo.CamposDivergentes.Contains(nomeCampo);

            border.Child = new TextBlock
            {
                Text = preenchido ? valor : "—",
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (preenchido && !divergente)
                    ? (Brush)FindResource("BrushTextPrimary")
                    : (Brush)FindResource("BrushAccentOrange")
            };

            border.ToolTip = divergente
                ? "Divergência entre os campos Material e Matéria-Prima."
                : (preenchido ? valor : $"Campo \"{nomeCampo}\" vazio.");
            border.MouseLeftButtonUp += (s, e) => ViewModel.ShowFileDetails(item);

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
                Image imagemPequena = new Image
                {
                    Source = thumbnail,
                    Width = 88,
                    Height = 64,
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                RenderOptions.SetBitmapScalingMode(imagemPequena, BitmapScalingMode.HighQuality);

                conteudo.Children.Add(imagemPequena);

                Image imagemAmpliada = new Image
                {
                    Source = thumbnail,
                    MaxWidth = 400,
                    MaxHeight = 300,
                    Stretch = Stretch.Uniform
                };

                RenderOptions.SetBitmapScalingMode(imagemAmpliada, BitmapScalingMode.HighQuality);

                border.ToolTip = imagemAmpliada;
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
                    Content = "",
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

                btnEDrawings.Click += (s, e) => ViewModel.AbrirNoEDrawings(item.FilePath);

                conteudo.Children.Add(btnEDrawings);
            }

            border.Child = conteudo;
            border.MouseLeftButtonUp += (s, e) => ViewModel.ShowFileDetails(item);

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

            border.MouseLeftButtonUp += (s, e) => ViewModel.ShowFileDetails(item);

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

            texto.MouseLeftButtonUp += (s, e) => ViewModel.ShowFileDetails(item);

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

                btnExecutarMesmoAssim.Click += (s, e) => ViewModel.ForcarChecksDeChapa(item);

                StackPanel painel = new StackPanel { Orientation = Orientation.Vertical };
                painel.Children.Add(texto);
                painel.Children.Add(btnExecutarMesmoAssim);

                border.Child = painel;
            }
            else
            {
                border.Child = texto;
                border.MouseLeftButtonUp += (s, e) => ViewModel.ShowFileDetails(item);
            }

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
                Image imagemAmpliada = new Image
                {
                    Source = thumbnail,
                    MaxWidth = 400,
                    MaxHeight = 300,
                    Stretch = Stretch.Uniform
                };

                RenderOptions.SetBitmapScalingMode(imagemAmpliada, BitmapScalingMode.HighQuality);

                border.ToolTip = imagemAmpliada;
            }

            border.MouseLeftButtonUp += (s, e) => ViewModel.AbrirNoSolidWorks(item);
            border.ToolTip = border.ToolTip ?? "Clique para abrir no SolidWorks.";

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

            border.MouseLeftButtonUp += (s, e) => ViewModel.ShowFileDetails(item);

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }
    }
}
