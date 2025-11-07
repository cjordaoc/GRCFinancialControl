using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    /// <summary>
    /// Formats import operation summaries with statistics and skip reasons.
    /// </summary>
    internal static class ImportSummaryFormatter
    {
        public static string Build(
            string heading,
            int inserted,
            int updated,
            IReadOnlyDictionary<string, IReadOnlyCollection<string>>? skipReasons = null,
            IEnumerable<string>? notes = null,
            int? processed = null)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"{heading} summary:");
            builder.AppendLine($" - Inserted: {inserted}");
            builder.AppendLine($" - Updated: {updated}");

            var skipped = skipReasons?.Values.Sum(v => v?.Count ?? 0) ?? 0;
            builder.AppendLine($" - Skipped: {skipped}");

            if (skipReasons is { Count: > 0 })
            {
                foreach (var entry in skipReasons
                             .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var reason = string.IsNullOrWhiteSpace(entry.Key) ? "Unspecified" : entry.Key.Trim();
                    var count = entry.Value?.Count ?? 0;
                    builder.Append($"   - {reason}: {count}");

                    var sample = FormatSampleList(entry.Value);
                    if (!string.IsNullOrWhiteSpace(sample))
                    {
                        builder.Append($" ({sample})");
                    }

                    builder.AppendLine();
                }
            }

            if (processed.HasValue)
            {
                builder.AppendLine($" - Rows processed: {processed.Value}");
            }

            if (notes != null)
            {
                foreach (var note in notes)
                {
                    if (string.IsNullOrWhiteSpace(note))
                    {
                        continue;
                    }

                    builder.AppendLine($" - {note.Trim()}");
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static string FormatSampleList(IReadOnlyCollection<string>? values, int maxItems = 5)
        {
            if (values is null || values.Count == 0)
            {
                return string.Empty;
            }

            var distinct = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinct.Count == 0)
            {
                return string.Empty;
            }

            var sample = distinct.Take(maxItems).ToList();
            var builder = new StringBuilder();
            builder.Append(string.Join(", ", sample));

            if (distinct.Count > sample.Count)
            {
                builder.Append(", ...");
            }

            return builder.ToString();
        }
    }
}
