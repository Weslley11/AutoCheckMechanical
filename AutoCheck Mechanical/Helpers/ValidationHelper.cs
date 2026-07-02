using System;
using AutoCheckMechanical.Configuration;
using SolidWorks.Interop.sldworks;

namespace AutoCheckMechanical.Helpers
{
    public static class ValidationHelper
    {
        public static bool CheckScale(View view)
        {
            return ScaleHelper.IsOneToOne(view);
        }

        public static bool CheckLayer(View view)
        {
            string layer = ViewHelper.GetLayer(view);

            return string.Equals(layer, CheckSettings.FlatPatternLayer, StringComparison.OrdinalIgnoreCase);
        }

        public static bool CheckFlatPattern(View view)
        {
            if (view == null)
                return false;

            return view.IsFlatPatternView();
        }

        public static bool CheckBendLines(View view)
        {
            return ViewHelper.GetBendLines(view).Count > 0;
        }

        public static bool CheckOrdinateDimensions(View view)
        {
            return DimensionHelper.CountOrdinate(view) > 0;
        }

        // Ainda vamos implementar
        public static bool CheckHoleCallouts(View view)
        {
            return true;
        }

        // Ainda vamos implementar
        public static bool CheckBalloons(View view)
        {
            return true;
        }

        // Ainda vamos implementar
        public static bool CheckNotes(View view)
        {
            return true;
        }
    }
}