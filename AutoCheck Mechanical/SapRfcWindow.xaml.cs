using System.Windows;
using AutoCheckMechanical.ViewModels;

namespace AutoCheckMechanical
{
    public partial class SapRfcWindow : Window
    {
        private readonly SapRfcViewModel _viewModel;

        public SapRfcWindow()
        {
            InitializeComponent();

            _viewModel = new SapRfcViewModel(obterSenha: () => txtSenha.Password);

            DataContext = _viewModel;
        }
    }
}
