using System;
using System.Globalization;
using System.Text;

namespace GRCFinancialControl.Common
{
    public static class StringNormalizer
    {
        public static string NormalizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var trimmed = input.Trim();
            var decomposed = trimmed.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            var cleaned = builder.ToString().Normalize(NormalizationForm.FormC);
            return cleaned.ToUpperInvariant();
        }

        public static string? TrimToNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
