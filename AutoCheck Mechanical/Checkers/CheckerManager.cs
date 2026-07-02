using AutoCheckMechanical.Checkers;
using AutoCheckMechanical.Services;

namespace AutoCheckMechanical.Core
{
    public static class CheckerManager
    {
        public static void Register(CheckEngine engine)
        {
            engine.Register(new FlatPatternChecker());
            engine.Register(new LayerChecker());
            engine.Register(new ScaleChecker());
            engine.Register(new DimensionChecker());

            // desativado a pedido: verificação de balões não é necessária por enquanto
            // engine.Register(new BalloonChecker());

            // próximos checkers
            // engine.Register(new NoteChecker());
            // engine.Register(new WeldChecker());
            // engine.Register(new GDTChecker());
            // engine.Register(new HoleCalloutChecker());
        }
    }
}