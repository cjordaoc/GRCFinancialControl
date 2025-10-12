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

        public static readonly IValueConverter AreEqual = EqualityValueConverter.Instance;
    }

    public sealed class EqualityValueConverter : IValueConverter
    {
        public static readonly EqualityValueConverter Instance = new();

        private EqualityValueConverter()
        {
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null && parameter is null)
            {
                return true;
            }

            if (value is null || parameter is null)
            {
                return false;
            }

            if (value.GetType().IsEnum && parameter is string enumCandidate)
            {
                if (Enum.TryParse(value.GetType(), enumCandidate, true, out var enumValue))
                {
                    return value.Equals(enumValue);
                }

                return false;
            }

            return string.Equals(
                System.Convert.ToString(value, CultureInfo.InvariantCulture),
                System.Convert.ToString(parameter, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
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
