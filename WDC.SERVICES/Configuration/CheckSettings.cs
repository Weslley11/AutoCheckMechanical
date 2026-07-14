using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WDC.SERVICES.Configuration
{
    public static class CheckSettings
    {
            public const bool CheckScale = true;
            public const bool CheckLayer = true;
            public const bool CheckFlatPattern = true;
            public const bool CheckHoleCallout = true;
            public const bool CheckBalloons = true;
            public const bool CheckNotes = true;

        // Layers
        public const string FlatPatternLayer = "L2-Planificado";

        // Escalas permitidas
        public static readonly double[] AllowedScales =
        {
            1.0,
            0.5,
            0.25,
            2.0
        };

        // Tolerância geométrica
        public const double PositionTolerance = 0.00001;

    }
}