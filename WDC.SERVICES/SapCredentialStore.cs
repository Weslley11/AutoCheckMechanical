using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace WDC.SERVICES
{
    [DataContract]
    public class SapCredentials
    {
        [DataMember] public string SistemaCodigo { get; set; }
        [DataMember] public string Client { get; set; }
        [DataMember] public string Usuario { get; set; }
        [DataMember] public string Senha { get; set; }
        [DataMember] public string Idioma { get; set; }
    }

    // Guarda as credenciais SAP localmente para reconectar automaticamente
    // nas próximas aberturas da tela -- mesma ideia do wbcli.dat do WBC
    // (CommonVM.SaveTempLogonInfo/GetTempLogonInfo, criptografado com a
    // Cryptography própria do Wau.Util). Aqui usamos o DPAPI do Windows
    // (ProtectedData, DataProtectionScope.CurrentUser) por não termos
    // acesso a essa lib da WEG -- mesma garantia de segurança (só o usuário
    // do Windows que salvou consegue descriptografar).
    public static class SapCredentialStore
    {
        private static readonly string PastaConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WDC");

        private static readonly string CaminhoArquivo = Path.Combine(PastaConfig, "sap_rfc.dat");

        public static SapCredentials Load()
        {
            try
            {
                if (!File.Exists(CaminhoArquivo))
                    return null;

                byte[] protegido = File.ReadAllBytes(CaminhoArquivo);
                byte[] json = ProtectedData.Unprotect(protegido, null, DataProtectionScope.CurrentUser);

                using (MemoryStream stream = new MemoryStream(json))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(SapCredentials));
                    return (SapCredentials)serializer.ReadObject(stream);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(SapCredentials credenciais)
        {
            Directory.CreateDirectory(PastaConfig);

            using (MemoryStream stream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(SapCredentials));
                serializer.WriteObject(stream, credenciais);

                byte[] protegido = ProtectedData.Protect(stream.ToArray(), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(CaminhoArquivo, protegido);
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(CaminhoArquivo))
                    File.Delete(CaminhoArquivo);
            }
            catch (Exception)
            {
            }
        }
    }
}
