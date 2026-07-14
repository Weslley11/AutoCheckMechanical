using System;
using System.Threading.Tasks;
using SAP.Middleware.Connector;

namespace WDC.SERVICES
{
    // Conexão RFC/BAPI via SAP .NET Connector (NCo), no mesmo padrão real do
    // WBC (SapConnectionInterface.cs, fornecido como referência direta):
    // registra o destino configurado no SAP Logon local (SAPUILandscape.xml)
    // com as credenciais informadas e valida com um Ping (GetDestination
    // sozinho só monta a config, não garante que o logon funciona).
    //
    // Diferença em relação ao WBC: quando o destino não é encontrado no SAP
    // Logon local, o WBC copia automaticamente um saplogon.ini/
    // SAPUILandscape.xml pré-configurados de uma pasta de rede interna da
    // WEG (Constants.AppRequisitesFilesFolder) e tenta de novo. O AutoCheck
    // Mechanical não tem essa pasta equivalente registrada, então nesse caso
    // só orienta a configurar o destino manualmente no SAP Logon.
    public class SapRfcService
    {
        private static SapRfcService _instance;

        public static SapRfcService Instance => _instance ?? (_instance = new SapRfcService());

        private readonly SapLogonIniConfiguration _sapLogonIni = SapLogonIniConfiguration.Create();

        private SapRfcService()
        {
        }

        internal RfcDestination RfcConnection { get; private set; }

        public bool IsSapConnected => RfcConnection != default(RfcDestination);
        public string ConnectedUser => IsSapConnected ? RfcConnection.User : string.Empty;
        public string ConnectedSystem => IsSapConnected ? RfcConnection.SystemID : string.Empty;

        public bool OpenConnection(string systemName, string client, string username, string password, string language)
        {
            bool result = false;

            try
            {
                AddConfiguration(systemName, client, username, password, language);

                RfcDestination rfcDestination = RfcDestinationManager.GetDestination(systemName);

                Task errorTest = Task.Run(() => rfcDestination.Ping());

                if (!errorTest.Wait(TimeSpan.FromSeconds(15)))
                {
                    throw new Exception("Tempo esgotado ao conectar no SAP.");
                }

                result = true;
                RfcConnection = rfcDestination;
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is RfcLogonException)
                {
                    throw ex.InnerException;
                }

                string message = string.Empty;

                foreach (Exception exception in ex.InnerExceptions)
                {
                    message = message + exception.Message + "\n";
                }

                throw new Exception(message);
            }

            return result;
        }

        public void CloseConnection()
        {
            RfcConnection = default(RfcDestination);
        }

        private void AddConfiguration(string systemName, string client, string username, string password, string language)
        {
            RfcDestinationManager.UnregisterDestinationConfiguration(_sapLogonIni);
            RfcDestinationManager.RegisterDestinationConfiguration(_sapLogonIni);

            RfcConfigParameters param = _sapLogonIni.GetParameters(systemName);

            if (param == null)
            {
                throw new InvalidOperationException(
                    $"Sistema \"{systemName}\" não encontrado no SAP Logon local (SAPUILandscape.xml). " +
                    "Configure o destino no SAP Logon antes de testar a conexão.");
            }

            param.Remove(RfcConfigParameters.Client);
            param.Remove(RfcConfigParameters.User);
            param.Remove(RfcConfigParameters.Password);
            param.Remove(RfcConfigParameters.Language);
            param.Remove(RfcConfigParameters.UseSAPGui);

            param.Add(RfcConfigParameters.Client, client);
            param.Add(RfcConfigParameters.User, username);
            param.Add(RfcConfigParameters.Password, password);
            param.Add(RfcConfigParameters.Language, language);
            param.Add(RfcConfigParameters.UseSAPGui, "1");
        }
    }
}
