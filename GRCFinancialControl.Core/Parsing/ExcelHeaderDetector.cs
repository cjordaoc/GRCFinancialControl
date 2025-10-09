using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace GRCFinancialControl.Parsing
{
    internal static class ExcelHeaderDetector
    {
        private const int MaxHeaderScanRows = 20;

        public static (Dictionary<string, int> Headers, int HeaderRow) DetectHeaders(IXLWorksheet worksheet, HeaderSchema schema)
        {
            if (worksheet == null)
            {
                throw new ArgumentNullException(nameof(worksheet));
            }

            var normalizedSynonyms = schema.Synonyms.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
                    .Select(ExcelParsingUtilities.NormalizeHeader)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

            var matches = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var assignedColumns = new HashSet<int>();
            var aggregatedByColumn = new Dictionary<int, string>();
            var lastValueRow = new Dictionary<int, int>();

            foreach (var row in worksheet.RowsUsed().OrderBy(r => r.RowNumber()))
            {
                if (row.RowNumber() > MaxHeaderScanRows && schema.RequiredColumns.All(matches.ContainsKey))
                {
                    break;
                }

                foreach (var cell in row.CellsUsed())
                {
                    var normalized = ExcelParsingUtilities.NormalizeHeader(cell.GetValue<string>() ?? string.Empty);
                    if (string.IsNullOrEmpty(normalized))
                    {
                        continue;
                    }

                    var column = cell.Address.ColumnNumber;
                    if (aggregatedByColumn.TryGetValue(column, out var existing))
                    {
                        if (!existing.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                        {
                            aggregatedByColumn[column] = existing + normalized;
                        }
                    }
                    else
                    {
                        aggregatedByColumn[column] = normalized;
                    }

                    lastValueRow[column] = row.RowNumber();
                }

                EvaluateMatches(normalizedSynonyms, matches, assignedColumns, aggregatedByColumn);

                if (schema.RequiredColumns.All(matches.ContainsKey))
                {
                    break;
                }
            }

            if (!schema.RequiredColumns.All(matches.ContainsKey))
            {
                throw new InvalidDataException($"Could not locate required headers: {string.Join(", ", schema.RequiredColumns)}");
            }

            var headerRow = matches
                .Select(kvp => lastValueRow.TryGetValue(kvp.Value, out var row) ? row : 0)
                .DefaultIfEmpty(0)
                .Max();

            return (matches, headerRow);
        }

        private static void EvaluateMatches(
            IReadOnlyDictionary<string, string[]> normalizedSynonyms,
            IDictionary<string, int> matches,
            ISet<int> assignedColumns,
            IReadOnlyDictionary<int, string> aggregatedByColumn)
        {
            foreach (var synonymEntry in normalizedSynonyms)
            {
                if (matches.ContainsKey(synonymEntry.Key))
                {
                    continue;
                }

                foreach (var column in aggregatedByColumn.OrderBy(c => c.Key))
                {
                    if (assignedColumns.Contains(column.Key))
                    {
                        continue;
                    }

                    var aggregated = column.Value;
                    if (string.IsNullOrEmpty(aggregated))
                    {
                        continue;
                    }

                    foreach (var candidate in synonymEntry.Value)
                    {
                        if (aggregated.Contains(candidate, StringComparison.OrdinalIgnoreCase) ||
                            candidate.Contains(aggregated, StringComparison.OrdinalIgnoreCase))
                        {
                            matches[synonymEntry.Key] = column.Key;
                            assignedColumns.Add(column.Key);
                            goto NextHeader;
                        }
                    }
                }

            NextHeader:;
            }
        }
    }
}
