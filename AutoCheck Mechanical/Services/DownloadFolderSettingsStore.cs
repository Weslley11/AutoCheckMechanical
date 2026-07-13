using System;
using System.IO;

namespace AutoCheckMechanical.Services
{
    public static class DownloadFolderSettingsStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoCheckMechanical",
            "pasta_download_documentos.txt");

        public static string LoadCaminho()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;

                string caminho = File.ReadAllText(FilePath).Trim();

                return string.IsNullOrEmpty(caminho) ? null : caminho;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(string caminho)
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(FilePath, caminho ?? "");
            }
            catch (Exception)
            {
            }
        }
    }
}
