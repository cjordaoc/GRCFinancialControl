using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ClosedXML.Excel;

namespace GRCFinancialControl.Parsing
{
    internal static class ExcelHeaderDetector
    {
        public static (Dictionary<string, int> Headers, int HeaderRow) DetectHeaders(IXLWorksheet worksheet, HeaderSchema schema)
        {
            if (worksheet == null)
            {
                throw new ArgumentNullException(nameof(worksheet));
            }

            var normalizedSynonyms = schema.Synonyms.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(ExcelParsingUtilities.NormalizeHeader).ToHashSet(StringComparer.OrdinalIgnoreCase));

            foreach (var row in worksheet.RowsUsed())
            {
                var cells = row.CellsUsed().Select(c => new
                {
                    Column = c.Address.ColumnNumber,
                    Value = ExcelParsingUtilities.NormalizeHeader(c.GetValue<string>() ?? string.Empty)
                }).ToList();

                var matches = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in normalizedSynonyms)
                {
                    var match = cells.FirstOrDefault(c => entry.Value.Contains(c.Value));
                    if (match != null)
                    {
                        matches[entry.Key] = match.Column;
                    }
                }

                if (schema.RequiredColumns.All(matches.ContainsKey))
                {
                    return (matches, row.RowNumber());
                }
            }

            throw new InvalidDataException($"Could not locate required headers: {string.Join(", ", schema.RequiredColumns)}");
        }
    }
}
