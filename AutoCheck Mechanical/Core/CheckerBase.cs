using AutoCheckMechanical.Interfaces;
using AutoCheckMechanical.Models;

namespace AutoCheckMechanical.Core
{
    public abstract class CheckerBase : IChecker
    {
        public abstract string Name { get; }

        public abstract CheckResult Execute(CheckContext context);

        protected CheckResult CreateResult()
        {
            return new CheckResult()
            {
                Checker = Name,
                Success = true
            };
        }

        protected void AddLog(CheckResult result, string text)
        {
            result.AddLog(text);
        }

        protected void AddError(CheckResult result, string text)
        {
            result.Success = false;
            result.Errors.Add(text);
        }

        protected void AddWarning(CheckResult result, string text)
        {
            result.AddLog("AVISO: " + text);
        }

        protected void AddInfo(CheckResult result, string text)
        {
            result.AddLog(text);
        }
    }
}