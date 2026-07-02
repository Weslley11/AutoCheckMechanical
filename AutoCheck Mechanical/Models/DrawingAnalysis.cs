using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Models
{
    public class DrawingAnalysis
    {
        public View FlatPatternView { get; set; }

        public List<SketchSegment> BendLines { get; set; }
            = new List<SketchSegment>();

        public List<DisplayDimension> Dimensions { get; set; }
            = new List<DisplayDimension>();

        public List<Annotation> Annotations { get; set; }
            = new List<Annotation>();

        public List<Note> Notes { get; set; }
            = new List<Note>();

        public List<Note> Balloons { get; set; }
            = new List<Note>();

        public string Layer { get; set; }

        public string Scale { get; set; }

        public List<View> Views;

        public List<Sheet> Sheets;

        public int DimensionCount;

        public int BalloonCount;

        public int HoleCalloutCount;

        public int NoteCount;

        public int BendCount;

        public bool HasScaleError;

        public bool HasLayerError;

        public bool HasDimensionError;

        public bool IsFlatPatternFound => FlatPatternView != null;
    }
}