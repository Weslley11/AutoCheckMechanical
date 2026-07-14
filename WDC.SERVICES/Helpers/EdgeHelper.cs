using System;
using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Helpers
{
    public static class EdgeHelper
    {
        public static double[] GetStartPoint(Edge edge)
        {
            if (edge == null)
                return null;

            Vertex v = edge.GetStartVertex();

            return v?.GetPoint() as double[];
        }

        public static double[] GetEndPoint(Edge edge)
        {
            if (edge == null)
                return null;

            Vertex v = edge.GetEndVertex();

            return v?.GetPoint() as double[];
        }

        public static double GetLength(Edge edge)
        {
            if (edge == null)
                return 0;

            double[] p1 = GetStartPoint(edge);
            double[] p2 = GetEndPoint(edge);

            if (p1 == null || p2 == null)
                return 0;

            return Math.Sqrt(
                Math.Pow(p2[0] - p1[0], 2) +
                Math.Pow(p2[1] - p1[1], 2) +
                Math.Pow(p2[2] - p1[2], 2));
        }

        public static bool IsVertical(Edge edge)
        {
            double[] p1 = GetStartPoint(edge);
            double[] p2 = GetEndPoint(edge);

            if (p1 == null || p2 == null)
                return false;

            return Math.Abs(p1[0] - p2[0]) < 0.000001;
        }

        public static bool IsHorizontal(Edge edge)
        {
            double[] p1 = GetStartPoint(edge);
            double[] p2 = GetEndPoint(edge);

            if (p1 == null || p2 == null)
                return false;

            return Math.Abs(p1[1] - p2[1]) < 0.000001;
        }
    }
}