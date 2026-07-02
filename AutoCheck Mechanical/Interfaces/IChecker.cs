using AutoCheckMechanical.Core;
using AutoCheckMechanical.Models;

namespace AutoCheckMechanical.Interfaces
{
    public interface IChecker
    {
        string Name { get; }

        CheckResult Execute(CheckContext context);
    }
}