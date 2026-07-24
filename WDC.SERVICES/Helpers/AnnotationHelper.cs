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

        public static List<DrawingAnnotation> GetBalloons(View view)
        {
            return GetAnnotations(view)
                .FindAll(x => x.Type == (int)swAnnotationType_e.swNote &&
                              (x.Specific as Note)?.IsBomBalloon() == true);
        }

        #endregion
    }
}