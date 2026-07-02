using System.Collections.Generic;

namespace AutoCheckMechanical.Models
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