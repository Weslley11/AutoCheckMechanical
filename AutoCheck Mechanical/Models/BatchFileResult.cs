using System.Collections.Generic;

namespace AutoCheckMechanical.Models
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
        // Enquanto o documento não foi checado ainda (RunCheckDrawing ainda
        // não rodou pra ele), FilePath é DocumentoCaminhoOriginal (o "Original"
        // do DMS) quando existe, ou um identificador sintético ("SAP:...")
        // quando não há original vinculado -- nos dois casos ThumbnailPath
        // fica vazio até o check rodar de verdade.
        public string DocumentoNumero { get; set; }
        public string DocumentoTipo { get; set; }
        public string DocumentoParte { get; set; }
        public string DocumentoVersao { get; set; }
        public string DocumentoDescricao { get; set; }
        public string DocumentoCaminhoOriginal { get; set; }
        public bool DocumentoTemPdf { get; set; }

        // URL de download HTTP do original SWD (só existe quando o SAP
        // devolve uma) -- quando preenchida, BaixarDocumentos baixa por
        // aqui em vez de precisar do SAP GUI Scripting.
        public string DocumentoUrlOriginal { get; set; }

        // ECM usada na busca que trouxe esse documento -- usada por
        // BaixarDocumentos pra criar a subpasta de destino.
        public string DocumentoEcm { get; set; }
    }
}
