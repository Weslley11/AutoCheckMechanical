using System.Collections.Generic;

namespace WDC.MODEL
{
    public class DrawingSheet
    {
        public string Name { get; set; }

        public List<DrawingView> Views { get; }

        public DrawingSheet()
        {
            Views = new List<DrawingView>();
        }
    }
}