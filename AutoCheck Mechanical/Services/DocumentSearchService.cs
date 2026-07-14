using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using AutoCheckMechanical.Services.DocumentOutput;
using Weg.Iceberg.Infrastructure.Uddi;

namespace AutoCheckMechanical.Services
{
    public class DocumentoEncontrado
    {
        public string DocumentNumber { get; set; }
        public string Type { get; set; }
        public string Part { get; set; }
        public string Version { get; set; }
        public string ChangeNumber { get; set; }
        public string Descricao { get; set; }
        public bool TemPdf { get; set; }
        public List<string> CaminhosOriginais { get; } = new List<string>();

        // URL de download HTTP do original SWD, quando o SAP devolve uma
        // (só existe se Originals.URL=true no request e o SAP realmente
        // gerar uma URL pra essa combinação de documento/storage category).
        public string UrlOriginalSwd { get; set; }

        // Diagnóstico temporário -- linha por Original, com os campos
        // brutos que o SAP devolveu, pra ajudar a descobrir por que
        // CaminhosOriginais está vindo vazio em algum caso real.
        public List<string> OriginaisDebug { get; } = new List<string>();
    }

    // Busca documentos do DMS vinculados a uma ECM, via o Web Service SOA da
    // WEG ITF_O_S_DOCUMENT_OUTPUT (código de registro 634-049) -- mesmo
    // padrão real usado pelo WBC (MaterialService.cs) e pelo WAU Factory
    // Viewer (DocumentOutput.cs), fornecidos como referência direta:
    // SoapClientFactory().Create(...) + Wau.Util.Services.SapServices.
    // GetServiceCredential(). Os tipos de request/response (pasta
    // DocumentOutput\) são o Reference.cs REAL gerado pelo wsdl.exe no WFV,
    // não uma reconstrução à mão.
    //
    // Duas ressalvas conhecidas, ainda não confirmadas:
    // 1) O endereço resolvido pelo SoapClientFactory pra esse código
    //    (634-049) é, no WFV, o mesmo brjgs916:50000 que já tentamos direto
    //    e foi recusado pela rede -- é bem provável que esse bloqueio
    //    continue acontecendo até o time de infra da WEG liberar o acesso.
    // 2) GetServiceCredential() retorna uma credencial de serviço
    //    registrada especificamente pro WBC/WFV -- não sabemos se o
    //    AutoCheck Mechanical é reconhecido como app autorizado por ela.
    public static class DocumentSearchService
    {
        // retornarUltimaVersao: false = comportamento padrão do DMS (última
        // versão LIBERADA); true = ReturnCurrentVersion=true (última versão
        // de verdade, mesmo que ainda não liberada). Essa leitura do campo
        // ainda não foi confirmada contra um teste real -- se o resultado
        // não bater com o que aparece no SAP GUI (CV04N), precisa ajustar.
        public static List<DocumentoEncontrado> BuscarPorEcm(string ecm, string usuario, bool retornarUltimaVersao)
        {
            DTP_DOCUMENT_OUTPUT request = new DTP_DOCUMENT_OUTPUT
            {
                language = "PT",
                UserCode = usuario,
                DMS = new DTP_DOCUMENT_OUTPUTDMS
                {
                    // CheckIn precisa ser true pra o SAP devolver os
                    // "Originals" -- confirmado (a busca real já mostrou os
                    // dois originais, SWD e PDF, com Path preenchido).
                    //
                    // Só que o Path do original SWD ("C:\SAP_SW\...") é
                    // exatamente a mesma convenção da pasta local que o
                    // fluxo antigo via macro Excel usa como destino de
                    // download -- não um caminho de rede copiável por SOAP.
                    // URL=true + Path=<pasta de interface> é o mecanismo
                    // real que o WAU Factory Viewer usa pra baixar de
                    // verdade (DocumentOutput.cs/GetDocumentInfoAsync): o
                    // SAP publica uma cópia numa pasta de interface (ALE)
                    // e devolve uma URL HTTP de download pra ela. O valor
                    // do Path usado pelo WFV ("/interfaces/EP0/out/
                    // WAU_ENG/WFV/") é específico do WFV -- aqui usamos o
                    // mesmo padrão com o nome deste app no lugar, mas ainda
                    // não está confirmado que essa pasta existe/está
                    // configurada pro AutoCheck Mechanical.
                    Originals = new DTP_DOCUMENT_OUTPUTDMSOriginals
                    {
                        CheckIn = true,
                        Path = "/interfaces/EP0/out/WAU_ENG/AutoCheckMechanical/",
                        URL = true,
                        URLSpecified = true,
                    },
                    ReturnClassInfo = false,
                    ReturnCurrentVersion = retornarUltimaVersao,
                    SearchBy = new DTP_DOCUMENT_OUTPUTDMSSearchBy
                    {
                        Header = new DTP_DOCUMENT_OUTPUTDMSSearchByHeader(),
                        Document = new DTP_DOCUMENT_OUTPUTDMSSearchByDocument
                        {
                            DescriptionList = new string[0],
                            ChangeNumberList = new[] { ecm },
                        },
                    },
                },
            };

            DTP_DOCUMENT_OUTPUT_R response;

            try
            {
                ITF_O_S_DOCUMENT_OUTPUTService service =
                    (ITF_O_S_DOCUMENT_OUTPUTService)new SoapClientFactory().Create(typeof(ITF_O_S_DOCUMENT_OUTPUTService), "634-049");

                service.Credentials = Wau.Util.Services.SapServices.GetServiceCredential();

                response = service.ITF_O_S_DOCUMENT_OUTPUT(request);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Erro ao chamar o serviço de busca de documentos: " + DescreverErroCompleto(ex), ex);
            }

            if (response.ErrorList != null && response.ErrorList.Length > 0)
            {
                string mensagens = string.Join(" | ", response.ErrorList.Select(erro =>
                    erro.Description != null && erro.Description.Length > 0 ? erro.Description[0].Value : erro.Code));

                throw new InvalidOperationException("O SAP retornou erro na busca: " + mensagens);
            }

            List<DocumentoEncontrado> resultado = new List<DocumentoEncontrado>();

            if (response.DIRList == null)
                return resultado;

            foreach (DTP_DOCUMENT_OUTPUT_RDIR dir in response.DIRList)
            {
                // Só nos interessam os documentos do tipo SWD (desenho
                // SolidWorks) -- mesmo filtro que o fluxo antigo via macro
                // Excel (ZTPLM025) já aplicava implicitamente ao só baixar
                // .slddrw.
                if (!string.Equals(dir.Type?.Trim(), "SWD", StringComparison.OrdinalIgnoreCase))
                    continue;

                DocumentoEncontrado documento = new DocumentoEncontrado
                {
                    DocumentNumber = dir.DocumentNumber,
                    Type = dir.Type,
                    Part = dir.Part,
                    Version = dir.Version,
                    ChangeNumber = dir.ChangeNumber,
                    Descricao = dir.DescriptionList?.Description?.Value,
                };

                if (dir.Originals != null)
                {
                    documento.CaminhosOriginais.AddRange(dir.Originals.Select(o => o.Path));
                    documento.TemPdf = dir.Originals.Any(EhOriginalPdf);

                    DTP_DOCUMENT_OUTPUT_RDIROriginal originalSwd = dir.Originals.FirstOrDefault(o =>
                        string.Equals(o.ApplicationCode?.Trim(), "SWD", StringComparison.OrdinalIgnoreCase));

                    documento.UrlOriginalSwd = originalSwd?.URL;

                    documento.OriginaisDebug.AddRange(dir.Originals.Select(o =>
                        $"Path=\"{o.Path}\" ApplicationCode=\"{o.ApplicationCode}\" URL=\"{o.URL}\" Code=\"{o.Code}\" StorageCategory=\"{o.StorageCategory}\" CheckedOutUser=\"{o.CheckedOutUser}\""));
                }
                else
                {
                    documento.OriginaisDebug.Add("(Originals veio nulo no response -- o SAP não devolveu nenhum original pra esse documento)");
                }

                resultado.Add(documento);
            }

            return resultado;
        }

        // Baixa o conteúdo de uma URL de original (gerada pelo SAP quando
        // Originals.URL=true) direto pro caminho local -- mesmo mecanismo
        // usado de fato pelo WAU Factory Viewer (DownloadFileFromURL), só
        // que síncrono em vez de async, pra seguir o mesmo padrão do resto
        // deste app (que não usa Task/async em nenhum outro lugar).
        public static void BaixarOriginalPorUrl(string url, string caminhoDestino)
        {
            using (HttpClient client = new HttpClient())
            {
                // ConfigureAwait(false) evita deadlock: sem isso, o
                // continuation tentaria voltar pro SynchronizationContext
                // da UI (Dispatcher), que está bloqueada esperando o
                // GetResult() -- clássico deadlock de async-sobre-síncrono
                // em WPF.
                HttpResponseMessage resposta = client.GetAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();

                resposta.EnsureSuccessStatusCode();

                string contentType = resposta.Content.Headers.ContentType?.MediaType ?? "";

                // O HttpClient aqui não manda nenhum cookie/sessão/login do
                // SAP -- se a URL exigir autenticação que não temos, o
                // servidor pode devolver uma página de login/erro com
                // status 200 (sucesso HTTP) só que o corpo é HTML/texto, não
                // o arquivo de verdade. EnsureSuccessStatusCode() sozinho
                // não pega esse caso -- por isso confere o Content-Type
                // antes de salvar, em vez de gravar a página de erro como
                // se fosse o desenho.
                if (contentType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    contentType.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    contentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    contentType.IndexOf("xml", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string corpo = resposta.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    string amostra = corpo.Length > 300 ? corpo.Substring(0, 300) : corpo;

                    throw new InvalidOperationException(
                        $"A URL devolveu conteúdo do tipo \"{contentType}\" em vez do arquivo binário " +
                        $"(provavelmente precisa de login/sessão SAP que este download direto não tem). " +
                        $"Início do conteúdo: {amostra}");
                }

                using (Stream origem = resposta.Content.ReadAsStreamAsync().ConfigureAwait(false).GetAwaiter().GetResult())
                using (FileStream destino = File.Create(caminhoDestino))
                {
                    origem.CopyTo(destino);
                }
            }

            // O Content-Type pode vir "application/octet-stream" mesmo
            // quando o corpo não é o arquivo de verdade (servidor não
            // marca corretamente) -- confere a assinatura binária real do
            // que foi salvo antes de considerar sucesso, em vez de confiar
            // só no cabeçalho HTTP.
            if (!EhArquivoSolidWorksValido(caminhoDestino, out string motivo))
            {
                File.Delete(caminhoDestino);
                throw new InvalidOperationException("O arquivo baixado " + motivo);
            }
        }

        // Confere se um arquivo é mesmo um documento SolidWorks
        // (SLDDRW/SLDPRT/SLDASM usam OLE Structured Storage, que sempre
        // começa com essa assinatura de 8 bytes), não uma página de
        // erro/login ou um download incompleto/corrompido. Quando
        // inválido, "motivo" traz um dump dos primeiros bytes (hex + ASCII)
        // pra diagnosticar o que veio de verdade.
        public static bool EhArquivoSolidWorksValido(string caminho, out string motivo)
        {
            motivo = null;

            if (!File.Exists(caminho))
            {
                motivo = "não existe.";
                return false;
            }

            long tamanho = new FileInfo(caminho).Length;

            if (tamanho < 4096)
            {
                motivo = $"é pequeno demais ({tamanho} byte(s)) pra ser um desenho de verdade.";
                return false;
            }

            byte[] assinaturaOle = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            byte[] primeirosBytes = new byte[16];
            int lidos;

            using (FileStream stream = File.OpenRead(caminho))
                lidos = stream.Read(primeirosBytes, 0, primeirosBytes.Length);

            bool assinaturaOk = lidos >= assinaturaOle.Length;

            for (int i = 0; assinaturaOk && i < assinaturaOle.Length; i++)
                assinaturaOk = primeirosBytes[i] == assinaturaOle[i];

            if (assinaturaOk)
                return true;

            string hex = BitConverter.ToString(primeirosBytes, 0, lidos).Replace("-", " ");
            string ascii = new string(primeirosBytes.Take(lidos)
                .Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());

            motivo = $"não é um arquivo SolidWorks válido (tamanho {tamanho} byte(s)). " +
                $"Primeiros bytes: {hex} | ASCII: \"{ascii}\"";

            return false;
        }

        // O Web Service não expõe um campo explícito "é PDF" -- inferimos
        // pelo ApplicationCode (quando preenchido) ou pela extensão do
        // Path. Essa leitura ainda não foi confirmada contra dados reais do
        // SAP -- se não bater, precisa ajustar.
        private static bool EhOriginalPdf(DTP_DOCUMENT_OUTPUT_RDIROriginal original)
        {
            if (!string.IsNullOrEmpty(original.ApplicationCode) &&
                original.ApplicationCode.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return !string.IsNullOrEmpty(original.Path) &&
                original.Path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        // "Método X não pode ser refletido" (e outras exceções do
        // System.Web.Services) são wrappers genéricos por cima da
        // InnerException que tem o motivo de verdade.
        public static string DescreverErroCompleto(Exception ex)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            Exception atual = ex;

            while (atual != null)
            {
                if (sb.Length > 0)
                    sb.Append(" -> ");

                sb.Append(atual.GetType().Name).Append(": ").Append(atual.Message);
                atual = atual.InnerException;
            }

            return sb.ToString();
        }
    }
}
