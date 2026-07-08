using System.Collections.Generic;

namespace AutoCheckMechanical.Models
{
    public class CheckResult
    {
        public string Checker { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; }
        public List<string> Warnings { get; }
        public List<string> Logs { get; }
        public Dictionary<string, string> Fields { get; }

        public CheckResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            Logs = new List<string>();
            Fields = new Dictionary<string, string>();
        }

        public void AddLog(string text)
        {
            Logs.Add(text);
        }

        public void AddError(string text)
        {
            Errors.Add(text);
            Success = false;
        }

        public void AddWarning(string text)
        {
            Warnings.Add(text);
        }
    }
}