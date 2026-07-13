using System;
using System.Windows.Input;
using AutoCheckMechanical.Services;

namespace AutoCheckMechanical.ViewModels
{
    // ViewModel da tela básica de integração SAP via RFC/BAPI (NCo). A senha
    // não é bindada diretamente (PasswordBox não suporta binding seguro), por
    // isso é lida do code-behind via o delegate injetado, igual o WBC faz.
    public class SapRfcViewModel : BaseViewModel
    {
        private readonly Func<string> _obterSenha;

        private string _sistema = "";
        public string Sistema
        {
            get { return _sistema; }
            set { _sistema = value; OnPropertyChanged(); }
        }

        private string _client = "";
        public string Client
        {
            get { return _client; }
            set { _client = value; OnPropertyChanged(); }
        }

        private string _usuario = "";
        public string Usuario
        {
            get { return _usuario; }
            set { _usuario = value; OnPropertyChanged(); }
        }

        private string _idioma = "PT";
        public string Idioma
        {
            get { return _idioma; }
            set { _idioma = value; OnPropertyChanged(); }
        }

        private string _statusText = "";
        public string StatusText
        {
            get { return _statusText; }
            set { _statusText = value; OnPropertyChanged(); }
        }

        private bool _testando;
        public bool Testando
        {
            get { return _testando; }
            set { _testando = value; OnPropertyChanged(); }
        }

        public ICommand TestConnectionCommand => new DelegateCommand(_ => TestarConexao(), () => !Testando);

        public SapRfcViewModel(Func<string> obterSenha)
        {
            _obterSenha = obterSenha;
        }

        private void TestarConexao()
        {
            if (string.IsNullOrWhiteSpace(Sistema) || string.IsNullOrWhiteSpace(Usuario))
            {
                StatusText = "Preencha ao menos Sistema e Usuário.";
                return;
            }

            Testando = true;
            StatusText = "Conectando...";

            try
            {
                SapRfcService.Instance.TestarConexao(Sistema, Client, Usuario, _obterSenha(), Idioma);

                StatusText =
                    $"Conectado com sucesso.\nUsuário: {SapRfcService.Instance.UsuarioConectado}\nSistema: {SapRfcService.Instance.SistemaConectado}";
            }
            catch (Exception ex)
            {
                StatusText = "Falha na conexão: " + ex.Message;
            }
            finally
            {
                Testando = false;
            }
        }
    }
}
