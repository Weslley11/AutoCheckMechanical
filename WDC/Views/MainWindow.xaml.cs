using System.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WDC.SERVICES.Checkers;
using WDC.MODEL;
using WDC.VIEWMODEL;
using Microsoft.Win32;

namespace WDC.Views
{
    public partial class MainWindow : Window
    {
        private bool _isPseudoMaximized;
        private Rect _restoreBounds;
        private string _caminhoLinhaSelecionada;

        // Soma das larguras fixas + MinWidth das colunas Star, recalculada a
        // cada RebuildResultsGrid (varia com a quantidade de checks ativos e
        // campos do bloco de título) -- é o "menor tamanho aceitável" da
        // tabela antes de precisar rolar horizontalmente.
        private double _larguraMinimaResultados;

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
                abrirSapConexao: () =>
                {
                    SapConnectionWindow janela = new SapConnectionWindow
                    {
                        Owner = this
                    };

                    janela.ShowDialog();
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
            ViewModel.VerificacaoConcluida += (s, e) => PiscarBarraDeTarefas();

            AplicarTema(ViewModel.TemaEscuro);
        }

        // O check em lote pode demorar, e é comum o usuário trocar de janela
        // enquanto espera -- pisca a barra de tarefas (igual notificação de
        // mensagem nova no Teams/Skype) até o usuário voltar pra essa janela.
        // Só pisca se a janela não estiver em primeiro plano; se o usuário já
        // está olhando o app, não tem por que chamar atenção.
        private void PiscarBarraDeTarefas()
        {
            if (IsActive)
                return;

            FLASHWINFO info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                hwnd = new WindowInteropHelper(this).Handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = uint.MaxValue,
                dwTimeout = 0
            };

            FlashWindowEx(ref info);
        }

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
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

                // SystemParameters.WorkArea é sempre a área útil do monitor
                // PRIMÁRIO, não do monitor onde a janela está de verdade --
                // é por isso que "maximizar" jogava a janela pro outro
                // monitor num setup com mais de uma tela. ObterAreaUtilDoMonitorAtual
                // usa MonitorFromWindow (Win32) pra achar o monitor onde a
                // janela está de fato antes de pegar a área útil dele.
                Rect workArea = ObterAreaUtilDoMonitorAtual();

                Left = workArea.Left;
                Top = workArea.Top;
                Width = workArea.Width;
                Height = workArea.Height;

                _isPseudoMaximized = true;
            }
        }

        private Rect ObterAreaUtilDoMonitorAtual()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            MONITORINFO info = new MONITORINFO();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

            if (!GetMonitorInfo(hMonitor, ref info))
                return SystemParameters.WorkArea;

            // rcWork vem em pixels físicos -- precisa converter pra DIPs
            // (unidades independentes de DPI, que é o que Left/Top/Width/
            // Height da Window esperam) usando o DPI de verdade da janela,
            // senão a janela fica com o tamanho errado em monitores com
            // escala diferente de 100%.
            DpiScale dpi = VisualTreeHelper.GetDpi(this);

            return new Rect(
                info.rcWork.Left / dpi.DpiScaleX,
                info.rcWork.Top / dpi.DpiScaleY,
                (info.rcWork.Right - info.rcWork.Left) / dpi.DpiScaleX,
                (info.rcWork.Bottom - info.rcWork.Top) / dpi.DpiScaleY);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
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
            // Colunas de documento SAP (busca por ECM) -- vazias nas linhas
            // vindas do check normal de arquivo, preenchidas nas linhas
            // vindas de BuscarDocumentosPorEcmCommand.
            const int colDocumento = 2;
            const int colTipo = 3;
            const int colParte = 4;
            const int colVersao = 5;
            const int colDescricaoDocumento = 6;
            const int colPdf = 7;
            int colCheckerStart = 8;
            int colTituloStart = colCheckerStart + checkerNames.Count;
            int colFolhas = colTituloStart + camposTitulo.Length;
            int colObservacao = colFolhas + 1;

            // ARQUIVO/DESCRIÇÃO/OBSERVAÇÃO usam Star (com MinWidth = largura
            // atual) em vez de largura fixa: esticam pra preencher o espaço
            // sobrando quando a janela é mais larga que o necessário. Como o
            // ScrollViewer que envolve gridResults oferece largura "infinita"
            // pro conteúdo (é assim que o scroll horizontal funciona), Star
            // sozinho não bastaria -- por isso ScrollResultados_SizeChanged
            // também define gridResults.Width explicitamente.
            const double larguraMinArquivo = 220;
            const double larguraMinDescricao = 220;
            const double larguraMinObservacao = 260;

            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = larguraMinArquivo });
            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = larguraMinDescricao });
            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            foreach (string _ in checkerNames)
                gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            foreach (string _ in camposTitulo)
                gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });

            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            gridResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = larguraMinObservacao });

            _larguraMinimaResultados = 96 + 110 + 70 + 70 + 80 + 80
                + checkerNames.Count * 140
                + camposTitulo.Length * 170
                + 90
                + larguraMinArquivo + larguraMinDescricao + larguraMinObservacao;

            gridResults.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });

            AddHeaderCell("PRÉVIA", colPreview, 0);
            AddHeaderCell("ARQUIVO", colArquivo, 0, centralizado: false);
            AddHeaderCell("DOCUMENTO", colDocumento, 0);
            AddHeaderCell("TIPO", colTipo, 0);
            AddHeaderCell("PARTE", colParte, 0);
            AddHeaderCell("VERSÃO", colVersao, 0);
            AddHeaderCell("DESCRIÇÃO", colDescricaoDocumento, 0, centralizado: false);
            AddHeaderCell("PDF", colPdf, 0);

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
                AddDocumentoFieldCell(item, item.DocumentoNumero, colDocumento, rowIndex);
                AddDocumentoFieldCell(item, item.DocumentoTipo, colTipo, rowIndex);
                AddDocumentoFieldCell(item, item.DocumentoParte, colParte, rowIndex);
                AddDocumentoFieldCell(item, item.DocumentoVersao, colVersao, rowIndex);
                AddDocumentoFieldCell(item, item.DocumentoDescricao, colDescricaoDocumento, rowIndex, centralizado: false);
                AddDocumentoPdfCell(item, colPdf, rowIndex);

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

            AjustarLarguraGridResultados();
        }

        // Sem isso, o ScrollViewer (que precisa de largura "infinita" pro
        // scroll horizontal funcionar) faz as colunas Star colapsarem pro
        // MinWidth mesmo com espaço sobrando. Fixando gridResults.Width
        // explicitamente no maior valor entre "espaço disponível" e "largura
        // mínima da tabela", as colunas Star voltam a esticar quando cabe, e
        // o scroll horizontal continua aparecendo quando não cabe.
        private void AjustarLarguraGridResultados()
        {
            if (scrollResultados.ViewportWidth <= 0)
                return;

            gridResults.Width = Math.Max(scrollResultados.ViewportWidth, _larguraMinimaResultados);
        }

        private void ScrollResultados_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AjustarLarguraGridResultados();
        }

        // Seleção de linha (destaque visual) pra permitir copiar as
        // informações do arquivo, via Ctrl+C ou pelo menu de botão direito.
        private void SelecionarLinha(BatchFileResult item)
        {
            _caminhoLinhaSelecionada = item?.FilePath;
            RebuildResultsGrid();
        }

        private bool LinhaEstaSelecionada(BatchFileResult item)
        {
            return !string.IsNullOrEmpty(item.FilePath) && item.FilePath == _caminhoLinhaSelecionada;
        }

        private Brush CorFundoLinha(BatchFileResult item)
        {
            return LinhaEstaSelecionada(item)
                ? (Brush)FindResource("BrushHoverSurface")
                : Brushes.Transparent;
        }

        private ContextMenu CriarMenuCopiarLinha(BatchFileResult item)
        {
            MenuItem menuCopiar = new MenuItem { Header = "Copiar linha" };

            menuCopiar.Click += (s, e) =>
            {
                SelecionarLinha(item);
                CopiarLinha(item);
            };

            MenuItem menuRemover = new MenuItem { Header = "Remover da lista" };

            menuRemover.Click += (s, e) => RemoverLinha(item);

            ContextMenu menu = new ContextMenu();
            menu.Items.Add(menuCopiar);
            menu.Items.Add(menuRemover);

            return menu;
        }

        private void CopiarLinha(BatchFileResult item)
        {
            string linha = ViewModel.GerarLinhaParaCopia(item);

            Clipboard.SetText(linha);

            ViewModel.StatusText = $"Linha de \"{item.FileName}\" copiada para a área de transferência.";
        }

        // Permite descartar da lista um documento vindo da busca por ECM
        // que o usuário não quer checar (ou qualquer outra linha antiga).
        private void RemoverLinha(BatchFileResult item)
        {
            if (_caminhoLinhaSelecionada == item.FilePath)
                _caminhoLinhaSelecionada = null;

            ViewModel.RemoverBatchResult(item);
            RebuildResultsGrid();

            ViewModel.StatusText = $"\"{item.FileName}\" removido da lista.";
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.C || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;

            // Deixa o Ctrl+C padrão (copiar texto selecionado) funcionar
            // normalmente quando o foco está numa caixa de texto.
            if (Keyboard.FocusedElement is TextBox)
                return;

            if (string.IsNullOrEmpty(_caminhoLinhaSelecionada))
                return;

            BatchFileResult item = ViewModel.BatchResults.Find(x => x.FilePath == _caminhoLinhaSelecionada);

            if (item == null)
                return;

            CopiarLinha(item);
            e.Handled = true;
        }

        private void AddFieldValueCell(BatchFileResult item, CheckResult resultadoBlocoTitulo, string nomeCampo, int column, int row)
        {
            Border border = new Border
            {
                BorderBrush = (Brush)FindResource("BrushBorder"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8),
                Background = CorFundoLinha(item),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                ContextMenu = CriarMenuCopiarLinha(item)
            };

            string valor = null;

            resultadoBlocoTitulo?.Fields.TryGetValue(nomeCampo, out valor);

            bool preenchido = !string.IsNullOrWhiteSpace(valor);
            bool divergente = resultadoBlocoTitulo != null && resultadoBlocoTitulo.CamposDivergentes.Contains(nomeCampo);
            bool verificado = resultadoBlocoTitulo != null && resultadoBlocoTitulo.CamposVerificados.Contains(nomeCampo);

            Brush corTexto;

            if (divergente)
                corTexto = (Brush)FindResource("BrushAccentOrange");
            else if (!preenchido)
                corTexto = (Brush)FindResource("BrushAccentOrange");
            else if (verificado)
                corTexto = Brushes.LimeGreen;
            else
                corTexto = (Brush)FindResource("BrushTextPrimary");

            border.Child = new TextBlock
            {
                Text = preenchido ? valor : "—",
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = corTexto
            };

            border.ToolTip = divergente
                ? "Divergência entre os campos Material e Matéria-Prima."
                : (preenchido ? valor : $"Campo \"{nomeCampo}\" vazio.");
            border.MouseLeftButtonUp += (s, e) =>
            {
                SelecionarLinha(item);
                ViewModel.ShowFileDetails(item);
            };

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }

        private void AddDocumentoFieldCell(BatchFileResult item, string valor, int column, int row, bool centralizado = true)
        {
            Border border = new Border
            {
                BorderBrush = (Brush)FindResource("BrushBorder"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8),
                Background = CorFundoLinha(item),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                ContextMenu = CriarMenuCopiarLinha(item)
            };

            bool preenchido = !string.IsNullOrWhiteSpace(valor);

            border.Child = new TextBlock
            {
                Text = preenchido ? valor : "—",
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = centralizado ? TextAlignment.Center : TextAlignment.Left,
                HorizontalAlignment = centralizado ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = preenchido
                    ? (Brush)FindResource("BrushTextPrimary")
                    : (Brush)FindResource("BrushTextSecondary")
            };

            border.ToolTip = preenchido ? valor : null;
            border.MouseLeftButtonUp += (s, e) =>
            {
                SelecionarLinha(item);
                ViewModel.ShowFileDetails(item);
            };

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }

        // Indica se o documento (vindo da busca por ECM) tem algum original
        // em PDF vinculado no DMS -- ver a ressalva em DocumentSearchService
        // sobre essa leitura (ApplicationCode/extensão do Path) ainda não
        // estar confirmada contra dados reais do SAP.
        private void AddDocumentoPdfCell(BatchFileResult item, int column, int row)
        {
            Border border = new Border
            {
                BorderBrush = (Brush)FindResource("BrushBorder"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8),
                Background = CorFundoLinha(item),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                ContextMenu = CriarMenuCopiarLinha(item)
            };

            string texto;
            Brush cor;

            if (string.IsNullOrEmpty(item.DocumentoNumero))
            {
                texto = "—";
                cor = (Brush)FindResource("BrushTextSecondary");
            }
            else if (item.DocumentoTemPdf)
            {
                texto = "OK";
                cor = Brushes.LimeGreen;
            }
            else
            {
                texto = "SEM PDF";
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

            border.MouseLeftButtonUp += (s, e) =>
            {
                SelecionarLinha(item);
                ViewModel.ShowFileDetails(item);
            };

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
                Background = CorFundoLinha(item),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                ContextMenu = CriarMenuCopiarLinha(item)
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
                // Modo "eDrawings" (leve, só visual) virou o padrão ao
                // clicar no nome do arquivo (ver AddFileNameCell) -- esse
                // botão no canto da prévia é o modo alternativo, pra abrir
                // no SolidWorks completo quando precisar de verdade (editar,
                // rodar o CHECK DRAWING manual no documento ativo, etc.).
                Button btnSolidWorks = new Button
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
                    ToolTip = "Abrir no SolidWorks"
                };

                btnSolidWorks.Click += (s, e) => ViewModel.AbrirNoSolidWorks(item);

                conteudo.Children.Add(btnSolidWorks);
            }

            border.Child = conteudo;
            border.MouseLeftButtonUp += (s, e) =>
            {
                SelecionarLinha(item);
                ViewModel.ShowFileDetails(item);
            };

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
                Background = CorFundoLinha(item),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                ContextMenu = CriarMenuCopiarLinha(item)
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

            border.MouseLeftButtonUp += (s, e) =>
            {
                SelecionarLinha(item);
                ViewModel.ShowFileDetails(item);
            };

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
                Background = CorFundoLinha(item),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                ContextMenu = CriarMenuCopiarLinha(item)
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

            texto.MouseLeftButtonUp += (s, e) =>
            {
                SelecionarLinha(item);
                ViewModel.ShowFileDetails(item);
            };

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
                border.MouseLeftButtonUp += (s, e) =>
                {
                    SelecionarLinha(item);
                    ViewModel.ShowFileDetails(item);
                };
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
                Background = CorFundoLinha(item),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                ContextMenu = CriarMenuCopiarLinha(item)
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

            // Clique no nome do arquivo abre no eDrawings (visualizador leve,
            // só pra conferência visual rápida) em vez do SolidWorks
            // completo -- os checks em si continuam rodando via SolidWorks
            // (BatchCheckRunner/RunCheckDrawing), só a abertura manual pra
            // olhar o desenho depois é que mudou. "Abrir no SolidWorks"
            // continua disponível pelo botão no canto da prévia (ver
            // AddPreviewCell) pra quem precisa do SolidWorks de verdade.
            border.MouseLeftButtonUp += (s, e) =>
            {
                SelecionarLinha(item);
                ViewModel.AbrirNoEDrawings(item.FilePath);
            };
            border.ToolTip = border.ToolTip ?? "Clique para abrir no eDrawings.";

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
                Background = CorFundoLinha(item),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                ContextMenu = CriarMenuCopiarLinha(item)
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

            border.MouseLeftButtonUp += (s, e) =>
            {
                SelecionarLinha(item);
                ViewModel.ShowFileDetails(item);
            };

            Grid.SetColumn(border, column);
            Grid.SetRow(border, row);
            gridResults.Children.Add(border);
        }
    }
}
