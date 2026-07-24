namespace WDC.SERVICES
{
    public static class DownloadFolderSettingsStore
    {
        private const string NomeArquivo = "pasta_download_documentos.txt";

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
