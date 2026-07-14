using System;
using System.IO;

namespace WDC.SERVICES
{
    public static class ThemeStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WDC",
            "tema.txt");

        public static bool LoadTemaEscuro()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return true;

                string conteudo = File.ReadAllText(FilePath).Trim();

                return !string.Equals(conteudo, "claro", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static void Save(bool temaEscuro)
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(FilePath, temaEscuro ? "escuro" : "claro");
            }
            catch (Exception)
            {
            }
        }
    }
}
