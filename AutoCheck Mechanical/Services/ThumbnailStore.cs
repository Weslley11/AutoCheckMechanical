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

                bool ok = doc.SaveBMP(caminhoCompleto, 240, 180);

                return ok && File.Exists(caminhoCompleto) ? caminhoCompleto : null;
            }
            catch (Exception)
            {
                return null;
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
