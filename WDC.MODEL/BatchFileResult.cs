using System.Collections.Generic;

namespace WDC.MODEL
{
    public class BatchFileResult
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public bool OpenFailed { get; set; }
        public string OpenError { get; set; }
        public int SheetCount { get; set; }
        public string ThumbnailPath { get; set; }
        public List<CheckResult> Results { get; set; } = new List<CheckResult>();

        // Preenchidos só nas linhas vindas da busca de documentos por ECM
        // (BuscarDocumentosPorEcmCommand, Web Service ITF_O_S_DOCUMENT_OUTPUT).
        // Enquanto o documento não foi baixado de verdade, FilePath é um
        // identificador sintético ("SAP:...") -- nunca o "Original" bruto do
        // DMS, que não é um caminho de arquivo de verdade (ver comentário em
        // DocumentSearchService.Buscar). ThumbnailPath fica vazio até o
        // check rodar de verdade.
        public string DocumentoNumero { get; set; }
        public string DocumentoTipo { get; set; }
        public string DocumentoParte { get; set; }
        public string DocumentoVersao { get; set; }
        public string DocumentoDescricao { get; set; }
        public string DocumentoCaminhoOriginal { get; set; }
        public bool DocumentoTemPdf { get; set; }

        // URL de download HTTP do original nativo -- SWD/SWA/SWP, o que
        // existir (só existe quando o SAP devolve uma) -- quando
        // preenchida, BaixarDocumentos baixa por aqui em vez de precisar do
        // SAP GUI Scripting.
        public string DocumentoUrlOriginal { get; set; }

        // URL de download HTTP do PDF do documento (exportação separada do
        // nativo CAD), quando existe.
        public string DocumentoUrlOriginalPdf { get; set; }

        // ECM usada na busca que trouxe esse documento -- usada por
        // BaixarDocumentos pra criar a subpasta de destino.
        public string DocumentoEcm { get; set; }

        // Componentes (montagens/peças) que este documento referencia
        // diretamente (1 nível), vindos de DocumentoEncontrado.Estrutura na
        // busca por ECM. Usado por BaixarOriginaisSwd pra também baixar a
        // árvore de componentes inteira (via
        // DocumentSearchService.ResolverEstruturaCompleta) pra dentro da
        // mesma pasta do arquivo principal -- sem os componentes no disco,
        // o SolidWorks não acha as referências e mostra tudo suprimido.
        public List<EstruturaItem> DocumentoEstruturaRaizes { get; set; } = new List<EstruturaItem>();
    }
}
