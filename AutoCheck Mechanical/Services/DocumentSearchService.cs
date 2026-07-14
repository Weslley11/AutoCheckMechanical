using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using AutoCheckMechanical.Services.Document;
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
        // Pasta de interface (ALE) real, confirmada pelo usuário -- mesmo
        // padrão do WAU Factory Viewer (WFV.MODEL.Constants.
        // DocumentOutputRequestPath = "/interfaces/EP0/out/WAU_ENG/WFV/"),
        // só trocando "WFV" por "AutoCheck".
        private const string PastaInterfaceAle = "/interfaces/EP0/out/WAU_ENG/AutoCheck/";

        // Caminho de rede (UNC) equivalente, pela MESMA fórmula real do WFV
        // (WFV.MODEL.Constants.DocumentOutputFolderPath): tudo que vem
        // depois de "/interfaces/" em PastaInterfaceAle, com "/" trocado por
        // "\", dentro do compartilhamento \\BRJGS100\APPS$\SAP\. O usuário
        // confirmou o mapeamento local equivalente (unidade Q:) como
        // Q:\APPS\SAP\EP0\OUT\WAU_ENG\AutoCheck -- usamos o UNC direto (em
        // vez de depender da letra Q: estar mapeada) pra funcionar em
        // qualquer máquina.
        private const string PastaInterfaceLocalUnc = @"\\BRJGS100\APPS$\SAP\EP0\out\WAU_ENG\AutoCheck\";

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
                    // e devolve uma URL HTTP de download pra ela -- mas essa
                    // URL já foi confirmada (ver BaixarOriginalPorUrl) como
                    // devolvendo conteúdo encriptado. Esse valor específico
                    // de Path (mesmo sendo um chute na época) é o que estava
                    // em uso quando a URL foi confirmada saindo de verdade
                    // pro SAP (9 documentos reais, ver histórico) -- trocar
                    // pra PastaInterfaceAle ("/interfaces/EP0/out/WAU_ENG/
                    // AutoCheck/", a pasta real confirmada depois) fez a URL
                    // parar de vir, então voltou pro valor confirmado
                    // empiricamente em vez do "correto" na teoria.
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

        // Chama o serviço ITF_O_S_DOCUMENT (singular, distinto do
        // ITF_O_S_DOCUMENT_OUTPUT usado em BuscarPorEcm) pra um documento
        // específico, pedindo CheckIn=true/Return=true no Original do SWD
        // com Path=PastaInterfaceAle -- isso é o sinal pro SAP publicar uma
        // cópia física do original na pasta de interface. O schema desse
        // serviço não tem campo de conteúdo binário nem URL em Originals.
        // Original (só metadata: Path/Description/Code/StorageCategory/
        // ApplicationCode/CheckIn/Return), então a resposta SOAP em si nunca
        // vira arquivo -- por isso, depois de chamar, procura o arquivo de
        // verdade direto em PastaInterfaceLocalUnc (a mesma pasta de
        // interface, só que pelo caminho de rede) e copia pra
        // caminhoDestino se achar um válido.
        //
        // "diagnostico" sempre traz o que aconteceu (erro do SAP, metadata
        // devolvida, e se achou/não achou o arquivo na pasta de rede) --
        // continua valendo como evidência real mesmo quando falha.
        public static string BaixarOriginalViaItfDocument(
            string documentNumber, string documentType, string documentPart, string documentVersion,
            string usuario, string caminhoDestino, out string diagnostico)
        {
            DTP_DOCUMENT request = new DTP_DOCUMENT
            {
                language = "PT",
                UserCode = usuario,
                DIRList = new[]
                {
                    new DTP_DOCUMENTDIR
                    {
                        DocumentNumber = documentNumber,
                        DocumentType = documentType,
                        DocumentPart = documentPart,
                        DocumentVersion = documentVersion,
                        Originals = new DTP_DOCUMENTDIROriginals
                        {
                            Original = new[]
                            {
                                new DTP_DOCUMENTDIROriginalsOriginal
                                {
                                    // CheckIn=true é o que, em BuscarPorEcm, faz
                                    // o SAP devolver/publicar os Originals de
                                    // verdade -- mandado aqui também, junto do
                                    // Path da pasta de interface, na expectativa
                                    // de que produza o mesmo efeito colateral
                                    // (publicar a cópia física na pasta).
                                    Path = PastaInterfaceAle,
                                    ApplicationCode = "SWD",
                                    CheckIn = true,
                                    Return = true,
                                },
                            },
                        },
                        DescriptionList = new DTP_DOCUMENTDIRDescriptionList
                        {
                            Description = new[]
                            {
                                new DTP_DOCUMENTDIRDescriptionListDescription { language = "PT", Value = string.Empty },
                            },
                        },
                        ReturnClassification = false,
                    },
                },
            };

            DTP_DOCUMENT_R response;
            string urlUsada;

            try
            {
                // Código de registro real no catálogo SOA da WEG pro
                // ITF_O_S_DOCUMENT (confirmado -- irmão do 634-049 da
                // OUTPUT). A tentativa anterior de reaproveitar o host
                // resolvido do 634-049 trocando só o parâmetro Interface= da
                // URL não funcionava -- o PI insistia em mapear pra
                // MTP_DOCUMENT_OUTPUT (canal preso na OUTPUT, independente
                // da URL) -- então precisa mesmo do código de registro
                // próprio dessa interface pro SoapClientFactory resolver o
                // canal certo.
                ITF_O_S_DOCUMENTService service =
                    (ITF_O_S_DOCUMENTService)new SoapClientFactory().Create(typeof(ITF_O_S_DOCUMENTService), "634-048");

                service.Credentials = Wau.Util.Services.SapServices.GetServiceCredential();
                urlUsada = service.Url;

                response = service.ITF_O_S_DOCUMENT(request);
            }
            catch (Exception ex)
            {
                diagnostico = "Erro ao chamar o serviço ITF_O_S_DOCUMENT: " + DescreverErroCompleto(ex);
                return null;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append($"URL usada: \"{urlUsada}\" | ");

            if (response.ErrorList?.Error != null && response.ErrorList.Error.Length > 0)
            {
                foreach (DTP_DOCUMENT_RErrorListError erro in response.ErrorList.Error)
                {
                    string mensagem = erro.Description != null && erro.Description.Length > 0
                        ? erro.Description[0].Value
                        : erro.Code;

                    sb.Append("Erro SAP: ").Append(erro.Code).Append(" - ").Append(mensagem).Append(" | ");
                }
            }

            DTP_DOCUMENT_RDIR dir = response.DIRList != null && response.DIRList.Length > 0 ? response.DIRList[0] : null;

            if (dir == null)
            {
                sb.Append("O SAP não devolveu nenhum DIR pra esse documento.");
            }
            else if (dir.Originals == null || dir.Originals.Length == 0)
            {
                sb.Append("O SAP não devolveu nenhum Original pra esse documento.");
            }
            else
            {
                foreach (DTP_DOCUMENT_RDIROriginal original in dir.Originals)
                {
                    sb.Append($"Path=\"{original.Path}\" Code=\"{original.Code}\" " +
                        $"StorageCategory=\"{original.StorageCategory}\" ApplicationCode=\"{original.ApplicationCode}\" " +
                        $"CheckIn={original.CheckIn} Return={original.Return} | ");
                }
            }

            string arquivoEncontrado = EncontrarArquivoNaPastaInterface(documentNumber);

            if (arquivoEncontrado == null)
            {
                sb.Append($"Nenhum arquivo com \"{documentNumber}\" no nome apareceu em \"{PastaInterfaceLocalUnc}\" depois da chamada.");
                diagnostico = sb.ToString();
                return null;
            }

            if (!EhArquivoSolidWorksValido(arquivoEncontrado, out string motivoInvalido))
            {
                sb.Append($"Achou \"{arquivoEncontrado}\" na pasta de interface, mas {motivoInvalido}");
                diagnostico = sb.ToString();
                return null;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(caminhoDestino));
                File.Copy(arquivoEncontrado, caminhoDestino, true);
            }
            catch (Exception ex)
            {
                sb.Append($"Achou \"{arquivoEncontrado}\" válido na pasta de interface, mas falhou ao copiar pra \"{caminhoDestino}\": {ex.Message}");
                diagnostico = sb.ToString();
                return null;
            }

            sb.Append($"OK -- copiado de \"{arquivoEncontrado}\" pra \"{caminhoDestino}\".");
            diagnostico = sb.ToString();

            return caminhoDestino;
        }

        // A publicação do arquivo na pasta de interface pode não ser
        // instantânea (efeito colateral do lado do SAP, possivelmente
        // assíncrono) -- por isso tenta algumas vezes com um intervalo
        // curto em vez de checar só uma vez logo após a chamada SOAP
        // responder. Casa pelo número do documento aparecer no nome do
        // arquivo (mesma convenção usada em CaminhoLocalEsperado, no
        // MainViewModel) já que não temos confirmação de qual é o nome
        // exato que o SAP usa ao publicar ali.
        private static string EncontrarArquivoNaPastaInterface(string documentNumber)
        {
            const int tentativas = 5;

            for (int tentativa = 0; tentativa < tentativas; tentativa++)
            {
                try
                {
                    if (Directory.Exists(PastaInterfaceLocalUnc))
                    {
                        string encontrado = Directory.EnumerateFiles(PastaInterfaceLocalUnc, "*.SLDDRW", SearchOption.TopDirectoryOnly)
                            .Where(f => Path.GetFileName(f).IndexOf(documentNumber, StringComparison.OrdinalIgnoreCase) >= 0)
                            .OrderByDescending(File.GetLastWriteTimeUtc)
                            .FirstOrDefault();

                        if (encontrado != null)
                            return encontrado;
                    }
                }
                catch
                {
                    // Pasta de rede pode oscilar (indisponível num instante) --
                    // só tenta de novo na próxima volta do loop.
                }

                if (tentativa < tentativas - 1)
                    System.Threading.Thread.Sleep(1000);
            }

            return null;
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

            // SEM validação de assinatura OLE aqui de propósito, a pedido --
            // volta pra fase em que o download em si "dava certo" (o arquivo
            // é salvo, mesmo que o conteúdo esteja encriptado) e só a
            // abertura no SolidWorks falhava depois. EhArquivoSolidWorksValido
            // ainda existe e é usada em MainViewModel.ArquivoBaixadoPareceValido
            // (decide se reusa um arquivo já baixado ou baixa de novo), só não
            // é mais chamada logo após o download pra rejeitar/apagar o
            // arquivo aqui.
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
