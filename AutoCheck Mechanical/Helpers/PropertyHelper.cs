using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Helpers
{
    public static class PropertyHelper
    {
        public static string GetValue(ModelDoc2 documento, string nomePropriedade)
        {
            if (documento == null)
                return null;

            CustomPropertyManager gerenciador = documento.Extension.CustomPropertyManager[""];

            if (gerenciador == null)
                return null;

            string valOut;
            string resolvedValOut;
            bool wasResolved;
            bool linkToProperty;

            gerenciador.Get6(nomePropriedade, false, out valOut, out resolvedValOut, out wasResolved, out linkToProperty);

            return !string.IsNullOrWhiteSpace(resolvedValOut) ? resolvedValOut : valOut;
        }

        public static string GetValue(ModelDoc2 documentoPrincipal, ModelDoc2 documentoReferenciado, string nomePropriedade)
        {
            string valor = GetValue(documentoPrincipal, nomePropriedade);

            if (!string.IsNullOrWhiteSpace(valor))
                return valor;

            return GetValue(documentoReferenciado, nomePropriedade);
        }
    }
}
