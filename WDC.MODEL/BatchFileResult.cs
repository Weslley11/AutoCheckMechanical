using System.Collections.Generic;

namespace WDC.MODEL
{
    public class BatchFileResult
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public bool OpenFailed { get; set; }
        public string OpenError { get; set; }

        // Quantas vezes seguidas esse documento falhou ao abrir (RPC_E_SERVERFAULT
        // do SolidWorks e afins são comuns e às vezes transitórios -- ver
        // BatchCheckRunner.RunSingleFile) -- carregado adiante por
        // MainViewModel.UpsertBatchResult a cada nova tentativa, zerado
        // assim que o documento abre com sucesso de novo. Ajuda a distinguir
        // "travado há N tentativas" de "ainda nem tentou".
        public int TentativasAbertura { get; set; }
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

        // Nome de arquivo (só o nome, não caminho completo) que o SAP
        // devolveu na metadata do Original nativo -- ex.:
        // "10014227142SWD000.SLDDRW". Usado como nomenclatura de verdade ao
        // baixar (CaminhoLocalEsperado), em vez do nome sintético "{numero}_
        // {versão}.ext" que este app inventa. Null quando o SAP não devolve
        // nenhum Path pra esse documento -- nesse caso cai no nome
        // sintético mesmo.
        public string DocumentoNomeArquivoOriginal { get; set; }

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

        // Componentes (SWA/SWP) já resolvidos por completo (URL, PDF etc.)
        // na própria busca por ECM (DocumentoEncontrado.ComponentesEcm) --
        // ao contrário de DocumentoEstruturaRaizes (só chaves, precisa de
        // uma segunda chamada SOAP pra virar arquivo baixável), esses já
        // podem ser baixados direto, sem round-trip adicional no SAP.
        public List<DocumentoEncontrado> DocumentoComponentesDiretos { get; set; } = new List<DocumentoEncontrado>();
    }
}
