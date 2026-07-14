using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Helpers
{
    public static class BendLineHelper
    {
        public static List<SketchSegment> GetSegments(View view)
        {
            List<SketchSegment> list = new List<SketchSegment>();

            if (view == null)
                return list;

            object[] bends = view.GetBendLines() as object[];

            if (bends == null)
                return list;

            foreach (object obj in bends)
            {
                SketchSegment seg = obj as SketchSegment;

                if (seg != null)
                    list.Add(seg);
            }

            return list;
        }

        public static int Count(View view)
        {
            return GetSegments(view).Count;
        }

        public static List<Edge> GetRelatedEdges(
            View view,
            int bendIndex)
        {
            List<Edge> list = new List<Edge>();

            if (view == null)
                return list;

            try
            {
                int count =
                    view.GetRelatedTangentEdgeCount(bendIndex);

                if (count == 0)
                    return list;

                object[] edges =
                    view.GetRelatedTangentEdges(bendIndex) as object[];

                if (edges == null)
                    return list;

                foreach (object obj in edges)
                {
                    Edge edge = obj as Edge;

                    if (edge != null)
                        list.Add(edge);
                }
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"BendLineHelper.GetRelatedEdges: {ex.Message}");
            }

            return list;
        }

        public static bool IsSameEdge(
            Edge edge1,
            Edge edge2)
        {
            if (edge1 == null || edge2 == null)
                return false;

            try
            {
                Vertex s1 = edge1.GetStartVertex();
                Vertex e1 = edge1.GetEndVertex();

                Vertex s2 = edge2.GetStartVertex();
                Vertex e2 = edge2.GetEndVertex();

                if (s1 == null || e1 == null)
                    return false;

                if (s2 == null || e2 == null)
                    return false;

                double[] p1 = s1.GetPoint() as double[];
                double[] p2 = e1.GetPoint() as double[];

                double[] p3 = s2.GetPoint() as double[];
                double[] p4 = e2.GetPoint() as double[];

                return ComparePoint(p1, p3) &&
                       ComparePoint(p2, p4);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"BendLineHelper.IsSameEdge: {ex.Message}");

                return false;
            }
        }

        private static bool ComparePoint(
            double[] p1,
            double[] p2)
        {
            if (p1 == null || p2 == null)
                return false;

            const double tol = 0.000001;

            return
                System.Math.Abs(p1[0] - p2[0]) < tol &&
                System.Math.Abs(p1[1] - p2[1]) < tol &&
                System.Math.Abs(p1[2] - p2[2]) < tol;
        }

        public static bool HasDimension(
    View view,
    int bendIndex)
        {
            List<Edge> bendEdges =
                GetRelatedEdges(view, bendIndex);

            if (bendEdges.Count == 0)
                return false;

            foreach (DisplayDimension dim in DimensionHelper.GetAll(view))
            {
                List<Edge> dimEdges =
                    DimensionHelper.GetAttachedEdges(dim);

                foreach (Edge bendEdge in bendEdges)
                {
                    foreach (Edge dimEdge in dimEdges)
                    {
                        if (IsSameEdge(bendEdge, dimEdge))
                            return true;
                    }
                }
            }

            return false;
        }
    }
}