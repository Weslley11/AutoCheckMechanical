using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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

                // Ao abrir o arquivo via automação (silencioso), algumas
                // vistas (detalhe, corte etc.) podem não estar totalmente
                // regeneradas ainda -- sem o rebuild, o ViewZoomtofit2 calcula
                // o enquadramento com base num bounding box incompleto e a
                // prévia sai cortada, faltando parte da folha.
                doc.ForceRebuild3(false);
                doc.ViewZoomtofit2();

                // O ViewZoomtofit2 enquadra a folha exatamente no limite da
                // viewport, sem margem. Como o SaveBMP recorta a partir dessa
                // viewport pro tamanho pedido (não faz um novo fit), qualquer
                // pequena diferença de proporção corta as bordas da folha
                // (zonas, bloco de título). Um zoom out extra cria uma
                // margem de segurança pra esse recorte não alcançar o
                // conteúdo.
                doc.ViewZoomout();

                int largura, altura;
                CalcularDimensoes(doc, out largura, out altura);

                bool ok = doc.SaveBMP(caminhoCompleto, largura, altura);

                if (!ok || !File.Exists(caminhoCompleto))
                    return null;

                RecortarMargemBranca(caminhoCompleto);

                return caminhoCompleto;
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
            const int tamanhoBase = 640;

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

        // Em vez de tentar acertar o zoom exato da câmera do SolidWorks (o
        // que deixava uma margem grande e imprevisível em volta da folha --
        // inclusive revelando o fundo escuro da área gráfica fora do papel,
        // dependendo do tema do SolidWorks do usuário), a imagem já salva é
        // recortada automaticamente até os limites reais do PAPEL (branco),
        // com uma folga pequena. Isso garante um enquadramento justo com
        // apenas a folha, independente do que exista fora dela.
        private static void RecortarMargemBranca(string caminhoArquivo)
        {
            try
            {
                using (Bitmap original = new Bitmap(caminhoArquivo))
                {
                    Rectangle limites = CalcularLimitesConteudo(original);

                    if (limites.Width <= 0 || limites.Height <= 0)
                        return;

                    int folga = Math.Max(4, (int)(Math.Max(limites.Width, limites.Height) * 0.03));

                    int x = Math.Max(0, limites.X - folga);
                    int y = Math.Max(0, limites.Y - folga);
                    int largura = Math.Min(original.Width - x, limites.Width + folga * 2);
                    int altura = Math.Min(original.Height - y, limites.Height + folga * 2);

                    if (largura <= 0 || altura <= 0)
                        return;

                    using (Bitmap recortado = original.Clone(
                        new Rectangle(x, y, largura, altura),
                        original.PixelFormat))
                    {
                        recortado.Save(caminhoArquivo, ImageFormat.Bmp);
                    }
                }
            }
            catch (Exception)
            {
                // Se o recorte falhar por qualquer motivo, mantém a imagem
                // original (sem recorte) em vez de perder a prévia.
            }
        }

        private static Rectangle CalcularLimitesConteudo(Bitmap bitmap)
        {
            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            BitmapData dados = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            try
            {
                int stride = dados.Stride;
                int tamanho = stride * bitmap.Height;
                byte[] pixels = new byte[tamanho];

                Marshal.Copy(dados.Scan0, pixels, 0, tamanho);

                int minX = bitmap.Width;
                int minY = bitmap.Height;
                int maxX = -1;
                int maxY = -1;

                for (int y = 0; y < bitmap.Height; y++)
                {
                    int linhaBase = y * stride;

                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int i = linhaBase + x * 3;

                        byte b = pixels[i];
                        byte g = pixels[i + 1];
                        byte r = pixels[i + 2];

                        // Procura os limites do PAPEL (branco), não do
                        // "conteúdo não-branco" -- se a área gráfica do
                        // SolidWorks fora da folha não for branca (ex.: tema
                        // escuro), aquele fundo também conta como "não
                        // branco" e o recorte antigo acabava incluindo o
                        // fundo escuro em vez de só a folha.
                        bool ehBranco = r > 235 && g > 235 && b > 235;

                        if (ehBranco)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                if (maxX < minX || maxY < minY)
                    return Rectangle.Empty;

                return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            }
            finally
            {
                bitmap.UnlockBits(dados);
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
