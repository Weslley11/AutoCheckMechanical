using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace WDC.MODEL
{
    public class DrawingAnnotation
    {
        public Annotation Annotation { get; set; }

        public object Specific { get; set; }

        public string Name { get; set; }

        public int Type { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public string TypeName
        {
            get
            {
                switch ((swAnnotationType_e)Type)
                {
                    case swAnnotationType_e.swNote:
                        return "Note";

                    case swAnnotationType_e.swDisplayDimension:
                        return "Dimension";

                    case swAnnotationType_e.swGTol:
                        return "GTol";

                    case swAnnotationType_e.swWeldSymbol:
                        return "Weld";

                    case swAnnotationType_e.swDatumTag:
                        return "Datum";

                    case swAnnotationType_e.swBlock:
                        return "Block";

                    case swAnnotationType_e.swCenterMarkSym:
                        return "Center Mark";

                    default:
                        return Type.ToString();
                }
            }
        }
    }
}