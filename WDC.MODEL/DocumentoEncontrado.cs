using System.Collections.Generic;

namespace WDC.MODEL
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
}
