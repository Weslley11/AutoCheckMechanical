using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Models
{
    public class BendLineInfo
    {
        public SketchSegment Segment { get; set; }

        public string Name { get; set; }

        public bool IsDimensioned { get; set; }

        public bool IsHorizontal { get; set; }

        public bool IsVertical { get; set; }
    }
}