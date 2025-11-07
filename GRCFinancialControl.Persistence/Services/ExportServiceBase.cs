using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using ExcelDataReader;
using GRCFinancialControl.Persistence.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static GRCFinancialControl.Persistence.Services.Utilities.DataNormalizationService;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Abstract base class for all export services.
    /// Provides common Excel loading, parsing, normalization, and data access functionality.
    /// 
    /// Child classes implement specific export logic for each export type:
    /// - RetainTemplateGenerator
    /// - Future exporters
    /// </summary>
    public abstract class ExportServiceBase
    {
        protected readonly IDbContextFactory<ApplicationDbContext> ContextFactory;
        protected readonly ILogger Logger;

        static ExportServiceBase()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        protected ExportServiceBase(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger logger)
        {
            ContextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Common Excel Loading

        /// <summary>
        /// Loads an Excel workbook from file path with shared read access.
        /// </summary>
        protected static WorkbookData LoadWorkbook(string filePath)
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
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });

            return new WorkbookData(dataSet);
        }

        #endregion

        #region Helper Methods for Child Classes

        /// <summary>
        /// Rounds a money value to 2 decimal places.
        /// </summary>
        protected static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Gets cell value from worksheet with bounds checking.
        /// </summary>
        protected static object? GetCellValue(IWorksheet worksheet, int rowIndex, int columnIndex)
        {
            if (worksheet == null || rowIndex < 0 || rowIndex >= worksheet.RowCount)
            {
                return null;
            }

            if (columnIndex < 0 || columnIndex >= worksheet.ColumnCount)
            {
                return null;
            }

            return worksheet.GetValue(rowIndex, columnIndex);
        }

        /// <summary>
        /// Gets cell string from worksheet with normalization.
        /// </summary>
        protected static string GetCellString(IWorksheet worksheet, int rowIndex, int columnIndex)
        {
            var value = GetCellValue(worksheet, rowIndex, columnIndex);
            return GetString(value);
        }

        /// <summary>
        /// Parses hours value from cell (handles different numeric types).
        /// </summary>
        protected static (decimal Hours, bool HasValue) ParseHours(object? value)
        {
            var parsed = TryParseDecimal(value);
            if (!parsed.HasValue)
            {
                return (0m, false);
            }

            return (parsed.Value, true);
        }

        /// <summary>
        /// Parses decimal with optional rounding.
        /// </summary>
        protected static decimal? ParseDecimal(object? value, int? roundDigits = null)
        {
            return TryParseDecimal(value, roundDigits);
        }

        /// <summary>
        /// Checks if a row is empty (all cells blank).
        /// </summary>
        protected static bool IsRowEmpty(IWorksheet worksheet, int rowIndex)
        {
            if (worksheet == null || rowIndex < 0 || rowIndex >= worksheet.RowCount)
            {
                return true;
            }

            for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
            {
                if (!IsBlank(GetCellValue(worksheet, rowIndex, columnIndex)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Finds column index matching any of the provided header candidates.
        /// </summary>
        protected static int GetRequiredColumnIndex(
            IReadOnlyDictionary<string, int> headerMap,
            string[] headerCandidates,
            string displayName)
        {
            foreach (var candidate in headerCandidates)
            {
                if (headerMap.TryGetValue(NormalizeHeader(candidate), out var index))
                {
                    return index;
                }
            }

            throw new InvalidDataException($"Required column '{displayName}' not found in worksheet headers.");
        }

        /// <summary>
        /// Finds column index for optional header.
        /// </summary>
        protected static int GetOptionalColumnIndex(
            IReadOnlyDictionary<string, int> headerMap,
            string headerName)
        {
            if (headerMap.TryGetValue(NormalizeHeader(headerName), out var index))
            {
                return index;
            }

            return -1;
        }

        /// <summary>
        /// Finds column index matching any of the provided header candidates.
        /// Returns -1 if not found.
        /// </summary>
        protected static int FindColumnIndex(IWorksheet worksheet, int headerRowIndex, IReadOnlyList<string> headerCandidates)
        {
            if (worksheet == null || headerRowIndex < 0 || headerCandidates == null || headerCandidates.Count == 0)
            {
                return -1;
            }

            var normalizedCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in headerCandidates)
            {
                var normalized = NormalizeHeader(candidate);
                if (!string.IsNullOrEmpty(normalized))
                {
                    normalizedCandidates.Add(normalized);
                }
            }

            if (normalizedCandidates.Count == 0)
            {
                return -1;
            }

            for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
            {
                var header = NormalizeHeader(GetCellString(worksheet, headerRowIndex, columnIndex));
                if (!string.IsNullOrEmpty(header) && normalizedCandidates.Contains(header))
                {
                    return columnIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds the first non-empty row in the worksheet (typically the header row).
        /// </summary>
        protected static int FindHeaderRowIndex(IWorksheet worksheet)
        {
            if (worksheet == null)
            {
                return -1;
            }

            for (var rowIndex = 0; rowIndex < worksheet.RowCount; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
                {
                    if (!string.IsNullOrWhiteSpace(GetCellString(worksheet, rowIndex, columnIndex)))
                    {
                        return rowIndex;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Builds a header map from worksheet row (column name → index).
        /// </summary>
        protected static Dictionary<string, int> BuildHeaderMap(IWorksheet table, int headerRowIndex)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var columnIndex = 0; columnIndex < table.ColumnCount; columnIndex++)
            {
                var header = NormalizeWhitespace(Convert.ToString(table.GetValue(headerRowIndex, columnIndex), CultureInfo.InvariantCulture));
                if (string.IsNullOrEmpty(header))
                {
                    continue;
                }

                var normalized = NormalizeHeader(header);
                if (!map.ContainsKey(normalized))
                {
                    map[normalized] = columnIndex;
                }
            }

            return map;
        }

        /// <summary>
        /// Checks if a worksheet name matches allocation worksheet naming patterns.
        /// </summary>
        protected static bool IsAllocationWorksheet(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return string.Equals(name, "Alocações_Staff", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Alocacoes_Staff", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the start of the week (Monday) for a given date.
        /// </summary>
        protected static DateTime StartOfWeek(DateTime date)
        {
            var monday = date.Date;
            var offset = (int)monday.DayOfWeek - (int)DayOfWeek.Monday;
            if (offset < 0)
            {
                offset += 7;
            }

            return monday.AddDays(-offset);
        }

        #endregion

        #region Workbook Data Wrapper

        /// <summary>
        /// Wrapper around Excel DataSet for simplified access.
        /// </summary>
        protected class WorkbookData : IDisposable
        {
            private readonly DataSet _dataSet;

            public WorkbookData(DataSet dataSet)
            {
                _dataSet = dataSet ?? throw new ArgumentNullException(nameof(dataSet));
            }

            public static WorkbookData From(DataTable table)
            {
                if (table == null)
                {
                    throw new ArgumentNullException(nameof(table));
                }

                var dataSet = new DataSet();
                dataSet.Tables.Add(table.Copy());
                return new WorkbookData(dataSet);
            }

            public IWorksheet? GetWorksheet(string name)
            {
                var table = _dataSet.Tables[name];
                return table != null ? new WorksheetAdapter(table) : null;
            }

            public int RowCount => _dataSet.Tables.Count > 0 ? _dataSet.Tables[0]!.Rows.Count : 0;
            public int ColumnCount => _dataSet.Tables.Count > 0 ? _dataSet.Tables[0]!.Columns.Count : 0;

            public object? GetValue(int rowIndex, int columnIndex)
            {
                if (_dataSet.Tables.Count == 0)
                {
                    return null;
                }

                var table = _dataSet.Tables[0];
                if (table == null || rowIndex < 0 || rowIndex >= table.Rows.Count ||
                    columnIndex < 0 || columnIndex >= table.Columns.Count)
                {
                    return null;
                }

                var value = table.Rows[rowIndex][columnIndex];
                return value == DBNull.Value ? null : value;
            }

            public void Dispose()
            {
                _dataSet?.Dispose();
            }

            public IWorksheet? FirstWorksheet
            {
                get
                {
                    if (_dataSet.Tables.Count == 0)
                    {
                        return null;
                    }

                    var table = _dataSet.Tables[0];
                    return table != null ? new WorksheetAdapter(table) : null;
                }
            }

            public IEnumerable<IWorksheet> Worksheets
            {
                get
                {
                    foreach (DataTable? table in _dataSet.Tables)
                    {
                        if (table != null)
                        {
                            yield return new WorksheetAdapter(table);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Worksheet interface for abstraction.
        /// </summary>
        protected interface IWorksheet
        {
            int RowCount { get; }
            int ColumnCount { get; }
            object? GetValue(int rowIndex, int columnIndex);
            string Name { get; }
        }

        /// <summary>
        /// Adapter for DataTable to IWorksheet.
        /// </summary>
        private class WorksheetAdapter : IWorksheet
        {
            private readonly DataTable _table;

            public WorksheetAdapter(DataTable table)
            {
                _table = table ?? throw new ArgumentNullException(nameof(table));
            }

            public int RowCount => _table.Rows.Count;
            public int ColumnCount => _table.Columns.Count;
            public string Name => _table.TableName ?? string.Empty;

            public object? GetValue(int rowIndex, int columnIndex)
            {
                if (rowIndex < 0 || rowIndex >= _table.Rows.Count)
                {
                    return null;
                }

                if (columnIndex < 0 || columnIndex >= _table.Columns.Count)
                {
                    return null;
                }

                var value = _table.Rows[rowIndex][columnIndex];
                return value == DBNull.Value ? null : value;
            }
        }

        #endregion
    }
}
