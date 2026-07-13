using System.Windows;
using System.Windows.Media;
using AutoCheckMechanical.ViewModels;

namespace AutoCheckMechanical
{
    public partial class SapRfcWindow : Window
    {
        private readonly SapRfcViewModel _viewModel;

        public SapRfcWindow(bool temaEscuro)
        {
            InitializeComponent();

            AplicarTema(temaEscuro);

            _viewModel = new SapRfcViewModel(obterSenha: () => txtSenha.Password);

            DataContext = _viewModel;
        }

        private void AplicarTema(bool escuro)
        {
            if (escuro)
            {
                panelRoot.Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
                this.Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
                this.Foreground = new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3));
            }
            else
            {
                panelRoot.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                this.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                this.Foreground = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24));
            }

            txtTitulo.Foreground = this.Foreground;
        }
    }
}
