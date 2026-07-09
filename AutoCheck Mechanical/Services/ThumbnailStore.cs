using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Services
{
    public static class ThumbnailStore
    {
        private static readonly string FolderPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "AutoCheckMechanical",
            "thumbnails");

        public static string Generate(ModelDoc2 doc, string sourceFilePath)
        {
            if (doc == null || string.IsNullOrEmpty(sourceFilePath))
                return null;

            try
            {
                if (!Directory.Exists(FolderPath))
                    Directory.CreateDirectory(FolderPath);

                string caminhoCompleto = Path.Combine(FolderPath, ChaveArquivo(sourceFilePath) + ".bmp");

                doc.ViewZoomtofit2();

                int largura, altura;
                CalcularDimensoes(doc, out largura, out altura);

                bool ok = doc.SaveBMP(caminhoCompleto, largura, altura);

                return ok && File.Exists(caminhoCompleto) ? caminhoCompleto : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Usar uma proporção fixa (ex.: 240x180) diferente da proporção real
        // da folha faz o SaveBMP cortar parte do desenho, já que o
        // ViewZoomtofit2 enquadra o conteúdo na proporção da folha atual, não
        // na proporção do bitmap de saída. Por isso a proporção é calculada
        // a partir do tamanho real da folha. O tamanho base também foi
        // aumentado (era 240x180) para a prévia ficar nítida ao ser exibida
        // maior (tooltip / hover).
        private static void CalcularDimensoes(ModelDoc2 doc, out int largura, out int altura)
        {
            const int tamanhoBase = 480;

            double proporcao = 4.0 / 3.0;

            DrawingDoc desenho = doc as DrawingDoc;

            if (desenho != null)
            {
                Sheet folha = desenho.GetCurrentSheet();

                if (folha != null)
                {
                    double larguraFolha = 0;
                    double alturaFolha = 0;

                    folha.GetSize(ref larguraFolha, ref alturaFolha);

                    if (larguraFolha > 0 && alturaFolha > 0)
                        proporcao = larguraFolha / alturaFolha;
                }
            }

            if (proporcao >= 1)
            {
                largura = tamanhoBase;
                altura = (int)Math.Round(tamanhoBase / proporcao);
            }
            else
            {
                altura = tamanhoBase;
                largura = (int)Math.Round(tamanhoBase * proporcao);
            }
        }

        public static void ClearAll()
        {
            try
            {
                if (Directory.Exists(FolderPath))
                    Directory.Delete(FolderPath, true);
            }
            catch (Exception)
            {
            }
        }

        private static string ChaveArquivo(string caminho)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(caminho));

                StringBuilder sb = new StringBuilder();

                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));

                return sb.ToString();
            }
        }
    }
}
