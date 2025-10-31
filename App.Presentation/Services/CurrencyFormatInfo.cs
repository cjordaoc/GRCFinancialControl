using System.Globalization;

namespace App.Presentation.Services;

/// <summary>
/// Represents metadata describing how a currency should be displayed.
/// </summary>
public readonly struct CurrencyFormatInfo
{
    public CurrencyFormatInfo(string code, string symbol, CultureInfo culture)
    {
        Code = code;
        Symbol = symbol;
        Culture = culture;
    }

    public string Code { get; }

    public string Symbol { get; }

    public CultureInfo Culture { get; }
}
