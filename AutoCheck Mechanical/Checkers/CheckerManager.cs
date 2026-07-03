using System;
using System.Collections.Generic;
using AutoCheckMechanical.Checkers;
using AutoCheckMechanical.Interfaces;
using AutoCheckMechanical.Services;

namespace AutoCheckMechanical.Core
{
    public static class CheckerManager
    {
        // Checkers disponíveis no app. Para desativar um checker por padrão
        // (sem removê-lo do código), basta comentar a linha correspondente aqui.
        private static readonly List<Func<IChecker>> Factories = new List<Func<IChecker>>
        {
            () => new FlatPatternChecker(),
            () => new LayerChecker(),
            () => new ScaleChecker(),
            () => new TitleBlockChecker(),

            // desativado a pedido: verificação de cota faltando na planificada
            // () => new DimensionChecker(),

            // desativado a pedido: verificação de balões não é necessária por enquanto
            // () => new BalloonChecker(),

            // próximos checkers
            // () => new NoteChecker(),
            // () => new WeldChecker(),
            // () => new GDTChecker(),
            // () => new HoleCalloutChecker(),
        };

        public static List<string> GetAllCheckerNames()
        {
            List<string> nomes = new List<string>();

            foreach (Func<IChecker> factory in Factories)
                nomes.Add(factory().Name);

            return nomes;
        }

        public static void Register(CheckEngine engine, ISet<string> checkersDesativados = null)
        {
            foreach (Func<IChecker> factory in Factories)
            {
                IChecker checker = factory();

                if (checkersDesativados != null && checkersDesativados.Contains(checker.Name))
                    continue;

                engine.Register(checker);
            }
        }
    }
}
