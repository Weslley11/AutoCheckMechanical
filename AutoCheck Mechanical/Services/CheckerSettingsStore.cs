using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace AutoCheckMechanical.Services
{
    public static class CheckerSettingsStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoCheckMechanical",
            "checks.json");

        public static HashSet<string> LoadDesativados()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new HashSet<string>();

                using (FileStream stream = File.OpenRead(FilePath))
                {
                    DataContractJsonSerializer serializer =
                        new DataContractJsonSerializer(typeof(List<string>));

                    List<string> lista = serializer.ReadObject(stream) as List<string>;

                    return lista != null
                        ? new HashSet<string>(lista, StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>();
                }
            }
            catch (Exception)
            {
                return new HashSet<string>();
            }
        }

        public static void Save(IEnumerable<string> checkersDesativados)
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = File.Create(FilePath))
                {
                    DataContractJsonSerializer serializer =
                        new DataContractJsonSerializer(typeof(List<string>));

                    serializer.WriteObject(stream, new List<string>(checkersDesativados));
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
