using System.Collections.Generic;
using WDC.SERVICES.Helpers;
using SolidWorks.Interop.sldworks;

namespace WDC.SERVICES.Core
{
    public class CheckContext
    {
        private List<View> _views;

        public SldWorks Application { get; }
        public ModelDoc2 Model { get; }
        public DrawingDoc Drawing { get; }

        public CheckContext(SldWorks application, ModelDoc2 model)
        {
            Application = application;
            Model = model;
            Drawing = model as DrawingDoc;
        }

        // Quando true, ignora a isenção dos checks de Layer/Flat Pattern/Scale
        // para desenhos sem info de chapa (usado quando o usuário força a
        // execução manualmente, mesmo o app tendo dispensado o check).
        public bool ForcarChecksDeChapa { get; set; }

        public List<View> Views
        {
            get
            {
                if (_views == null)
                    _views = DrawingHelper.GetAllViews(Drawing);

                return _views;
            }
        }

        public bool IsDrawing
        {
            get
            {
                return Drawing != null;
            }
        }

        public int SheetCount
        {
            get
            {
                return Drawing?.GetSheetCount() ?? 0;
            }
        }

        public string FileName
        {
            get
            {
                return Model?.GetTitle();
            }
        }

        public string FilePath
        {
            get
            {
                return Model?.GetPathName();
            }
        }
    }
}