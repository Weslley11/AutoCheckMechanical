using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

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

    // Base visual da tela de conexão com o SAP -- só o formulário (System/
    // Client/User/Password/Language) e o botão LOGIN, sem nenhuma chamada
    // real ao SAP ainda. A abordagem de conexão (RFC/NCo, SOAP, SAP GUI
    // Scripting) ainda não foi decidida; as duas tentativas anteriores
    // esbarraram em bloqueios de infraestrutura da WEG (DLL x86 do SAP NCo
    // não disponível, porta do Web Service SOAP bloqueada na rede) que
    // precisam ser resolvidos com o time de Basis/rede antes de continuar.
    public class SapConnectionViewModel : BaseViewModel
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

        public ICommand TestConnectionCommand => new DelegateCommand(_ => TestarConexao());

        public SapConnectionViewModel(Func<string> obterSenha)
        {
            _obterSenha = obterSenha;
            SistemaSelecionado = SistemasDisponiveis[4]; // EP0 - ECC Produção
        }

        // Ainda não faz nenhuma chamada real ao SAP -- só valida que os
        // campos foram preenchidos. A conexão de verdade entra aqui quando a
        // abordagem for decidida (RFC/NCo, SOAP ou SAP GUI Scripting).
        private void TestarConexao()
        {
            if (SistemaSelecionado == null || string.IsNullOrWhiteSpace(Usuario))
            {
                StatusText = "Preencha ao menos System e User.";
                return;
            }

            string senha = _obterSenha();

            if (string.IsNullOrWhiteSpace(senha))
            {
                StatusText = "Preencha a senha.";
                return;
            }

            StatusText = "Conexão ainda não implementada -- esta tela é só a base visual.";
        }
    }
}
