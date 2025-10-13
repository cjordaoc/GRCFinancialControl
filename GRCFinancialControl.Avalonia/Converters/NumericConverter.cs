using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GRCFinancialControl.Avalonia.Converters
{
    public class NumericConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                {
                    if (decimal.TryParse(stringValue, NumberStyles.Any, culture, out var decimalResult))
                    {
                        return decimalResult;
                    }
                }
                else if (targetType == typeof(int) || targetType == typeof(int?))
                {
                    if (int.TryParse(stringValue, NumberStyles.Any, culture, out var intResult))
                    {
                        return intResult;
                    }
                }
                else if (targetType == typeof(double) || targetType == typeof(double?))
                {
                    if (double.TryParse(stringValue, NumberStyles.Any, culture, out var doubleResult))
                    {
                        return doubleResult;
                    }
                }
            }

            if (targetType == typeof(decimal?) || targetType == typeof(int?) || targetType == typeof(double?))
            {
                return null;
            }

            return Activator.CreateInstance(targetType);
        }
    }
}