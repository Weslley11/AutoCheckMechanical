using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Models
{
    public class DrawingDimension
    {
        public DisplayDimension DisplayDimension { get; set; }

        public Dimension Dimension { get; set; }

        public Annotation Annotation { get; set; }

        public string Name { get; set; }

        public int Type { get; set; }

        public bool IsLinear { get; set; }

        public bool IsOrdinate { get; set; }

        public bool IsDiameter { get; set; }

        public bool IsRadius { get; set; }

        public bool IsHoleCallout { get; set; }

        public bool IsBendDimension { get; set; }

        public bool IsDriven { get; set; }
    }
}