namespace WDC.EDRAWINGS
{
    public class EDrawingsMassResult
    {
        public bool Sucesso { get; set; }
        public string Erro { get; set; }
        public double MassaKg { get; set; }
        public int QuantidadeFolhas { get; set; }
        public int QuantidadeCamadas { get; set; }
        public long TempoDecorridoMs { get; set; }
    }
}
