using System;
using System.IO;

namespace WDC.SERVICES
{
    // Implementação compartilhada do padrão "um arquivo texto em %AppData%
    // \WDC\, com um caminho salvo por linha" usado por DownloadFolderSettingsStore,
    // EDrawingsSettingsStore e SolidWorksSettingsStore -- as três eram cópias
    // idênticas desse mesmo código, cada uma só trocando o nome do arquivo.
    internal static class SimpleTextSettingsStore
    {
        public static string LoadCaminho(string nomeArquivo)
        {
            try
            {
                string filePath = CaminhoCompleto(nomeArquivo);

                if (!File.Exists(filePath))
                    return null;

                string caminho = File.ReadAllText(filePath).Trim();

                return string.IsNullOrEmpty(caminho) ? null : caminho;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(string nomeArquivo, string caminho)
        {
            try
            {
                string filePath = CaminhoCompleto(nomeArquivo);
                string directory = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(filePath, caminho ?? "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"SimpleTextSettingsStore.Save({nomeArquivo}): falha ao salvar ({ex.GetType().Name}): {ex.Message}");
            }
        }

        private static string CaminhoCompleto(string nomeArquivo)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WDC",
                nomeArquivo);
        }
    }
}
