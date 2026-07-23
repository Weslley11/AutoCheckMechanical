using System;
using System.Globalization;
using System.Windows.Data;
using WDC.VIEWMODEL;

namespace WDC.Converters
{
    // Liga cada RadioButton do filtro rápido (ComErro/SemErro/Pendente/Todos)
    // a um valor específico de FiltroStatus -- ConverterParameter é o nome do
    // valor do enum (ex.: "ComErro"), igual pros dois lados (Convert marca o
    // RadioButton certo, ConvertBack atualiza a ViewModel quando o usuário
    // clica em outro).
    public class FiltroStatusToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is FiltroStatus status && parameter is string nome && status.ToString() == nome;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool marcado && marcado && parameter is string nome)
                return Enum.Parse(typeof(FiltroStatus), nome);

            return Binding.DoNothing;
        }
    }
}
