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

        // URL de download HTTP do original nativo (SWD/SWA/SWP -- desenho,
        // montagem ou peça, o que existir), quando o SAP devolve uma (só
        // existe se Originals.URL=true no request e o SAP realmente gerar
        // uma URL pra essa combinação de documento/storage category).
        public string UrlOriginalNativo { get; set; }

        // ApplicationCode do original nativo (SWD/SWA/SWP) -- usado pra
        // decidir a extensão do arquivo local (ver
        // DocumentSearchService.ExtensaoParaTipoCad).
        public string TipoOriginalNativo { get; set; }

        // Path bruto do Original nativo, como o SAP devolveu (NÃO é um
        // caminho de arquivo de verdade -- ver o comentário em
        // DocumentSearchService.Buscar). Usado só como melhor palpite pro
        // nome de arquivo original (extensão + nome) ao baixar um componente
        // da estrutura, já que o SolidWorks resolve referência de montagem
        // por nome de arquivo.
        public string CaminhoOriginalNativo { get; set; }

        // URL de download HTTP do original PDF, quando existe (documento
        // exportado em PDF, além do nativo CAD).
        public string UrlOriginalPdf { get; set; }

        // Componentes (montagens/peças) referenciados por este documento --
        // só vem preenchido quando a busca pediu ReturnDocumentStructure=true
        // (ver DocumentSearchService.ResolverEstruturaCompleta). Vazio pra
        // documentos sem estrutura (ex: desenho de peça avulsa).
        public List<EstruturaItem> Estrutura { get; } = new List<EstruturaItem>();

        // Diagnóstico temporário -- linha por Original, com os campos
        // brutos que o SAP devolveu, pra ajudar a descobrir por que
        // CaminhosOriginais está vindo vazio em algum caso real.
        public List<string> OriginaisDebug { get; } = new List<string>();
    }
}
