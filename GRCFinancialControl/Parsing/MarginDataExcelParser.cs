using System;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using GRCFinancialControl.Common;

namespace GRCFinancialControl.Parsing
{
    public sealed class MarginDataExcelParser : ExcelParserBase<MarginDataRow>
    {
        private static readonly CultureInfo[] SupportedCultures =
        {
            CultureInfo.InvariantCulture,
            new CultureInfo("pt-BR")
        };

        public MarginDataParseResult Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Excel file not found.", filePath);
            }

            var result = new MarginDataParseResult();

            using var workbook = new XLWorkbook(filePath);
            var worksheet = ExcelWorksheetHelper.FirstVisible(workbook.Worksheets);

            foreach (var row in worksheet.RowsUsed())
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var rowNumber = row.RowNumber();
                var descriptor = GetCellString(row.Cell(1));
                if (string.IsNullOrWhiteSpace(descriptor))
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing engagement descriptor in column A.");
                    continue;
                }

                if (!TryExtractEngagement(descriptor, out var engagementId, out var engagementTitle))
                {
                    result.IncrementSkipped($"Row {rowNumber}: Unable to extract engagement ID from '{descriptor}'.");
                    continue;
                }

                var marginRow = new MarginDataRow
                {
                    ExcelRowNumber = rowNumber,
                    EngagementId = engagementId,
                    EngagementTitle = engagementTitle
                };

                if (!TryReadMarginCell(row.Cell(4), out var openingMargin))
                {
                    var openingText = GetCellString(row.Cell(4));
                    if (!string.IsNullOrEmpty(openingText))
                    {
                        result.AddWarning($"Row {rowNumber}: Invalid opening margin '{openingText}'.");
                    }
                }
                else
                {
                    marginRow.OpeningMargin = openingMargin;
                }

                if (!TryReadMarginCell(row.Cell(5), out var currentMargin))
                {
                    var currentText = GetCellString(row.Cell(5));
                    if (!string.IsNullOrEmpty(currentText))
                    {
                        result.AddWarning($"Row {rowNumber}: Invalid current margin '{currentText}'.");
                    }
                }
                else
                {
                    marginRow.CurrentMargin = currentMargin;
                }

                if (!TryReadMarginCell(row.Cell(15), out var marginValue))
                {
                    var marginText = GetCellString(row.Cell(15));
                    if (!string.IsNullOrEmpty(marginText))
                    {
                        result.AddWarning($"Row {rowNumber}: Invalid margin value '{marginText}'.");
                    }
                }
                else
                {
                    marginRow.MarginValue = marginValue;
                }

                result.AddRow(marginRow);
            }

            return result;
        }

        private static bool TryExtractEngagement(string descriptor, out string engagementId, out string? engagementTitle)
        {
            engagementId = string.Empty;
            engagementTitle = null;

            var trimmed = descriptor.Trim();
            var openIndex = trimmed.LastIndexOf('(');
            var closeIndex = trimmed.LastIndexOf(')');

            if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex + 1)
            {
                return false;
            }

            var candidate = trimmed[(openIndex + 1)..closeIndex].Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            engagementId = candidate;
            var titleCandidate = trimmed[..openIndex].Trim();
            engagementTitle = StringNormalizer.TrimToNull(titleCandidate);
            return true;
        }

        private static bool TryReadMarginCell(IXLCell cell, out decimal? normalized)
        {
            normalized = null;
            if (cell == null)
            {
                return false;
            }

            if (ExcelParsingUtilities.TryGetNullableDecimal(cell, out var parsed))
            {
                if (parsed.HasValue)
                {
                    normalized = NormalizeMargin(parsed.Value);
                }

                return true;
            }

            var rawText = GetCellString(cell);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return true;
            }

            var sanitized = rawText.Replace("%", string.Empty, StringComparison.Ordinal).Trim();
            foreach (var culture in SupportedCultures)
            {
                if (decimal.TryParse(sanitized, NumberStyles.Any, culture, out var parsedValue))
                {
                    normalized = NormalizeMargin(parsedValue);
                    return true;
                }
            }

            return false;
        }

        private static decimal NormalizeMargin(decimal value)
        {
            if (value > 1m)
            {
                value /= 100m;
            }

            return Math.Round(value, 6, MidpointRounding.AwayFromZero);
        }
    }
}
