using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using AutoCheckMechanical.Models;

namespace AutoCheckMechanical.Helpers
{
    public static class ViewHelper
    {
        public static bool IsFlatPattern(View view)
        {
            if (view == null)
                return false;

            string config = view.ReferencedConfiguration;

            if (string.IsNullOrEmpty(config))
                return false;

            return config.ToUpper().Contains("FLAT");
        }

        public static string GetConfiguration(View view)
        {
            return view?.ReferencedConfiguration;
        }

        public static string GetLayer(View view)
        {
            if (view == null)
                return "";

            DrawingComponent comp = view.RootDrawingComponent;

            if (comp == null)
                return "";

            return comp.Layer;
        }

        public static bool UseSheetScale(View view)
        {
            if (view == null)
                return false;

            return view.UseSheetScale == 1;
        }

        public static List<SketchSegment> GetBendLines(View view)
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
        public static List<BendLine> GetBendLinesInfo(View view)
        {
            List<BendLine> list = new List<BendLine>();

            if (view == null)
                return list;

            object[] bends = view.GetBendLines() as object[];

            if (bends == null)
                return list;

            foreach (object obj in bends)
            {
                SketchSegment seg = obj as SketchSegment;

                if (seg == null)
                    continue;

                SketchLine line = seg as SketchLine;

                if (line == null)
                    continue;

                SketchPoint sp = line.GetStartPoint2();
                SketchPoint ep = line.GetEndPoint2();

                BendLine bend = new BendLine();

                bend.Segment = seg;
                bend.Name = seg.GetName();

                bend.X1 = sp.X;
                bend.Y1 = sp.Y;

                bend.X2 = ep.X;
                bend.Y2 = ep.Y;

                bend.CenterX = (bend.X1 + bend.X2) / 2.0;
                bend.CenterY = (bend.Y1 + bend.Y2) / 2.0;

                bend.IsVertical =
                    System.Math.Abs(bend.X1 - bend.X2) < 0.00001;

                bend.IsHorizontal =
                    System.Math.Abs(bend.Y1 - bend.Y2) < 0.00001;

                list.Add(bend);
            }

            return list;
        }
    }
}