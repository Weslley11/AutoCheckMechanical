namespace WDC.MODEL
{
    // Um nível da estrutura (BOM) de um documento no DMS -- devolvido pelo
    // SAP quando a busca pede DocumentStructureList (ver
    // DocumentSearchService.ResolverEstruturaCompleta). É a chave de um
    // componente referenciado (montagem/peça), não o arquivo em si.
    public class EstruturaItem
    {
        public string Item { get; set; }
        public string DocumentNumber { get; set; }
        public string Type { get; set; }
        public string Part { get; set; }
        public string Version { get; set; }
    }
}
