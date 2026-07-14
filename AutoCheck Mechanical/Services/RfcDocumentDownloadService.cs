using System;
using System.IO;
using System.Linq;
using SAP.Middleware.Connector;

namespace AutoCheckMechanical.Services
{
    // Baixa o conteúdo binário do original de um documento DMS via RFC puro
    // -- sem SAP GUI (Scripting) e sem URL/HTTP (que só devolve conteúdo
    // encriptado, ver DocumentSearchService.BaixarOriginalPorUrl). Usa a
    // mesma conexão RFC já aberta pela tela de login SAP (SapRfcService).
    //
    // Sequência de 3 BAPIs/função padrão do SAP (não são Z-customizadas),
    // documentada publicamente (SAP Community, "Como fazer download de
    // arquivo do DMS via BAPI", thread original SDN messageID=1802505):
    //
    // 1) BAPI_DOCUMENT_GETDETAIL2 -- lista os originais do documento
    //    (DOCUMENTFILES), pra achar o WSAPPLICATION certo (ex.: "SWD").
    // 2) BAPI_DOCUMENT_CHECKOUTVIEW2 -- "faz o checkout" desse original
    //    específico e devolve o FILE_ID/STOR_CAT usados pra ler o conteúdo.
    //    PF_FTP_DEST="*SAPFTPA*" é obrigatório aqui: sem isso a BAPI espera
    //    um SAP GUI ativo do lado do cliente e falha, já que estamos
    //    chamando via RFC puro (sem GUI nenhuma).
    // 3) SCMS_DOC_READ -- lê o conteúdo binário de verdade do Content
    //    Server, usando STOR_CAT + DOC_ID (=FILE_ID do passo 2).
    //
    // Ainda não testado contra o SAP real -- os nomes de campo batem com a
    // referência (BAPIs padrão, não deveriam variar entre sistemas), mas se
    // algum não bater o jeito de descobrir é o mesmo já usado neste app:
    // rodar, colar a mensagem de erro exata, ajustar.
    public static class RfcDocumentDownloadService
    {
        public static void BaixarOriginal(
            string documentNumber, string documentType, string documentPart, string documentVersion,
            string wsApplication, string caminhoDestino)
        {
            if (!SapRfcService.Instance.IsSapConnected)
            {
                throw new InvalidOperationException(
                    "Não há conexão RFC com o SAP -- conecte primeiro pela tela \"CONEXÃO SAP\".");
            }

            RfcDestination destino = SapRfcService.Instance.RfcConnection;

            // Campo DOCUMENTNUMBER do DMS é de 25 posições, zero-padded à
            // esquerda -- mesmo padrão confirmado no código real do WBC
            // (RfcDocumentGetFunctions.cs).
            string numeroFormatado = documentNumber.PadLeft(25, '0');

            IRfcStructure arquivoOriginal = ObterArquivoOriginal(destino, numeroFormatado, documentType, documentPart, documentVersion, wsApplication);

            IRfcStructure arquivoCheckout = FazerCheckoutView(destino, numeroFormatado, documentType, documentPart, documentVersion, wsApplication);

            string fileId = arquivoCheckout.GetString("FILE_ID");
            string storageCategory = arquivoCheckout.GetString("STOR_CAT");

            LerConteudoBinario(destino, storageCategory, fileId, caminhoDestino);
        }

        private static IRfcStructure ObterArquivoOriginal(
            RfcDestination destino, string numeroFormatado, string documentType, string documentPart, string documentVersion, string wsApplication)
        {
            IRfcFunction getDetail = destino.Repository.CreateFunction("BAPI_DOCUMENT_GETDETAIL2");
            getDetail.SetValue("DOCUMENTTYPE", documentType);
            getDetail.SetValue("DOCUMENTNUMBER", numeroFormatado);
            getDetail.SetValue("DOCUMENTPART", documentPart);
            getDetail.SetValue("DOCUMENTVERSION", documentVersion);
            getDetail.SetValue("GETCOMPONENTS", "X");
            getDetail.SetValue("GETDOCDESCRIPTIONS", "X");
            getDetail.SetValue("GETDOCFILES", "X");
            getDetail.SetValue("GETCLASSIFICATION", "X");

            getDetail.Invoke(destino);

            LancarSeErro(getDetail.GetStructure("RETURN"), "BAPI_DOCUMENT_GETDETAIL2");

            IRfcTable documentFiles = getDetail.GetTable("DOCUMENTFILES");

            IRfcStructure arquivo = documentFiles.FirstOrDefault(f =>
                string.Equals(f.GetString("WSAPPLICATION")?.Trim(), wsApplication, StringComparison.OrdinalIgnoreCase));

            if (arquivo == null)
            {
                throw new InvalidOperationException(
                    $"Nenhum original do tipo \"{wsApplication}\" encontrado pra esse documento (BAPI_DOCUMENT_GETDETAIL2 devolveu {documentFiles.RowCount} arquivo(s) no total).");
            }

            return arquivo;
        }

        private static IRfcStructure FazerCheckoutView(
            RfcDestination destino, string numeroFormatado, string documentType, string documentPart, string documentVersion, string wsApplication)
        {
            IRfcFunction checkout = destino.Repository.CreateFunction("BAPI_DOCUMENT_CHECKOUTVIEW2");
            checkout.SetValue("DOCUMENTTYPE", documentType);
            checkout.SetValue("DOCUMENTNUMBER", numeroFormatado);
            checkout.SetValue("DOCUMENTPART", documentPart);
            checkout.SetValue("DOCUMENTVERSION", documentVersion);
            checkout.SetValue("GETSTRUCTURE", "1");
            checkout.SetValue("GETHEADER", "X");

            // Sem isso, a BAPI espera um SAP GUI ativo do lado do cliente
            // (é feita pra ser chamada de dentro do SAP GUI) e falha com
            // "SAP GUI não encontrado" quando chamada via RFC puro.
            checkout.SetValue("PF_FTP_DEST", "*SAPFTPA*");

            IRfcStructure documentFile = checkout.GetStructure("DOCUMENTFILE");
            documentFile.SetValue("WSAPPLICATION", wsApplication);

            checkout.Invoke(destino);

            LancarSeErro(checkout.GetStructure("RETURN"), "BAPI_DOCUMENT_CHECKOUTVIEW2");

            IRfcTable documentFiles = checkout.GetTable("DOCUMENTFILES");

            if (documentFiles.RowCount == 0)
            {
                throw new InvalidOperationException("BAPI_DOCUMENT_CHECKOUTVIEW2 não devolveu nenhum arquivo pra leitura.");
            }

            return documentFiles.First();
        }

        private static void LerConteudoBinario(RfcDestination destino, string storageCategory, string fileId, string caminhoDestino)
        {
            IRfcFunction docRead = destino.Repository.CreateFunction("SCMS_DOC_READ");
            docRead.SetValue("STOR_CAT", storageCategory);
            docRead.SetValue("DOC_ID", fileId);

            docRead.Invoke(destino);

            IRfcTable contentBin = docRead.GetTable("CONTENT_BIN");

            using (MemoryStream memoria = new MemoryStream())
            {
                foreach (IRfcStructure linha in contentBin)
                {
                    byte[] bytesLinha = linha.GetByteArray("LINE");
                    memoria.Write(bytesLinha, 0, bytesLinha.Length);
                }

                File.WriteAllBytes(caminhoDestino, memoria.ToArray());
            }
        }

        // BAPIs (diferente de function modules "puros") não lançam exceção
        // em erro de negócio -- devolvem uma estrutura RETURN (BAPIRET2)
        // com TYPE="E"/"A" indicando falha, que precisa ser conferida à mão.
        private static void LancarSeErro(IRfcStructure retorno, string nomeBapi)
        {
            string tipo = retorno.GetString("TYPE");

            if (tipo == "E" || tipo == "A")
            {
                string mensagem = retorno.GetString("MESSAGE");
                throw new InvalidOperationException($"{nomeBapi} retornou erro: {mensagem}");
            }
        }
    }
}
