using System;
using System.Data;
using System.IO;
using System.Text;
using ExcelDataReader;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    /// <summary>
    /// Base class for all Excel workbook importers.
    /// Provides common functionality for loading workbooks, parsing data, and managing transactions.
    /// Delegates all normalization/parsing operations to DataNormalizationService for consistency.
    /// </summary>
    public abstract class BaseImporter
    {
        protected readonly IDbContextFactory<ApplicationDbContext> ContextFactory;
        protected readonly ILogger Logger;

        /// <summary>
        /// Centralized normalization service - use this for all data parsing and normalization.
        /// </summary>
        protected static readonly DataNormalizationService Normalize = null!; // Static class reference

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

        // All normalization methods are now in DataNormalizationService
        // Use DataNormalizationService.MethodName() directly for all data normalization needs
        // Examples:
        //   - DataNormalizationService.NormalizeWhitespace(value)
        //   - DataNormalizationService.TryParseDecimal(value)
        //   - DataNormalizationService.FindColumnIndex(worksheet, headerRow, candidates)
        //   - DataNormalizationService.GetCellString(worksheet, row, col)
    }
}
