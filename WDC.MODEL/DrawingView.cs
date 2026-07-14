using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace WDC.MODEL
{
    public class DrawingView
    {
        public View View { get; set; }

        public string Name { get; set; }

        public string Configuration { get; set; }

        public string Layer { get; set; }

        public bool IsFlatPattern { get; set; }

        public bool UseSheetScale { get; set; }

        public List<DrawingDimension> Dimensions { get; }

        public List<SketchSegment> BendLines { get; }

        public DrawingView()
        {
            Dimensions = new List<DrawingDimension>();

            BendLines = new List<SketchSegment>();
        }
    }
}