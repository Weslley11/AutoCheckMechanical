using System.Collections.Generic;
using AutoCheckMechanical.Core;
using AutoCheckMechanical.Interfaces;
using AutoCheckMechanical.Models;

namespace AutoCheckMechanical.Services
{
    public class CheckEngine
    {
        private readonly List<IChecker> _checkers =
            new List<IChecker>();

        public void Register(IChecker checker)
        {
            _checkers.Add(checker);
        }

        public IReadOnlyList<IChecker> Checkers
        {
            get
            {
                return _checkers;
            }
        }

        public List<CheckResult> Execute(CheckContext context)
        {
            List<CheckResult> results =
                new List<CheckResult>();

            foreach (IChecker checker in _checkers)
            {
                results.Add(
                    checker.Execute(context));
            }

            return results;
        }
    }
}