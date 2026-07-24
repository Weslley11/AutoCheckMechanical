namespace WDC.SERVICES
{
    public static class EDrawingsSettingsStore
    {
        private const string NomeArquivo = "edrawings_exe.txt";

        public static string LoadCaminho()
        {
            return SimpleTextSettingsStore.LoadCaminho(NomeArquivo);
        }

        public static void Save(string caminho)
        {
            SimpleTextSettingsStore.Save(NomeArquivo, caminho);
        }
    }
}
