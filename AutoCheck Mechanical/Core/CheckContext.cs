using System.Collections.Generic;
using AutoCheckMechanical.Helpers;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Core
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