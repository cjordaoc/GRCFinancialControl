using System;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;
using GRCFinancialControl.Common;

namespace GRCFinancialControl.Parsing
{
    internal static class ExcelParsingUtilities
    {
        private static readonly CultureInfo PtBr = new("pt-BR");

        public static string NormalizeHeader(string header)
        {
            var normalized = StringNormalizer.NormalizeName(header ?? string.Empty);
            var cleaned = new string(normalized.Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').ToArray());
            return cleaned;
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
                var dt = DateTime.FromOADate(cell.GetDouble());
                date = DateOnly.FromDateTime(dt);
                return true;
            }

            var raw = cell.GetValue<string>()?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                date = default;
                return false;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                date = DateOnly.FromDateTime(parsed);
                return true;
            }

            if (DateTime.TryParse(raw, PtBr, DateTimeStyles.AssumeLocal, out parsed))
            {
                date = DateOnly.FromDateTime(parsed);
                return true;
            }

            date = default;
            return false;
        }
    }
}
