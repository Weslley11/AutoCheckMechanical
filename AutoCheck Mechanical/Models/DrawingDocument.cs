using System.Collections.Generic;

namespace AutoCheckMechanical.Models
{
    public class DrawingDocument
    {
        public string FileName { get; set; }

        public string FilePath { get; set; }

        public List<DrawingSheet> Sheets { get; }

        public DrawingView FlatPattern { get; set; }

        public DrawingDocument()
        {
            Sheets = new List<DrawingSheet>();
        }
    }
}