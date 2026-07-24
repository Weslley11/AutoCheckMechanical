using WDC.SERVICES.Helpers;
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

        // Bloco repetido em todo checker de chapa (Flat Pattern/Layer/Scale/
        // Bloco Legenda WAU): marca o result como dispensado quando o
        // desenho não tem info de chapa (ver
        // WauBlockHelper.DesenhoDispensaChecksDeChapa). Devolve true quando
        // dispensou -- o chamador só precisa dar "return result" nesse caso.
        protected bool TrySkipSemInfoDeChapa(CheckContext context, CheckResult result,
            string mensagemAviso = "Sem info de chapa: checks de layer/planificado dispensados.")
        {
            if (!WauBlockHelper.DesenhoDispensaChecksDeChapa(context))
                return false;

            result.Skipped = true;
            result.Message = "Check dispensado (sem info de chapa).";
            AddLog(result, "Sem Matéria-Prima e sem vista planificada: check dispensado.");
            result.AddWarning(mensagemAviso);

            return true;
        }
    }
}