using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using ExcelDataReader;
using GRCFinancialControl.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    /// <summary>
    /// Base class for all Excel workbook importers.
    /// Provides common functionality for loading workbooks, parsing data, and managing transactions.
    /// </summary>
    public abstract class BaseImporter
    {
        protected readonly IDbContextFactory<ApplicationDbContext> ContextFactory;
        protected readonly ILogger Logger;

        protected BaseImporter(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger logger)
        {
            ContextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        static BaseImporter()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Loads an Excel workbook from the specified file path.
        /// </summary>
        protected static DataSet LoadWorkbook(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Workbook file could not be found.", filePath);
            }

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            return reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });
        }

        /// <summary>
        /// Finds the first worksheet matching any of the provided names (case-insensitive).
        /// </summary>
        protected static DataTable? FindWorksheet(DataSet dataSet, params string[] worksheetNames)
        {
            if (dataSet == null || worksheetNames == null || worksheetNames.Length == 0)
            {
                return null;
            }

            foreach (DataTable? table in dataSet.Tables)
            {
                if (table == null)
                {
                    continue;
                }

                var tableName = table.TableName?.Trim() ?? string.Empty;
                foreach (var worksheetName in worksheetNames)
                {
                    if (string.Equals(tableName, worksheetName, StringComparison.OrdinalIgnoreCase))
                    {
                        return table;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the first column index matching any of the provided header candidates.
        /// </summary>
        protected static int FindColumnIndex(DataTable worksheet, int headerRowIndex, params string[] headerCandidates)
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
                var cellText = NormalizeHeader(Convert.ToString(cellValue, CultureInfo.InvariantCulture));

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
        /// Normalizes a header string for comparison (lowercase, whitespace collapsed).
        /// </summary>
        protected static string NormalizeHeader(string? header)
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
        /// Gets a cell value as a string with whitespace normalized.
        /// </summary>
        protected static string GetCellString(DataTable worksheet, int rowIndex, int columnIndex)
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
        /// Converts a cell value to a normalized string.
        /// </summary>
        protected static string GetString(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DBNull => string.Empty,
                string s => s.Trim(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture).Trim(),
                _ => value.ToString()?.Trim() ?? string.Empty
            };
        }

        /// <summary>
        /// Normalizes whitespace in a string (collapses multiple spaces to one).
        /// </summary>
        protected static string NormalizeWhitespace(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // Replace multiple whitespace with single space
            var normalized = value.Trim();
            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return normalized;
        }

        /// <summary>
        /// Tries to parse a decimal value from a cell.
        /// </summary>
        protected static decimal? TryParseDecimal(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            if (value is decimal decimalValue)
            {
                return decimalValue;
            }

            if (value is double doubleValue)
            {
                return Convert.ToDecimal(doubleValue);
            }

            if (value is float floatValue)
            {
                return Convert.ToDecimal(floatValue);
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            var text = GetString(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Try invariant culture first
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantResult))
            {
                return invariantResult;
            }

            // Try pt-BR culture
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out var ptResult))
            {
                return ptResult;
            }

            return null;
        }

        /// <summary>
        /// Tries to parse a date value from a cell.
        /// </summary>
        protected static DateTime? TryParseDate(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            if (value is DateTime dateTime)
            {
                return dateTime.Date;
            }

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

            // Try various date formats
            var formats = new[]
            {
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd",
                "MM/dd/yyyy",
                "dd-MM-yyyy"
            };

            var cultures = new[]
            {
                CultureInfo.InvariantCulture,
                CultureInfo.GetCultureInfo("pt-BR")
            };

            foreach (var culture in cultures)
            {
                if (DateTime.TryParse(text, culture, DateTimeStyles.AssumeLocal, out var parsed))
                {
                    return parsed.Date;
                }

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(text, format, culture, DateTimeStyles.AssumeLocal, out var exactParsed))
                    {
                        return exactParsed.Date;
                    }
                }
            }

            return null;
        }
    }
}
