using System.Windows;
using AutoCheckMechanical.Services;
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
            _viewModel.CredenciaisEsquecidas += (s, e) => txtSenha.Password = "";

            DataContext = _viewModel;

            SapCredentials salvo = SapCredentialStore.Load();

            if (salvo != null)
            {
                _viewModel.CarregarCredenciaisSalvas(salvo);
                txtSenha.Password = salvo.Senha;
                _viewModel.TentarReconectarAutomaticamente();
            }
        }
    }
}
