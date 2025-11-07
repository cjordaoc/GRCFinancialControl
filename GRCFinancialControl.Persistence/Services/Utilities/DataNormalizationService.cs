using System;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GRCFinancialControl.Persistence.Services.Utilities
{
    /// <summary>
    /// Centralized service for all data normalization, parsing, and extraction operations.
    /// Provides consistent handling of Excel cell values, strings, numbers, dates, and business identifiers.
    /// 
    /// Design rationale: Consolidates normalization logic previously scattered across
    /// WorksheetValueHelper, BaseImporter, ImportService, and other services into a single
    /// source of truth for data transformation.
    /// </summary>
    public static class DataNormalizationService
    {
        #region Regex Patterns

        private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex EngagementCodeRegex = new(@"\bE-\d+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FiscalYearCodeRegex = new(@"FY\d{2,4}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DigitsOnlyRegex = new(@"\d+", RegexOptions.Compiled);
        private static readonly Regex TrailingDigitsRegex = new(@"\d+$", RegexOptions.Compiled);

        #endregion

        #region Culture Settings

        private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        private static readonly CultureInfo[] DateCultures =
        {
            InvariantCulture,
            PtBrCulture
        };

        private static readonly string[] DateFormats =
        {
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd/MM/yy",
            "d/M/yy",
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "MM/dd/yyyy",
            "dd-MM-yyyy",
            "d-M-yyyy"
        };

        #endregion

        #region String Normalization

        /// <summary>
        /// Collapses multiple whitespace characters into a single space and trims.
        /// </summary>
        public static string NormalizeWhitespace(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return MultiWhitespaceRegex.Replace(value.Trim(), " ");
        }

        /// <summary>
        /// Normalizes a string to uppercase invariant culture for case-insensitive comparisons.
        /// </summary>
        public static string NormalizeToUpperInvariant(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Normalizes a header string for column matching (lowercase, whitespace collapsed, special chars removed).
        /// </summary>
        public static string NormalizeHeader(string? header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return string.Empty;
            }

            var normalized = header
                .Replace("_", " ", StringComparison.Ordinal)
                .Replace("-", " ", StringComparison.Ordinal)
                .Replace("  ", " ", StringComparison.Ordinal)
                .Trim();

            return normalized.ToLowerInvariant();
        }

        /// <summary>
        /// Normalizes an optional string value, returning null if blank.
        /// </summary>
        public static string? NormalizeOptional(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = NormalizeWhitespace(value);
            return string.IsNullOrEmpty(normalized) ? null : normalized;
        }

        #endregion

        #region Cell Value Extraction

        /// <summary>
        /// Checks if a cell value is blank (null, DBNull, or whitespace string).
        /// </summary>
        public static bool IsBlank(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return true;
            }

            if (value is string text)
            {
                return string.IsNullOrWhiteSpace(text);
            }

            return false;
        }

        /// <summary>
        /// Converts a cell value to a normalized string.
        /// </summary>
        public static string GetString(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DBNull => string.Empty,
                string s => s.Trim(),
                IFormattable formattable => formattable.ToString(null, InvariantCulture).Trim(),
                _ => value.ToString()?.Trim() ?? string.Empty
            };
        }

        /// <summary>
        /// Gets a cell value from a DataTable with bounds checking.
        /// </summary>
        public static string GetCellString(DataTable worksheet, int rowIndex, int columnIndex)
        {
            if (worksheet == null || rowIndex < 0 || rowIndex >= worksheet.Rows.Count)
            {
                return string.Empty;
            }

            if (columnIndex < 0 || columnIndex >= worksheet.Columns.Count)
            {
                return string.Empty;
            }

            var cellValue = worksheet.Rows[rowIndex][columnIndex];
            return GetString(cellValue);
        }

        /// <summary>
        /// Gets a cell value for display purposes (formats dates and numbers consistently).
        /// </summary>
        public static string GetDisplayText(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DBNull => string.Empty,
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd", InvariantCulture),
                IFormattable formattable => formattable.ToString(null, InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }

        #endregion

        #region Number Parsing

        /// <summary>
        /// Tries to parse a decimal value from a cell using multiple cultures.
        /// </summary>
        public static decimal? TryParseDecimal(object? value, int? roundDigits = null)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            decimal result;

            // Handle numeric types directly
            switch (value)
            {
                case decimal decimalValue:
                    result = decimalValue;
                    break;
                case double doubleValue:
                    result = Convert.ToDecimal(doubleValue);
                    break;
                case float floatValue:
                    result = Convert.ToDecimal(floatValue);
                    break;
                case int intValue:
                    result = intValue;
                    break;
                case long longValue:
                    result = longValue;
                    break;
                default:
                {
                    var text = GetString(value);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return null;
                    }

                    // Try invariant culture first
                    if (decimal.TryParse(text, NumberStyles.Any, InvariantCulture, out var invariantResult))
                    {
                        result = invariantResult;
                    }
                    // Try pt-BR culture
                    else if (decimal.TryParse(text, NumberStyles.Any, PtBrCulture, out var ptResult))
                    {
                        result = ptResult;
                    }
                    else
                    {
                        return null;
                    }

                    break;
                }
            }

            // Apply rounding if specified
            if (roundDigits.HasValue)
            {
                result = Math.Round(result, roundDigits.Value, MidpointRounding.AwayFromZero);
            }

            return result;
        }

        /// <summary>
        /// Tries to parse an integer value from a cell.
        /// </summary>
        public static int? TryParseInt(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue && longValue <= int.MaxValue && longValue >= int.MinValue)
            {
                return (int)longValue;
            }

            if (value is decimal decimalValue && decimalValue <= int.MaxValue && decimalValue >= int.MinValue)
            {
                return (int)decimalValue;
            }

            var text = GetString(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (int.TryParse(text, NumberStyles.Integer, InvariantCulture, out var result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Rounds a money value to 2 decimal places.
        /// </summary>
        public static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        #endregion

        #region Date Parsing

        /// <summary>
        /// Tries to parse a date value from a cell using multiple formats and cultures.
        /// </summary>
        public static DateTime? TryParseDate(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            // Handle DateTime directly
            if (value is DateTime dateTime)
            {
                return dateTime.Date;
            }

            // Handle OLE Automation date (Excel serial date)
            if (value is double serialDate)
            {
                try
                {
                    return DateTime.FromOADate(serialDate).Date;
                }
                catch
                {
                    return null;
                }
            }

            var text = GetString(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Try general parsing first
            foreach (var culture in DateCultures)
            {
                if (DateTime.TryParse(text, culture, DateTimeStyles.AssumeLocal, out var parsed))
                {
                    return parsed.Date;
                }
            }

            // Try explicit formats
            foreach (var format in DateFormats)
            {
                foreach (var culture in DateCultures)
                {
                    if (DateTime.TryParseExact(text, format, culture, DateTimeStyles.AssumeLocal, out var exactParsed))
                    {
                        return exactParsed.Date;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Normalizes a date to the start of its week (Monday).
        /// </summary>
        public static DateTime NormalizeWeekStart(DateTime date)
        {
            var truncated = date.Date;
            var offset = ((int)truncated.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return truncated.AddDays(-offset);
        }

        #endregion

        #region Business Identifier Extraction

        /// <summary>
        /// Tries to extract an engagement code (format: E-NNNN) from a cell value.
        /// </summary>
        public static string? TryExtractEngagementCode(object? value)
        {
            if (value is null)
            {
                return null;
            }

            string text = value is string s
                ? s
                : Convert.ToString(value, InvariantCulture) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = EngagementCodeRegex.Match(text);
            return match.Success ? match.Value.ToUpperInvariant() : null;
        }

        /// <summary>
        /// Tries to extract a fiscal year code (format: FY2024) from a string.
        /// </summary>
        public static string? TryExtractFiscalYearCode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var match = FiscalYearCodeRegex.Match(value);
            return match.Success ? match.Value.ToUpperInvariant() : null;
        }

        /// <summary>
        /// Extracts all digits from a string.
        /// </summary>
        public static string ExtractDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var matches = DigitsOnlyRegex.Matches(value);
            if (matches.Count == 0)
            {
                return string.Empty;
            }

            return string.Concat(matches);
        }

        /// <summary>
        /// Tries to extract digits from a string and parse as integer.
        /// </summary>
        public static bool TryExtractDigits(string value, out int numericValue)
        {
            var digits = ExtractDigits(value);
            if (string.IsNullOrEmpty(digits))
            {
                numericValue = 0;
                return false;
            }

            return int.TryParse(digits, NumberStyles.Integer, InvariantCulture, out numericValue);
        }

        /// <summary>
        /// Extracts trailing digits from a string (e.g., "FY2024" -> "2024").
        /// </summary>
        public static string? ExtractTrailingDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var match = TrailingDigitsRegex.Match(value);
            return match.Success ? match.Value : null;
        }

        #endregion

        #region Column/Header Finding

        /// <summary>
        /// Finds the first column index matching any of the provided header candidates.
        /// </summary>
        public static int FindColumnIndex(DataTable worksheet, int headerRowIndex, params string[] headerCandidates)
        {
            if (worksheet == null || headerRowIndex < 0 || headerRowIndex >= worksheet.Rows.Count)
            {
                return -1;
            }

            if (headerCandidates == null || headerCandidates.Length == 0)
            {
                return -1;
            }

            var headerRow = worksheet.Rows[headerRowIndex];
            for (var columnIndex = 0; columnIndex < worksheet.Columns.Count; columnIndex++)
            {
                var cellValue = headerRow[columnIndex];
                var cellText = NormalizeHeader(Convert.ToString(cellValue, InvariantCulture));

                if (string.IsNullOrEmpty(cellText))
                {
                    continue;
                }

                foreach (var candidate in headerCandidates)
                {
                    if (string.Equals(cellText, NormalizeHeader(candidate), StringComparison.OrdinalIgnoreCase))
                    {
                        return columnIndex;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Checks if a row is empty (all cells are blank).
        /// </summary>
        public static bool IsRowEmpty(DataTable worksheet, int rowIndex)
        {
            if (worksheet == null || rowIndex < 0 || rowIndex >= worksheet.Rows.Count)
            {
                return true;
            }

            var row = worksheet.Rows[rowIndex];
            for (var columnIndex = 0; columnIndex < worksheet.Columns.Count; columnIndex++)
            {
                if (!IsBlank(row[columnIndex]))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Specialized Normalization

        /// <summary>
        /// Normalizes a rank name for comparison (uppercase, whitespace collapsed).
        /// </summary>
        public static string NormalizeRank(string? value)
        {
            return NormalizeToUpperInvariant(value);
        }

        /// <summary>
        /// Normalizes a code (engagement, customer, etc.) for lookup (uppercase, whitespace collapsed).
        /// </summary>
        public static string NormalizeCode(string? value)
        {
            return NormalizeToUpperInvariant(value);
        }

        /// <summary>
        /// Normalizes an identifier (GPN, employee ID, etc.) by removing common prefixes and standardizing format.
        /// </summary>
        public static string NormalizeIdentifier(object? value)
        {
            var text = GetString(value);
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Remove common prefixes
            text = text
                .Replace("GUI-", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("MRS-", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("EMP-", string.Empty, StringComparison.OrdinalIgnoreCase);

            return NormalizeWhitespace(text);
        }

        /// <summary>
        /// Normalizes a closing period identifier.
        /// </summary>
        public static string? NormalizeClosingPeriodId(string? closingPeriodId)
        {
            return string.IsNullOrWhiteSpace(closingPeriodId)
                ? null
                : closingPeriodId.Trim();
        }

        #endregion
    }
}
