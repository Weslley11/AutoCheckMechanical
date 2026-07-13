using System.Windows;
using AutoCheckMechanical.ViewModels;

namespace AutoCheckMechanical
{
    public partial class SapConnectionWindow : Window
    {
        private readonly SapConnectionViewModel _viewModel;

        public SapConnectionWindow()
        {
            InitializeComponent();

            _viewModel = new SapConnectionViewModel(obterSenha: () => txtSenha.Password);

            DataContext = _viewModel;
        }
    }
}
