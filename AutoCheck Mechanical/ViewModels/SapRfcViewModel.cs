using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoCheckMechanical.Services;

namespace AutoCheckMechanical.ViewModels
{
    // Um sistema/destino SAP disponível no combo "System", igual à tela de
    // logon do SAP GUI (código do destino + descrição).
    public class SistemaSapOption
    {
        public string Codigo { get; }
        public string Descricao { get; }
        public string Exibicao => $"{Codigo} - {Descricao}";

        public SistemaSapOption(string codigo, string descricao)
        {
            Codigo = codigo;
            Descricao = descricao;
        }
    }

    // ViewModel da tela básica de integração SAP via RFC/BAPI (NCo). A senha
    // não é bindada diretamente (PasswordBox não suporta binding seguro), por
    // isso é lida do code-behind via o delegate injetado, igual o WBC faz.
    public class SapRfcViewModel : BaseViewModel
    {
        private readonly Func<string> _obterSenha;

        public ObservableCollection<SistemaSapOption> SistemasDisponiveis { get; } = new ObservableCollection<SistemaSapOption>
        {
            new SistemaSapOption("ED0", "ECC Desenvolvimento"),
            new SistemaSapOption("EM4", "ECC MOCK 4"),
            new SistemaSapOption("EM5", "ECC MOCK 5 - EHP6 Server BRJGS923"),
            new SistemaSapOption("EM6", "ECC MOCK 6"),
            new SistemaSapOption("EP0", "ECC Produção"),
            new SistemaSapOption("EQ0", "ECC - Quality Assurance"),
        };

        private SistemaSapOption _sistemaSelecionado;
        public SistemaSapOption SistemaSelecionado
        {
            get { return _sistemaSelecionado; }
            set { _sistemaSelecionado = value; OnPropertyChanged(); }
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
            SistemaSelecionado = SistemasDisponiveis[4]; // EP0 - ECC Produção
        }

        private void TestarConexao()
        {
            if (SistemaSelecionado == null || string.IsNullOrWhiteSpace(Usuario))
            {
                StatusText = "Preencha ao menos System e User.";
                return;
            }

            Testando = true;
            StatusText = "Conectando...";

            try
            {
                // O SAP Logon (SAPUILandscape.xml) indexa os destinos pelo nome
                // completo exibido no SAP Logon Pad (ex.: "EP0 - ECC Produção"),
                // não pelo código curto de 3 letras (systemid) -- por isso usamos
                // Exibicao, não Codigo, na busca do destino.
                SapRfcService.Instance.TestarConexao(SistemaSelecionado.Exibicao, Client, Usuario, _obterSenha(), Idioma);

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
