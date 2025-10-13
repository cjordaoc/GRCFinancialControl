using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GRCFinancialControl.Avalonia.Converters
{
    public class KpiColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is decimal kpiValue)
            {
                if (kpiValue >= 95)
                {
                    return new SolidColorBrush(Colors.Green);
                }
                else if (kpiValue >= 70)
                {
                    return new SolidColorBrush(Colors.Yellow);
                }
                else
                {
                    return new SolidColorBrush(Colors.Red);
                }
            }

            return new SolidColorBrush(Colors.Transparent);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}