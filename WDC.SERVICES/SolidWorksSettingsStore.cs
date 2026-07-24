namespace WDC.SERVICES
{
    public static class SolidWorksSettingsStore
    {
        private const string NomeArquivo = "sldworks_exe.txt";

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
