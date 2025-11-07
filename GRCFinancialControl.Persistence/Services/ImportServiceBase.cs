using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExcelDataReader;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static GRCFinancialControl.Persistence.Services.Utilities.DataNormalizationService;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Abstract base class for all import services.
    /// Provides common Excel loading, parsing, normalization, and transaction management.
    /// 
    /// Child classes implement specific import logic for each file type:
    /// - FullManagementDataImporter
    /// - BudgetImporter  
    /// - AllocationPlanningImporter
    /// </summary>
    public abstract class ImportServiceBase
    {
        protected readonly IDbContextFactory<ApplicationDbContext> ContextFactory;
        protected readonly ILogger Logger;

        static ImportServiceBase()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        protected ImportServiceBase(
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

            public IWorksheet? GetWorksheet(string name)
            {
                var table = _dataSet.Tables[name];
                return table != null ? new WorksheetAdapter(table) : null;
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
