using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using WDC.SERVICES;

namespace WDC.VIEWMODEL
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

    // Tela de conexão com o SAP via RFC/BAPI (SAP .NET Connector / NCo), no
    // mesmo padrão real do WBC (SapConnectionInterface.cs + CommonVM.cs,
    // fornecidos como referência direta).
    //
    // Igual o WBC: depois do primeiro login bem-sucedido, as credenciais
    // ficam salvas (criptografadas) e as próximas aberturas reconectam
    // sozinhas, sem precisar digitar usuário/senha de novo -- só volta a
    // pedir login se a reconexão automática falhar ou se o usuário usar
    // "Esquecer login salvo".
    public class SapConnectionViewModel : BaseViewModel
    {
        private readonly Func<string> _obterSenha;

        public event EventHandler CredenciaisEsquecidas;

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

        private bool _conectando;
        public bool Conectando
        {
            get { return _conectando; }
            set { _conectando = value; OnPropertyChanged(); }
        }

        public ICommand TestConnectionCommand => new DelegateCommand(_ => Conectar(false), () => !Conectando);

        public ICommand EsquecerLoginCommand => new DelegateCommand(_ => EsquecerLogin());

        public SapConnectionViewModel(Func<string> obterSenha)
        {
            _obterSenha = obterSenha;
            SistemaSelecionado = SistemasDisponiveis[4]; // EP0 - ECC Produção
        }

        // Preenche os campos (exceto a senha, que o code-behind seta direto
        // na PasswordBox) a partir de uma credencial salva anteriormente.
        public void CarregarCredenciaisSalvas(SapCredentials salvo)
        {
            foreach (SistemaSapOption opcao in SistemasDisponiveis)
            {
                if (opcao.Codigo == salvo.SistemaCodigo)
                {
                    SistemaSelecionado = opcao;
                    break;
                }
            }

            Client = salvo.Client;
            Usuario = salvo.Usuario;
            Idioma = salvo.Idioma;
        }

        public void TentarReconectarAutomaticamente()
        {
            Conectar(automatico: true);
        }

        private void Conectar(bool automatico)
        {
            if (SistemaSelecionado == null || string.IsNullOrWhiteSpace(Usuario))
            {
                if (!automatico)
                    StatusText = "Preencha ao menos System e User.";

                return;
            }

            string senha = _obterSenha();

            if (string.IsNullOrWhiteSpace(senha))
            {
                if (!automatico)
                    StatusText = "Preencha a senha.";

                return;
            }

            Conectando = true;
            StatusText = automatico ? "Reconectando automaticamente..." : "Conectando...";

            try
            {
                // O SAP Logon (SAPUILandscape.xml) indexa os destinos pelo
                // nome completo exibido no SAP Logon Pad (ex.: "EP0 - ECC
                // Produção"), não pelo código curto de 3 letras (systemid).
                bool conectado = SapRfcService.Instance.OpenConnection(
                    SistemaSelecionado.Exibicao, Client, Usuario, senha, Idioma);

                if (conectado)
                {
                    StatusText =
                        $"Conectado com sucesso.\nUsuário: {SapRfcService.Instance.ConnectedUser}\nSistema: {SapRfcService.Instance.ConnectedSystem}";

                    SapCredentialStore.Save(new SapCredentials
                    {
                        SistemaCodigo = SistemaSelecionado.Codigo,
                        Client = Client,
                        Usuario = Usuario,
                        Senha = senha,
                        Idioma = Idioma,
                    });
                }
            }
            catch (Exception ex)
            {
                StatusText = (automatico ? "Falha ao reconectar automaticamente: " : "Falha na conexão: ") + ex.Message;

                // Credencial salva não serve mais (senha trocada, usuário
                // bloqueado, etc.) -- descarta pra forçar um login manual.
                if (automatico)
                    SapCredentialStore.Clear();
            }
            finally
            {
                Conectando = false;
            }
        }

        private void EsquecerLogin()
        {
            SapRfcService.Instance.CloseConnection();
            SapCredentialStore.Clear();

            Usuario = "";
            Client = "";
            StatusText = "Login salvo esquecido. Informe as credenciais novamente.";

            CredenciaisEsquecidas?.Invoke(this, EventArgs.Empty);
        }
    }
}
