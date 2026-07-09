using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using AutoCheckMechanical.ViewModels;

namespace AutoCheckMechanical
{
    public partial class ChecksConfigWindow : Window
    {
        private readonly ChecksConfigViewModel _viewModel;

        public HashSet<string> CheckersDesativados => _viewModel.CheckersDesativados;

        public ChecksConfigWindow(IEnumerable<string> todosOsChecks, ISet<string> desativadosAtuais, bool temaEscuro)
        {
            InitializeComponent();

            AplicarTema(temaEscuro);

            _viewModel = new ChecksConfigViewModel(todosOsChecks, desativadosAtuais, resultado => DialogResult = resultado);

            DataContext = _viewModel;
        }

        private void AplicarTema(bool escuro)
        {
            if (escuro)
            {
                gridRoot.Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
                this.Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
                this.Foreground = new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3));
            }
            else
            {
                gridRoot.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                this.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                this.Foreground = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24));
            }

            txtTitulo.Foreground = this.Foreground;
        }
    }
}
