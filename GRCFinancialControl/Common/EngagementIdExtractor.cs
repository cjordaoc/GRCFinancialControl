using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace GRCFinancialControl.Common
{
    public static class EngagementIdExtractor
    {
        private static readonly Regex EngagementPattern = new("(E-\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryExtract(string? value, out string engagementId)
        {
            return TryExtract(value, out engagementId, out _);
        }

        public static bool TryExtract(string? value, out string engagementId, out string? cleanedLabel)
        {
            engagementId = string.Empty;
            cleanedLabel = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var match = EngagementPattern.Match(value);
            if (!match.Success)
            {
                return false;
            }

            engagementId = match.Value.ToUpperInvariant();

            var segments = new[]
            {
                value[..match.Index],
                value[(match.Index + match.Length)..]
            };

            var cleaned = string.Join(" ", segments
                .Select(segment => segment?.Trim(' ', '-', '–', '—', '(', ')', '/', '\\'))
                .Where(segment => !string.IsNullOrWhiteSpace(segment)));

            cleanedLabel = StringNormalizer.TrimToNull(cleaned);
            return true;
        }
    }
}
