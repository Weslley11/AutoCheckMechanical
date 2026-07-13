using System;
using System.Linq;
using System.Threading.Tasks;
using SAP.Middleware.Connector;

namespace AutoCheckMechanical.Services
{
    // Integração SAP via RFC/BAPI usando o SAP .NET Connector (NCo), no mesmo
    // padrão usado pelo WBC (SapConnectionInterface): registra as credenciais
    // no destino configurado no SAP Logon local (SAPUILandscape.xml) e valida
    // a conexão com um Ping. Por enquanto só testa a conexão -- é a base para
    // as chamadas de RFC/BAPI que virão depois.
    public class SapRfcService
    {
        private static SapRfcService _instance;

        public static SapRfcService Instance => _instance ?? (_instance = new SapRfcService());

        private readonly SapLogonIniConfiguration _sapLogonIni = SapLogonIniConfiguration.Create();

        private SapRfcService()
        {
        }

        public RfcDestination Conexao { get; private set; }

        public bool Conectado => Conexao != null;
        public string UsuarioConectado => Conectado ? Conexao.User : string.Empty;
        public string SistemaConectado => Conectado ? Conexao.SystemID : string.Empty;

        public void TestarConexao(string sistema, string client, string usuario, string senha, string idioma)
        {
            RfcDestinationManager.UnregisterDestinationConfiguration(_sapLogonIni);
            RfcDestinationManager.RegisterDestinationConfiguration(_sapLogonIni);

            RfcConfigParameters parametros = _sapLogonIni.GetParameters(sistema);

            if (parametros == null)
            {
                throw new InvalidOperationException(
                    $"Sistema \"{sistema}\" não encontrado no SAP Logon local (SAPUILandscape.xml). " +
                    "Configure o destino no SAP Logon antes de testar a conexão.");
            }

            parametros.Remove(RfcConfigParameters.Client);
            parametros.Remove(RfcConfigParameters.User);
            parametros.Remove(RfcConfigParameters.Password);
            parametros.Remove(RfcConfigParameters.Language);
            parametros.Remove(RfcConfigParameters.UseSAPGui);

            parametros.Add(RfcConfigParameters.Client, client);
            parametros.Add(RfcConfigParameters.User, usuario);
            parametros.Add(RfcConfigParameters.Password, senha);
            parametros.Add(RfcConfigParameters.Language, idioma);
            parametros.Add(RfcConfigParameters.UseSAPGui, "1");

            try
            {
                RfcDestination destino = RfcDestinationManager.GetDestination(sistema);

                Task pingTask = Task.Run(() => destino.Ping());

                if (!pingTask.Wait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("Tempo esgotado ao conectar no SAP.");

                Conexao = destino;
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is RfcLogonException)
                    throw ex.InnerException;

                throw new InvalidOperationException(string.Join("\n", ex.InnerExceptions.Select(e => e.Message)));
            }
        }

        public void Desconectar()
        {
            Conexao = null;
        }
    }
}
