using System.Collections.Generic;

namespace AutoCheckMechanical.Models
{
    public class BatchFileResult
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public bool OpenFailed { get; set; }
        public string OpenError { get; set; }
        public int SheetCount { get; set; }
        public string ThumbnailPath { get; set; }
        public List<CheckResult> Results { get; set; } = new List<CheckResult>();
    }
}
