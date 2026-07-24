using SolidWorks.Interop.sldworks;

namespace WDC.MODEL
{
    public class BendLine
    {
        public SketchSegment Segment { get; set; }

        public string Name { get; set; }

        public double X1 { get; set; }

        public double Y1 { get; set; }

        public double X2 { get; set; }

        public double Y2 { get; set; }

        public bool IsHorizontal { get; set; }

        public bool IsVertical { get; set; }

        public double CenterX { get; set; }

        public double CenterY { get; set; }
    }
}