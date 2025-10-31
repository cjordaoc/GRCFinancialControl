using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace App.Presentation.Services;

/// <summary>
/// Provides helpers to resolve currency display metadata and render monetary amounts consistently.
/// </summary>
public static class CurrencyDisplayHelper
{
    private const string DefaultFallbackCultureName = "en-US";

    private static readonly ConcurrentDictionary<string, CurrencyFormatInfo> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, CultureInfo> CurrencyCultures = BuildCurrencyCultureMap();
    private static string? _defaultCurrencyCode;

    /// <summary>
    /// Configures the default currency code used when an engagement does not define one.
    /// </summary>
    /// <param name="currencyCode">The ISO currency code to use as default.</param>
    public static void SetDefaultCurrency(string? currencyCode)
    {
        _defaultCurrencyCode = NormalizeCurrencyCode(currencyCode);
    }

    /// <summary>
    /// Formats a monetary amount using the engagement currency or the configured default.
    /// </summary>
    /// <param name="amount">The value to format.</param>
    /// <param name="currencyCode">The engagement currency code.</param>
    /// <returns>A localized currency string.</returns>
    public static string Format(decimal amount, string? currencyCode)
    {
        var info = Resolve(currencyCode);
        var numberFormat = (NumberFormatInfo)info.Culture.NumberFormat.Clone();
        numberFormat.CurrencySymbol = info.Symbol;
        return amount.ToString("C2", numberFormat);
    }

    /// <summary>
    /// Resolves the metadata associated with an engagement currency.
    /// </summary>
    /// <param name="currencyCode">The engagement currency code.</param>
    /// <returns>A <see cref="CurrencyFormatInfo"/> describing how to display the currency.</returns>
    public static CurrencyFormatInfo Resolve(string? currencyCode)
    {
        var normalized = NormalizeCurrencyCode(currencyCode);
        var effectiveCode = string.IsNullOrWhiteSpace(normalized) ? _defaultCurrencyCode : normalized;

        if (string.IsNullOrWhiteSpace(effectiveCode))
        {
            return BuildFallbackInfo();
        }

        return Cache.GetOrAdd(effectiveCode, CreateCurrencyInfo);
    }

    private static CurrencyFormatInfo CreateCurrencyInfo(string currencyCode)
    {
        if (CurrencyCultures.TryGetValue(currencyCode, out var culture))
        {
            var symbol = TryGetCurrencySymbol(culture) ?? currencyCode;
            return new CurrencyFormatInfo(currencyCode, symbol, culture);
        }

        var fallbackCulture = ResolveFallbackCulture();
        return new CurrencyFormatInfo(currencyCode, currencyCode, fallbackCulture);
    }

    private static CurrencyFormatInfo BuildFallbackInfo()
    {
        var culture = ResolveFallbackCulture();
        var symbol = TryGetCurrencySymbol(culture) ?? culture.NumberFormat.CurrencySymbol;
        return new CurrencyFormatInfo(string.Empty, symbol, culture);
    }

    private static CultureInfo ResolveFallbackCulture()
    {
        if (!string.IsNullOrWhiteSpace(_defaultCurrencyCode)
            && CurrencyCultures.TryGetValue(_defaultCurrencyCode, out var defaultCulture))
        {
            return defaultCulture;
        }

        try
        {
            return CultureInfo.CurrentCulture;
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(DefaultFallbackCultureName);
        }
    }

    private static IReadOnlyDictionary<string, CultureInfo> BuildCurrencyCultureMap()
    {
        var map = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                if (!map.ContainsKey(region.ISOCurrencySymbol))
                {
                    map[region.ISOCurrencySymbol] = culture;
                }
            }
            catch (ArgumentException)
            {
                // Ignore cultures without region information.
            }
        }

        return map;
    }

    private static string? NormalizeCurrencyCode(string? currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? null
            : currencyCode.Trim().ToUpperInvariant();
    }

    private static string? TryGetCurrencySymbol(CultureInfo culture)
    {
        try
        {
            var region = new RegionInfo(culture.Name);
            return region.CurrencySymbol;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
