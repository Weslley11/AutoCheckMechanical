using WDC.SERVICES.Core;
using WDC.MODEL;

namespace WDC.SERVICES.Interfaces
{
    public interface IChecker
    {
        string Name { get; }

        CheckResult Execute(CheckContext context);
    }
}