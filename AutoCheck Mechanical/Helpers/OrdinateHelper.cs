using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Helpers
{
    public class OrdinateInfo
    {
        public DisplayDimension Dimension { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public string Name { get; set; }
    }

    public static class OrdinateHelper
    {
        public static List<OrdinateInfo> GetOrdinateDimensions(View view)
        {
            List<OrdinateInfo> list = new List<OrdinateInfo>();

            foreach (DisplayDimension disp in DimensionHelper.GetOrdinate(view))
            {
                Annotation ann = disp.GetAnnotation();

                if (ann == null)
                    continue;

                double[] pos = ann.GetPosition() as double[];

                if (pos == null)
                    continue;

                Dimension dim = disp.GetDimension2(0);

                list.Add(new OrdinateInfo()
                {
                    Dimension = disp,
                    X = pos[0],
                    Y = pos[1],
                    Name = dim?.FullName
                });
            }

            return list;
        }
    }
}