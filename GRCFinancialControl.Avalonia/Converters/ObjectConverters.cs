using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GRCFinancialControl.Avalonia.Converters
{
    public static class ObjectConverters
    {
        public static readonly IValueConverter IsNotNull =
            new FuncValueConverter<object?, bool>(x => x != null);

        public static readonly IValueConverter IsNull =
            new FuncValueConverter<object?, bool>(x => x == null);
    }

    public class FuncValueConverter<TIn, TOut> : IValueConverter
    {
        private readonly Func<TIn?, TOut> _converter;

        public FuncValueConverter(Func<TIn?, TOut> converter)
        {
            _converter = converter;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return _converter((TIn?)value);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
