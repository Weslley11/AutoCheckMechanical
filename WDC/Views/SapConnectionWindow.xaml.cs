using System.Windows;
using WDC.SERVICES;
using WDC.VIEWMODEL;

namespace WDC.Views
{
    public partial class SapConnectionWindow : Window
    {
        private readonly SapConnectionViewModel _viewModel;

        public SapConnectionWindow()
        {
            InitializeComponent();

            _viewModel = new SapConnectionViewModel(obterSenha: () => txtSenha.Password);
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
