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
        // (BuscarDocumentosPorEcmCommand, Web Service ITF_O_S_DOCUMENT_OUTPUT)
        // -- nesse caso não há arquivo local baixado, então FilePath é um
        // identificador sintético (não um caminho real) e ThumbnailPath fica
        // vazio.
        public string DocumentoNumero { get; set; }
        public string DocumentoTipo { get; set; }
        public string DocumentoParte { get; set; }
        public string DocumentoVersao { get; set; }
        public string DocumentoDescricao { get; set; }
    }
}
