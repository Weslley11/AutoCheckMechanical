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

        // Documento "pai" (montagem/peça que este desenho representa),
        // quando o SAP devolve o campo SuperiorDocument -- diagnóstico:
        // Estrutura veio vazia mesmo em desenhos confirmados como montagem
        // com Lista Técnica real no SAP, então isso pode ser um caminho
        // alternativo pra achar a estrutura de verdade (ainda não usado
        // como fallback, só logado pra confirmar se o campo vem preenchido).
        public EstruturaItem DocumentoSuperior { get; set; }

        // Materiais (peça/montagem no SAP, fora do DMS) vinculados a este
        // documento via ObjectLinks.MasterMaterialList -- diagnóstico: como
        // Estrutura/DocumentoSuperior vieram vazios mesmo com Lista Técnica
        // real confirmada, a hipótese é que essa Lista Técnica é dado de
        // Materiais (SAP CS03/BOM), não de Gestão de Documentos, e o elo
        // entre o desenho e o material é esse link, não DocumentStructureList.
        // Só logado por enquanto -- ainda não existe no app uma interface
        // SAP de explosão de lista técnica pra usar esse número de material.
        public List<string> MateriaisVinculados { get; } = new List<string>();

        // Componentes (montagens SWA / peças SWP) encontrados na MESMA busca
        // por ECM que trouxe este documento -- confirmado por log real: a
        // busca por ChangeNumberList devolve o SWD, a(s) SWA e a(s) SWP como
        // DIRs distintos vinculados à mesma ECM (não uma lista aninhada
        // dentro do DIR do SWD), então não precisa de uma segunda chamada
        // (BuscarPorChaves/ResolverEstruturaCompleta) pra ter os dados
        // completos (URL etc.) desses componentes -- já vêm prontos aqui.
        // Compartilhado entre todos os SWD da mesma ECM (não é possível
        // saber com certeza qual SWA/SWP pertence a qual SWD só pelo
        // DocumentNumber/ChangeNumber), então o download é melhor-esforço:
        // baixa todos pra pasta da ECM, e o SolidWorks resolve por nome de
        // arquivo o que for referenciado de verdade.
        public List<DocumentoEncontrado> ComponentesEcm { get; } = new List<DocumentoEncontrado>();

        // Diagnóstico temporário -- linha por Original, com os campos
        // brutos que o SAP devolveu, pra ajudar a descobrir por que
        // CaminhosOriginais está vindo vazio em algum caso real.
        public List<string> OriginaisDebug { get; } = new List<string>();
    }
}
