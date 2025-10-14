using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace GRCFinancialControl.Avalonia.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        public IBrush TrueBrush { get; set; } = Brushes.Transparent;
        public IBrush FalseBrush { get; set; } = Brushes.Transparent;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool flag)
            {
                return flag ? TrueBrush : FalseBrush;
            }

            return FalseBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
