using System.Collections.Generic;
using AutoCheckMechanical.Models;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace AutoCheckMechanical.Helpers
{
    public static class DimensionHelper
    {
        /// <summary>
        /// Retorna TODAS as DisplayDimensions da View.
        /// </summary>
        public static List<DisplayDimension> GetAll(View view)
        {
            List<DisplayDimension> list = new List<DisplayDimension>();

            if (view == null)
                return list;

            Annotation ann = view.GetFirstAnnotation() as Annotation;

            while (ann != null)
            {
                if (ann.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    DisplayDimension disp = ann.GetSpecificAnnotation() as DisplayDimension;

                    if (disp != null) list.Add(disp);
                }

                ann = ann.GetNext3() as Annotation;
            }

            return list;
        }

        public static void DumpDimensions(View view, CheckResult result)
        {
            foreach (DisplayDimension disp in GetAll(view))
            {
                Dimension dim = disp.GetDimension2(0);

                if (dim == null)
                    continue;

                Annotation ann = disp.GetAnnotation();

                string nome = dim.FullName;

                result.AddLog("--------------------------------");
                result.AddLog($"Dimension : {nome}");

                try
                {
                    object valueObj = dim.GetSystemValue3(
                        (int)swInConfigurationOpts_e.swThisConfiguration,
                        null);

                    if (valueObj is double[])
                    {
                        double[] values = (double[])valueObj;

                        if (values.Length > 0)
                            result.AddLog($"Valor = {values[0]}");
                    }
                    else if (valueObj is double)
                    {
                        result.AddLog($"Valor = {(double)valueObj}");
                    }
                }
                catch (System.Exception ex)
                {
                    result.AddLog($"Erro GetSystemValue3: {ex.Message}");
                }

                if (ann != null)
                {
                    double[] pos = ann.GetPosition() as double[];

                    if (pos != null)
                    {
                        result.AddLog($"Texto X = {pos[0]}");
                        result.AddLog($"Texto Y = {pos[1]}");
                    }
                }

                // SketchPoints anexados
                foreach (SketchPoint pt in GetAttachedSketchPoints(disp))
                {
                    result.AddLog("SketchPoint");
                    result.AddLog($"X = {pt.X}");
                    result.AddLog($"Y = {pt.Y}");
                    result.AddLog($"Z = {pt.Z}");
                }

                // Edges anexadas
                foreach (Edge edge in GetAttachedEdges(disp))
                {
                    try
                    {
                        double[] start = edge.GetStartVertex()?.GetPoint() as double[];
                        double[] end = edge.GetEndVertex()?.GetPoint() as double[];

                        result.AddLog("Edge");

                        if (start != null)
                            result.AddLog($"Start = {start[0]}, {start[1]}, {start[2]}");

                        if (end != null)
                            result.AddLog($"End = {end[0]}, {end[1]}, {end[2]}");
                    }
                    catch
                    {
                        result.AddLog("Edge inválida.");
                    }
                }
            }
        }
        /// <summary>
        /// Quantidade total de DisplayDimensions.
        /// </summary>
        public static int Count(View view)
        {
            return GetAll(view).Count;
        }

        public static List<DisplayDimension> GetLinear(View view)
        {
            List<DisplayDimension> list = new List<DisplayDimension>();

            foreach (DisplayDimension dim in GetAll(view))
            {
                if (dim.Type2 == (int)swDimensionType_e.swLinearDimension)
                {
                    list.Add(dim);
                }
            }
            return list;
        }

        public static int CountLinear(View view)
        {
            return GetLinear(view).Count;
        }

        public static List<DisplayDimension> GetOrdinate(View view)
        {
            List<DisplayDimension> list = new List<DisplayDimension>();

            foreach (DisplayDimension dim in GetAll(view))
            {
                if (dim.Type2 == (int)swDimensionType_e.swOrdinateDimension)
                {
                    list.Add(dim);
                }
            }

            return list;
        }

        public static void DumpAttachedEntities(View view, CheckResult result)
        {
            foreach (DisplayDimension disp in GetAll(view))
            {
                Annotation ann = disp.GetAnnotation();

                object[] entities = ann.GetAttachedEntities3() as object[];

                result.AddLog("--------------------------------");
                result.AddLog($"Cota: {disp.GetDimension2(0)?.FullName}");

                if (entities == null)
                {
                    result.AddLog("Nenhuma entidade anexada.");
                    continue;
                }

                result.AddLog($"Qtd entidades: {entities.Length}");

                foreach (object entity in entities)
                {
                    if (entity == null)
                    {
                        result.AddLog("NULL");
                        continue;
                    }

                    result.AddLog("--------------------------------");
                    result.AddLog($"CLR: {entity.GetType().FullName}");

                    Edge edge = entity as Edge;

                    if (edge != null)
                    {
                        result.AddLog("É EDGE");

                        try
                        {
                            Curve curve = edge.GetCurve();

                            if (curve != null)
                            {
                                result.AddLog($"Curve Identity: {curve.Identity()}");

                                double[] start = edge.GetStartVertex()?.GetPoint() as double[];
                                double[] end = edge.GetEndVertex()?.GetPoint() as double[];

                                if (start != null)
                                    result.AddLog($"Start: X={start[0]}  Y={start[1]}  Z={start[2]}");

                                if (end != null)
                                    result.AddLog($"End..: X={end[0]}  Y={end[1]}  Z={end[2]}");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            result.AddLog(ex.Message);
                        }

                        continue;
                    }

                    SketchPoint point = entity as SketchPoint;

                    if (point != null)
                    {
                        result.AddLog("É SKETCH POINT");
                        result.AddLog($"X={point.X}");
                        result.AddLog($"Y={point.Y}");
                        result.AddLog($"Z={point.Z}");
                    }
                }
            }
        }

        public static int CountOrdinate(View view)
        {
            return GetOrdinate(view).Count;
        }

        public static List<DisplayDimension> GetHoleCallouts(View view)
        {
            List<DisplayDimension> list = new List<DisplayDimension>();

            foreach (DisplayDimension dim in GetAll(view))
            {
                if (dim.Type2 == (int)swDimensionType_e.swDiameterDimension)
                {
                    list.Add(dim);
                }
            }

            return list;

        }

        public static List<Edge> GetAttachedEdges(DisplayDimension disp)
        {
            List<Edge> list = new List<Edge>();

            if (disp == null)
                return list;

            Annotation ann = disp.GetAnnotation();

            if (ann == null)
                return list;

            object[] entities = ann.GetAttachedEntities3() as object[];

            if (entities == null)
                return list;

            foreach (object obj in entities)
            {
                Edge edge = obj as Edge;

                if (edge != null)
                    list.Add(edge);
            }

            return list;
        }

        public static List<SketchPoint> GetAttachedSketchPoints(DisplayDimension disp)
        {
            List<SketchPoint> points = new List<SketchPoint>();

            Annotation ann = disp.GetAnnotation();

            if (ann == null)
                return points;

            object[] entities = ann.GetAttachedEntities3() as object[];

            if (entities == null)
                return points;

            foreach (object obj in entities)
            {
                SketchPoint pt = obj as SketchPoint;

                if (pt != null)
                    points.Add(pt);
            }

            return points;
        }
    }
}