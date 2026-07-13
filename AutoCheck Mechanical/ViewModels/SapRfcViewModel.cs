using System;
using System.Collections.Generic;
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
    //
    // Igual o WBC: depois do primeiro login bem-sucedido, as credenciais
    // ficam salvas (criptografadas) e as próximas aberturas reconectam
    // sozinhas, sem precisar digitar usuário/senha de novo -- só volta a
    // pedir login se a reconexão automática falhar ou se o usuário usar
    // "Esquecer login salvo".
    public class SapRfcViewModel : BaseViewModel
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

        private bool _testando;
        public bool Testando
        {
            get { return _testando; }
            set { _testando = value; OnPropertyChanged(); }
        }

        private string _ecmBusca = "";
        public string EcmBusca
        {
            get { return _ecmBusca; }
            set { _ecmBusca = value; OnPropertyChanged(); }
        }

        private string _statusBusca = "";
        public string StatusBusca
        {
            get { return _statusBusca; }
            set { _statusBusca = value; OnPropertyChanged(); }
        }

        private bool _buscando;
        public bool Buscando
        {
            get { return _buscando; }
            set { _buscando = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DocumentoEncontrado> ResultadosBusca { get; } = new ObservableCollection<DocumentoEncontrado>();

        public ICommand TestConnectionCommand => new DelegateCommand(_ => TestarConexao(false), () => !Testando);

        public ICommand EsquecerLoginCommand => new DelegateCommand(_ => EsquecerLogin());

        public ICommand BuscarDocumentosCommand => new DelegateCommand(_ => BuscarDocumentos(), () => !Buscando);

        public SapRfcViewModel(Func<string> obterSenha)
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
            TestarConexao(automatico: true);
        }

        private void TestarConexao(bool automatico)
        {
            if (SistemaSelecionado == null || string.IsNullOrWhiteSpace(Usuario))
            {
                if (!automatico)
                    StatusText = "Preencha ao menos System e User.";

                return;
            }

            Testando = true;
            StatusText = automatico ? "Reconectando automaticamente..." : "Conectando...";

            try
            {
                string senha = _obterSenha();

                // O SAP Logon (SAPUILandscape.xml) indexa os destinos pelo nome
                // completo exibido no SAP Logon Pad (ex.: "EP0 - ECC Produção"),
                // não pelo código curto de 3 letras (systemid) -- por isso usamos
                // Exibicao, não Codigo, na busca do destino.
                SapRfcService.Instance.TestarConexao(SistemaSelecionado.Exibicao, Client, Usuario, senha, Idioma);

                StatusText =
                    $"Conectado com sucesso.\nUsuário: {SapRfcService.Instance.UsuarioConectado}\nSistema: {SapRfcService.Instance.SistemaConectado}";

                SapCredentialStore.Save(new SapCredentials
                {
                    SistemaCodigo = SistemaSelecionado.Codigo,
                    Client = Client,
                    Usuario = Usuario,
                    Senha = senha,
                    Idioma = Idioma,
                });
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
                Testando = false;
            }
        }

        // Busca documentos vinculados à ECM informada, via o Web Service
        // ITF_O_S_DOCUMENT_OUTPUT (SOA, código 634-049) -- não usa a conexão
        // RFC acima, é um mecanismo de integração SAP separado (SOAP sobre o
        // PI da SAP), mas reaproveita o Usuário/Senha já digitados aqui.
        private void BuscarDocumentos()
        {
            if (string.IsNullOrWhiteSpace(EcmBusca) || string.IsNullOrWhiteSpace(Usuario))
            {
                StatusBusca = "Preencha ao menos User e a ECM.";
                return;
            }

            Buscando = true;
            StatusBusca = "Buscando...";
            ResultadosBusca.Clear();

            try
            {
                string senha = _obterSenha();

                List<DocumentoEncontrado> documentos = DocumentSearchService.BuscarPorEcm(EcmBusca, Usuario, senha);

                foreach (DocumentoEncontrado documento in documentos)
                    ResultadosBusca.Add(documento);

                StatusBusca = documentos.Count == 0
                    ? "Nenhum documento encontrado para essa ECM."
                    : $"{documentos.Count} documento(s) encontrado(s).";
            }
            catch (Exception ex)
            {
                StatusBusca = "Falha na busca: " + ex.Message;
            }
            finally
            {
                Buscando = false;
            }
        }

        private void EsquecerLogin()
        {
            SapRfcService.Instance.Desconectar();
            SapCredentialStore.Clear();

            Usuario = "";
            Client = "";
            StatusText = "Login salvo esquecido. Informe as credenciais novamente.";

            CredenciaisEsquecidas?.Invoke(this, EventArgs.Empty);
        }
    }
}
