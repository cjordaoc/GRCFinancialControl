using System;
using System.Globalization;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using GRCFinancialControl.Common;

namespace GRCFinancialControl.Parsing
{
    internal static class ExcelParsingUtilities
    {
        private static readonly CultureInfo PtBr = new("pt-BR");
        private const double OleAutomationMinValue = -657435.0;
        private const double OleAutomationMaxValue = 2958466.0;

        public static string NormalizeHeader(string header)
        {
            var normalized = StringNormalizer.NormalizeName(header ?? string.Empty);
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                }
                else if (ch == '%')
                {
                    builder.Append("PCT");
                }
            }

            return builder.ToString();
        }

        public static string GetCellString(IXLCell cell)
        {
            if (cell == null)
            {
                return string.Empty;
            }

            return cell.GetValue<string>()?.Trim() ?? string.Empty;
        }

        public static bool IsRowEmpty(IXLRow row)
        {
            return !row.CellsUsed().Any(c => !string.IsNullOrWhiteSpace(c.GetValue<string>()));
        }

        public static bool TryGetDecimal(IXLCell cell, out decimal value)
        {
            if (cell == null)
            {
                value = 0m;
                return false;
            }

            if (cell.DataType == XLDataType.Number || cell.DataType == XLDataType.DateTime)
            {
                value = Convert.ToDecimal(cell.GetDouble());
                return true;
            }

            var raw = cell.GetValue<string>()?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                value = 0m;
                return false;
            }

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (decimal.TryParse(raw, NumberStyles.Any, PtBr, out value))
            {
                return true;
            }

            value = 0m;
            return false;
        }

        public static bool TryGetNullableDecimal(IXLCell cell, out decimal? value)
        {
            if (cell == null)
            {
                value = null;
                return false;
            }

            if (cell.IsEmpty())
            {
                value = null;
                return true;
            }

            if (TryGetDecimal(cell, out var parsed))
            {
                value = parsed;
                return true;
            }

            value = null;
            return false;
        }

        public static bool TryGetDate(IXLCell cell, out DateOnly date)
        {
            if (cell == null)
            {
                date = default;
                return false;
            }

            if (cell.DataType == XLDataType.DateTime)
            {
                var dt = cell.GetDateTime();
                date = DateOnly.FromDateTime(dt);
                return true;
            }

            if (cell.DataType == XLDataType.Number)
            {
                var raw = cell.GetDouble();
                if (double.IsNaN(raw) || double.IsInfinity(raw) || raw < OleAutomationMinValue || raw > OleAutomationMaxValue)
                {
                    date = default;
                    return false;
                }

                try
                {
                    var dt = DateTime.FromOADate(raw);
                    date = DateOnly.FromDateTime(dt);
                    return true;
                }
                catch (ArgumentException)
                {
                    date = default;
                    return false;
                }
            }

            var raw = cell.GetValue<string>()?.Trim();
            return TryParseDateFromText(raw, out date);
        }

        public static bool TryParseDateFromText(string? value, out DateOnly date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                date = DateOnly.FromDateTime(parsed);
                return true;
            }

            if (DateTime.TryParse(value, PtBr, DateTimeStyles.AssumeLocal, out parsed))
            {
                date = DateOnly.FromDateTime(parsed);
                return true;
            }

            return false;
        }

        public static bool TryGetInt(IXLCell cell, out int value)
        {
            if (cell == null)
            {
                value = 0;
                return false;
            }

            if (cell.DataType == XLDataType.Number)
            {
                value = Convert.ToInt32(Math.Round(cell.GetDouble(), MidpointRounding.AwayFromZero));
                return true;
            }

            var raw = cell.GetValue<string>()?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                value = 0;
                return false;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (int.TryParse(raw, NumberStyles.Integer, PtBr, out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        public static bool TryGetNullableInt(IXLCell cell, out int? value)
        {
            if (cell == null)
            {
                value = null;
                return false;
            }

            if (cell.IsEmpty())
            {
                value = null;
                return true;
            }

            if (TryGetInt(cell, out var parsed))
            {
                value = parsed;
                return true;
            }

            value = null;
            return false;
        }

        public static bool TryGetPercentage(IXLCell cell, out decimal? value)
        {
            value = null;

            if (cell == null)
            {
                return false;
            }

            if (TryGetNullableDecimal(cell, out var numeric) && numeric.HasValue)
            {
                value = NormalizePercentage(numeric.Value);
                return true;
            }

            var raw = GetCellString(cell);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            var sanitized = raw.Replace("%", string.Empty, StringComparison.Ordinal).Trim();
            if (decimal.TryParse(sanitized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ||
                decimal.TryParse(sanitized, NumberStyles.Any, PtBr, out parsed))
            {
                value = NormalizePercentage(parsed);
                return true;
            }

            return false;
        }

        public static string GetCombinedHeaderText(IXLWorksheet worksheet, int columnNumber, int headerRow)
        {
            ArgumentNullException.ThrowIfNull(worksheet);

            if (headerRow < 1)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var row = 1; row <= headerRow; row++)
            {
                var cellValue = worksheet.Cell(row, columnNumber).GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(cellValue))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(cellValue);
            }

            return builder.ToString();
        }

        private static decimal NormalizePercentage(decimal value)
        {
            if (Math.Abs(value) > 1m)
            {
                value /= 100m;
            }

            return Math.Round(value, 6, MidpointRounding.AwayFromZero);
        }
    }
}
