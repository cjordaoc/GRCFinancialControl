using System;
using System.Collections.Generic;
using System.Linq;

namespace Invoices.Core.Payments;

/// <summary>
/// Provides the supported invoice payment types for planners and exports.
/// </summary>
public static class PaymentTypeCatalog
{
    public const string TransferenciaBancariaCode = "TRANSFERENCIA_BANCARIA";
    public const string BoletosCode = "BOLETOS";

    private static readonly PaymentTypeOption[] OptionsBackingField =
    {
        new(TransferenciaBancariaCode, "Transferência Bancária"),
        new(BoletosCode, "Boletos"),
    };

    /// <summary>
    /// Gets the immutable list of payment type options.
    /// </summary>
    public static IReadOnlyList<PaymentTypeOption> Options => OptionsBackingField;

    /// <summary>
    /// Normalizes a payment type code ensuring it maps to a supported option.
    /// </summary>
    /// <param name="code">The code to normalize.</param>
    /// <returns>The resolved code, or <see cref="TransferenciaBancariaCode"/> when unknown.</returns>
    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return TransferenciaBancariaCode;
        }

        var normalized = code.Trim().ToUpperInvariant();
        return OptionsBackingField.Any(option => string.Equals(option.Code, normalized, StringComparison.Ordinal))
            ? normalized
            : TransferenciaBancariaCode;
    }

    /// <summary>
    /// Attempts to resolve the option associated with the provided code.
    /// </summary>
    /// <param name="code">The payment type code.</param>
    /// <param name="option">The resolved option.</param>
    /// <returns><c>true</c> when the code exists; otherwise <c>false</c>.</returns>
    public static bool TryGetByCode(string? code, out PaymentTypeOption option)
    {
        var normalized = NormalizeCode(code);
        option = OptionsBackingField.First(opt => string.Equals(opt.Code, normalized, StringComparison.Ordinal));
        return string.Equals(normalized, code?.Trim().ToUpperInvariant(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves the option associated with the provided code, defaulting when missing.
    /// </summary>
    /// <param name="code">The payment type code.</param>
    /// <returns>The resolved option.</returns>
    public static PaymentTypeOption GetByCode(string? code)
    {
        var normalized = NormalizeCode(code);
        return OptionsBackingField.First(option => string.Equals(option.Code, normalized, StringComparison.Ordinal));
    }
}

/// <summary>
/// Represents a selectable payment type option.
/// </summary>
/// <param name="Code">The system code.</param>
/// <param name="DisplayName">The user-facing description.</param>
public sealed record PaymentTypeOption(string Code, string DisplayName);
