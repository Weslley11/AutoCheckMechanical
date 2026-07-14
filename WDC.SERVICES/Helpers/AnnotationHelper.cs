using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using WDC.MODEL;

namespace WDC.SERVICES.Helpers
{
    public static class AnnotationHelper
    {
        #region Coleta

        public static List<Annotation> GetAll(View view)
        {
            List<Annotation> list = new List<Annotation>();

            if (view == null)
                return list;

            Annotation ann = view.GetFirstAnnotation() as Annotation;

            while (ann != null)
            {
                list.Add(ann);
                ann = ann.GetNext3() as Annotation;
            }

            return list;
        }

        public static List<DrawingAnnotation> GetAnnotations(View view)
        {
            List<DrawingAnnotation> list = new List<DrawingAnnotation>();

            if (view == null)
                return list;

            Annotation ann = view.GetFirstAnnotation() as Annotation;

            while (ann != null)
            {
                DrawingAnnotation item = new DrawingAnnotation();

                item.Annotation = ann;
                item.Specific = ann.GetSpecificAnnotation();
                item.Type = ann.GetType();
                item.Name = ann.GetName();

                double[] pos = ann.GetPosition() as double[];

                if (pos != null && pos.Length >= 3)
                {
                    item.X = pos[0];
                    item.Y = pos[1];
                    item.Z = pos[2];
                }

                list.Add(item);

                ann = ann.GetNext3() as Annotation;
            }

            return list;
        }

        #endregion

        #region Busca por tipo

        public static List<Annotation> GetByType(View view, swAnnotationType_e type)
        {
            List<Annotation> list = new List<Annotation>();

            foreach (Annotation ann in GetAll(view))
            {
                if (ann.GetType() == (int)type)
                    list.Add(ann);
            }

            return list;
        }

        public static List<DrawingAnnotation> GetNotes(View view)
        {
            return GetAnnotations(view)
                .FindAll(x => x.Type == (int)swAnnotationType_e.swNote);
        }

        public static List<DrawingAnnotation> GetDimensions(View view)
        {
            return GetAnnotations(view)
                .FindAll(x => x.Type == (int)swAnnotationType_e.swDisplayDimension);
        }

        public static List<DrawingAnnotation> GetDisplayDimensions(View view)
        {
            return GetDimensions(view);
        }

        public static List<DrawingAnnotation> GetGTols(View view)
        {
            return GetAnnotations(view)
                .FindAll(x => x.Type == (int)swAnnotationType_e.swGTol);
        }

        public static List<DrawingAnnotation> GetDatumTags(View view)
        {
            return GetAnnotations(view)
                .FindAll(x => x.Type == (int)swAnnotationType_e.swDatumTag);
        }

        public static List<DrawingAnnotation> GetWeldSymbols(View view)
        {
            return GetAnnotations(view)
                .FindAll(x => x.Type == (int)swAnnotationType_e.swWeldSymbol);
        }

        public static List<DrawingAnnotation> GetCenterMarks(View view)
        {
            return GetAnnotations(view)
                .FindAll(x => x.Type == (int)swAnnotationType_e.swCenterMarkSym);
        }

        public static List<DrawingAnnotation> GetBalloons(View view)
        {
            return GetAnnotations(view)
                .FindAll(x => x.Type == (int)swAnnotationType_e.swNote &&
                              (x.Specific as Note)?.IsBomBalloon() == true);
        }

        public static List<DrawingAnnotation> GetSurfaceFinish(View view)
        {
            return GetAnnotations(view)
                .FindAll(x => x.Type == (int)swAnnotationType_e.swSFSymbol);
        }

        public static List<DrawingAnnotation> GetBlocks(View view)
        {
            return GetAnnotations(view)
                .FindAll(x => x.Type == (int)swAnnotationType_e.swBlock);
        }

        #endregion

        #region Contadores

        public static int Count(View view)
        {
            return GetAll(view).Count;
        }

        public static int Count(View view, swAnnotationType_e type)
        {
            return GetByType(view, type).Count;
        }

        public static bool HasNotes(View view)
        {
            return GetNotes(view).Count > 0;
        }

        public static bool HasDimensions(View view)
        {
            return GetDimensions(view).Count > 0;
        }

        public static bool HasBalloons(View view)
        {
            return GetBalloons(view).Count > 0;
        }

        public static bool HasGTols(View view)
        {
            return GetGTols(view).Count > 0;
        }

        public static bool HasDatumTags(View view)
        {
            return GetDatumTags(view).Count > 0;
        }

        public static bool HasWeldSymbols(View view)
        {
            return GetWeldSymbols(view).Count > 0;
        }

        public static bool HasCenterMarks(View view)
        {
            return GetCenterMarks(view).Count > 0;
        }

        public static bool HasSurfaceFinish(View view)
        {
            return GetSurfaceFinish(view).Count > 0;
        }

        #endregion

        #region Estatísticas

        public static Dictionary<string, int> GetStatistics(View view)
        {
            Dictionary<string, int> stats = new Dictionary<string, int>();

            stats["Dimensions"] = GetDimensions(view).Count;
            stats["Notes"] = GetNotes(view).Count;
            stats["Balloons"] = GetBalloons(view).Count;
            stats["GTols"] = GetGTols(view).Count;
            stats["Datum"] = GetDatumTags(view).Count;
            stats["Weld"] = GetWeldSymbols(view).Count;
            stats["CenterMarks"] = GetCenterMarks(view).Count;
            stats["SurfaceFinish"] = GetSurfaceFinish(view).Count;
            stats["Blocks"] = GetBlocks(view).Count;

            return stats;
        }

        #endregion

        #region Debug

        public static void Dump(View view, CheckResult result)
        {
            foreach (DrawingAnnotation ann in GetAnnotations(view))
            {
                result.AddLog("--------------------------------");
                result.AddLog($"Nome : {ann.Name}");
                result.AddLog($"Tipo : {ann.TypeName}");
                result.AddLog($"X    : {ann.X}");
                result.AddLog($"Y    : {ann.Y}");
            }
        }

        #endregion
    }
}