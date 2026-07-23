using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WDC.SERVICES
{
    // Últimas ECMs buscadas no SAP -- uma por linha, mais recente primeiro,
    // sem duplicata, limitado a um tamanho razoável pro dropdown do campo
    // ECM não crescer sem limite.
    public static class EcmHistoricoStore
    {
        private const int MaximoItens = 15;

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WDC",
            "ecms_recentes.txt");

        public static List<string> Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new List<string>();

                return File.ReadAllLines(FilePath)
                    .Select(linha => linha.Trim())
                    .Where(linha => !string.IsNullOrEmpty(linha))
                    .ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public static List<string> Add(string ecm)
        {
            ecm = ecm?.Trim();

            List<string> atuais = Load();

            if (string.IsNullOrEmpty(ecm))
                return atuais;

            atuais.RemoveAll(x => string.Equals(x, ecm, StringComparison.OrdinalIgnoreCase));
            atuais.Insert(0, ecm);

            if (atuais.Count > MaximoItens)
                atuais = atuais.Take(MaximoItens).ToList();

            try
            {
                string directory = Path.GetDirectoryName(FilePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllLines(FilePath, atuais);
            }
            catch (Exception)
            {
            }

            return atuais;
        }
    }
}
