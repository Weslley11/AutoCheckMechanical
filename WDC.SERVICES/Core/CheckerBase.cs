using WDC.SERVICES.Interfaces;
using WDC.MODEL;

namespace WDC.SERVICES.Core
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

    }
}