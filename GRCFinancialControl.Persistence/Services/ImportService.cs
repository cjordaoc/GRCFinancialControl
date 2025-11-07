using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExcelDataReader;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Extensions;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static GRCFinancialControl.Persistence.Services.Importers.WorksheetValueHelper;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Imports budget, FCS backlog, hours allocation, and planned allocation data from Excel workbooks.
    /// </summary>
    public class ImportService : IImportService
    {
        private static readonly FileStreamOptions SharedReadOptions = new()
        {
            Access = FileAccess.Read,
            Mode = FileMode.Open,
            Share = FileShare.ReadWrite,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        };

        static ImportService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<ImportService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IFiscalCalendarConsistencyService _fiscalCalendarConsistencyService;
        private readonly IFullManagementDataImporter _fullManagementDataImporter;
        private const int FcsHeaderSearchLimit = 20;
        private const int FcsDataStartRowIndex = 11; // Default row 12 in Excel (1-based)
        private const int FullManagementHeaderRowIndex = 10;
        private const int FullManagementDataStartRowIndex = 11;
        private static readonly string[] FcsEngagementIdHeaders =
        {
            "engagement id",
            "project id"
        };
        private static readonly string[] FcsCurrentFiscalYearBacklogHeaders =
        {
            "fytg backlog",
            "fiscal year to go backlog"
        };
        private static readonly string[] FcsFutureFiscalYearBacklogHeaders =
        {
            "future fy backlog",
            "future fiscal year backlog"
        };
        private static readonly Regex DigitsRegex = new Regex(@"\d+", RegexOptions.Compiled);
        private static readonly Regex TrailingDigitsRegex = new Regex(@"\d+$", RegexOptions.Compiled);
        private static readonly Regex FiscalYearCodeRegex = new Regex(@"FY\d{2,4}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EngagementIdRegex = new Regex(@"\bE-\d+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LastUpdateDateRegex = new Regex(@"Last Update\s*:\s*(\d{1,2}\s+[A-Za-z]{3}\s+\d{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");
        private const string MissingRankWarningKey = "MissingRankMapping";
        private const string MissingEngagementWarningKey = "MissingEngagement";
        private const string MissingBudgetWarningKey = "MissingBudget";

        public ImportService(IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<ImportService> logger,
            ILoggerFactory loggerFactory,
            IFiscalCalendarConsistencyService fiscalCalendarConsistencyService,
            IFullManagementDataImporter fullManagementDataImporter)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(fiscalCalendarConsistencyService);
            ArgumentNullException.ThrowIfNull(fullManagementDataImporter);

            _contextFactory = contextFactory;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _fiscalCalendarConsistencyService = fiscalCalendarConsistencyService;
            _fullManagementDataImporter = fullManagementDataImporter;
        }

        public async Task<string> ImportBudgetAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Budget workbook could not be found.", filePath);
            }

            await _fiscalCalendarConsistencyService.EnsureConsistencyAsync().ConfigureAwait(false);

            using var workbook = LoadWorkbook(filePath);

            var planInfo = workbook.GetWorksheet("PLAN INFO") ??
                           throw new InvalidDataException("Worksheet 'PLAN INFO' is missing from the budget workbook.");
            var resourcing = ResolveResourcingWorksheet(workbook);

            var customerName = NormalizeWhitespace(GetCellString(planInfo, 3, 1));
            var engagementKey = NormalizeWhitespace(GetCellString(planInfo, 4, 1));
            var descriptionRaw = NormalizeWhitespace(GetCellString(planInfo, 0, 0));

            if (string.IsNullOrWhiteSpace(customerName))
            {
                throw new InvalidDataException("PLAN INFO!B4 (Client) must contain a customer name.");
            }

            if (string.IsNullOrWhiteSpace(engagementKey))
            {
                throw new InvalidDataException("PLAN INFO!B5 (Project ID) must contain an engagement identifier.");
            }

            var engagementDescription = ExtractDescription(descriptionRaw);

            var resourcingParseResult = ParseResourcing(resourcing);
            var aggregatedBudgets = AggregateRankBudgets(resourcingParseResult.RankBudgets);
            var totalBudgetHours = aggregatedBudgets.Sum(r => r.Hours);
            var generatedAtUtc = ExtractGeneratedTimestampUtc(planInfo);

            await using var strategyContext = await _contextFactory
                .CreateDbContextAsync()
                .ConfigureAwait(false);
            var strategy = strategyContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var context = await _contextFactory
                    .CreateDbContextAsync()
                    .ConfigureAwait(false);
                await using var transaction = await context.Database
                    .BeginTransactionAsync()
                    .ConfigureAwait(false);

                try
                {
                    var normalizedCustomerName = customerName;
                    var normalizedCustomerLookup = normalizedCustomerName.ToLowerInvariant();
                    var existingCustomer = await context.Customers
                        .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedCustomerLookup)
                        .ConfigureAwait(false);

                    bool customerCreated = false;
                    Customer customer;
                    if (existingCustomer == null)
                    {
                        customer = new Customer
                        {
                            Name = normalizedCustomerName
                        };
                        await context.Customers.AddAsync(customer).ConfigureAwait(false);
                        customerCreated = true;
                    }
                    else
                    {
                        existingCustomer.Name = normalizedCustomerName;
                        customer = existingCustomer;
                    }

                    await EnsureCustomerCodeAsync(context, customer).ConfigureAwait(false);

                    var engagement = await context.Engagements
                        .Include(e => e.RankBudgets)
                        .FirstOrDefaultAsync(e => e.EngagementId == engagementKey)
                        .ConfigureAwait(false);

                    bool engagementCreated = false;
                    if (engagement == null)
                    {
                        engagement = new Engagement
                        {
                            EngagementId = engagementKey,
                            Description = engagementDescription,
                            InitialHoursBudget = totalBudgetHours,
                            EstimatedToCompleteHours = 0m
                        };

                        await context.Engagements.AddAsync(engagement).ConfigureAwait(false);
                        engagementCreated = true;
                    }

                    else if (EngagementImportSkipEvaluator.TryCreate(engagement, out var skipMetadata))
                    {
                        _logger.LogWarning(skipMetadata.WarningMessage);
                        await transaction.RollbackAsync().ConfigureAwait(false);

                        var skipReasons = new Dictionary<string, IReadOnlyCollection<string>>
                        {
                            [skipMetadata.ReasonKey] = new[] { engagement.EngagementId }
                        };

                        var skipNotes = new List<string>(1)
                        {
                            skipMetadata.WarningMessage
                        };

                        return ImportSummaryFormatter.Build(
                            "Budget import",
                            inserted: 0,
                            updated: 0,
                            skipReasons,
                            skipNotes);
                    }

                    engagement.Description = engagementDescription;
                    engagement.InitialHoursBudget = totalBudgetHours;

                    await UpsertRankMappingsAsync(context, resourcingParseResult.RankMappings, generatedAtUtc)
                        .ConfigureAwait(false);
                    await UpsertEmployeesAsync(context, resourcingParseResult.Employees).ConfigureAwait(false);

                    engagement.Customer = customer;
                    if (customer.Id > 0)
                    {
                        engagement.CustomerId = customer.Id;
                    }

            var fiscalYears = await context.FiscalYears
                .AsNoTracking()
                .OrderBy(fy => fy.StartDate)
                .ToListAsync()
                .ConfigureAwait(false);

                    var insertedBudgets = ApplyBudgetSnapshot(engagement, fiscalYears, aggregatedBudgets, DateTime.UtcNow);

                    await context.SaveChangesAsync().ConfigureAwait(false);
                    await transaction.CommitAsync().ConfigureAwait(false);

                    var customersInserted = customerCreated ? 1 : 0;
                    var customersUpdated = customerCreated ? 0 : 1;
                    var engagementsInserted = engagementCreated ? 1 : 0;
                    var engagementsUpdated = engagementCreated ? 0 : 1;

                    var notes = new List<string>(4)
                    {
                        $"Customers inserted: {customersInserted}, updated: {customersUpdated}",
                        $"Engagements inserted: {engagementsInserted}, updated: {engagementsUpdated}",
                        $"Rank budgets processed: {aggregatedBudgets.Count}",
                        $"Budget entries inserted: {insertedBudgets}",
                        $"Initial hours budget total: {totalBudgetHours:F2}"
                    };

                    if (resourcingParseResult.Issues.Count > 0)
                    {
                        notes.Add($"Notes: {string.Join("; ", resourcingParseResult.Issues)}");
                    }

                    return ImportSummaryFormatter.Build(
                        "Budget import",
                        customersInserted + engagementsInserted,
                        customersUpdated + engagementsUpdated,
                        null,
                        notes);
                }
                catch
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    throw;
                }
            }).ConfigureAwait(false);
        }

        private static IWorksheet ResolveResourcingWorksheet(WorkbookData workbook)
        {
            ArgumentNullException.ThrowIfNull(workbook);

            var resourcing = workbook.GetWorksheet("RESOURCING");
            if (resourcing is not null)
            {
                return resourcing;
            }

            foreach (var worksheet in workbook.Worksheets)
            {
                if (worksheet is null)
                {
                    continue;
                }

                if (FindResourcingHeaderRow(worksheet) >= 0)
                {
                    return worksheet;
                }
            }

            throw new InvalidDataException(
                "Worksheet 'RESOURCING' is missing from the budget workbook and no alternative worksheet with Level and Employee columns could be found.");
        }

        private static ResourcingParseResult ParseResourcing(IWorksheet resourcing)
        {
            ArgumentNullException.ThrowIfNull(resourcing);

            var headerRowIndex = FindResourcingHeaderRow(resourcing);
            if (headerRowIndex < 0)
            {
                throw new InvalidDataException("The RESOURCING worksheet does not contain a header row with Level and Employee columns.");
            }

            var headerMap = BuildHeaderMap(resourcing, headerRowIndex);
            var hoursColumnIndex = FindFirstHeaderColumnIndex(resourcing, headerRowIndex, "H");
            if (hoursColumnIndex < 0)
            {
                throw new InvalidDataException("Unable to locate the first weekly hours column in the RESOURCING worksheet.");
            }

            var weekRowIndex = headerRowIndex > 0 ? headerRowIndex - 1 : headerRowIndex;
            var weekStartDates = ExtractWeekStartDates(resourcing, weekRowIndex, hoursColumnIndex);

            var estimatedRowCapacity = Math.Max(0, resourcing.RowCount - (headerRowIndex + 1));
            var rankBudgets = new List<RankBudgetRow>(estimatedRowCapacity);
            var rankMappings = new Dictionary<string, RankMappingCandidate>(estimatedRowCapacity, StringComparer.OrdinalIgnoreCase);
            var employees = new List<ResourcingEmployee>(estimatedRowCapacity);
            var issues = new List<string>(Math.Max(4, estimatedRowCapacity / 4));

            var levelColumnIndex = GetRequiredColumnIndex(headerMap, "level");
            var employeeColumnIndex = GetRequiredColumnIndex(headerMap, "employee");
            var guiColumnIndex = GetOptionalColumnIndex(headerMap, "gui number");
            var mrsColumnIndex = GetOptionalColumnIndex(headerMap, "mrs");
            var gdsColumnIndex = GetOptionalColumnIndex(headerMap, "gds");
            var costCenterColumnIndex = GetOptionalColumnIndex(headerMap, "cost center");
            var officeColumnIndex = GetOptionalColumnIndex(headerMap, "office");

            var dataRowIndex = headerRowIndex + 1;
            var consecutiveBlankRows = 0;

            while (dataRowIndex < resourcing.RowCount && consecutiveBlankRows < 10)
            {
                var rawRank = NormalizeWhitespace(GetCellString(resourcing, dataRowIndex, levelColumnIndex));
                var (hours, hasHoursValue) = ParseHours(GetCellValue(resourcing, dataRowIndex, hoursColumnIndex));

                var isRowEmpty = string.IsNullOrEmpty(rawRank) && !hasHoursValue;
                if (isRowEmpty)
                {
                    consecutiveBlankRows++;
                    dataRowIndex++;
                    continue;
                }

                consecutiveBlankRows = 0;

                if (string.IsNullOrEmpty(rawRank))
                {
                    if (hours > 0)
                    {
                        issues.Add($"Row {dataRowIndex + 1}: Hours present but rank name missing; skipped.");
                    }

                    dataRowIndex++;
                    continue;
                }

                if (IsResourcingSummaryRow(rawRank))
                {
                    dataRowIndex++;
                    continue;
                }

                rankBudgets.Add(new RankBudgetRow(rawRank, hours));

                if (!rankMappings.ContainsKey(rawRank))
                {
                    rankMappings[rawRank] = new RankMappingCandidate(
                        rawRank,
                        NormalizeRankName(rawRank),
                        DeriveSpreadsheetRankName(rawRank));
                }

                var employeeName = NormalizeWhitespace(GetCellString(resourcing, dataRowIndex, employeeColumnIndex));
                if (!string.IsNullOrEmpty(employeeName))
                {
                    var guiValue = guiColumnIndex >= 0 ? NormalizeIdentifier(GetCellValue(resourcing, dataRowIndex, guiColumnIndex)) : string.Empty;
                    var mrsValue = mrsColumnIndex >= 0 ? NormalizeIdentifier(GetCellValue(resourcing, dataRowIndex, mrsColumnIndex)) : string.Empty;
                    var identifier = DetermineEmployeeIdentifier(guiValue, mrsValue, employeeName);

                    if (!string.IsNullOrEmpty(identifier))
                    {
                        var isContractor = identifier.StartsWith("MRS-", StringComparison.OrdinalIgnoreCase);
                        var costCenter = costCenterColumnIndex >= 0
                            ? NormalizeOptionalString(GetCellString(resourcing, dataRowIndex, costCenterColumnIndex))
                            : null;
                        var office = officeColumnIndex >= 0
                            ? NormalizeOptionalString(GetCellString(resourcing, dataRowIndex, officeColumnIndex))
                            : null;

                        var isEyEmployee = !isContractor;
                        if (gdsColumnIndex >= 0)
                        {
                            var gdsValue = NormalizeWhitespace(GetCellString(resourcing, dataRowIndex, gdsColumnIndex));
                            if (string.Equals(gdsValue, "vendor", StringComparison.OrdinalIgnoreCase))
                            {
                                isContractor = true;
                                isEyEmployee = false;
                            }
                        }

                        employees.Add(new ResourcingEmployee(
                            identifier,
                            employeeName,
                            isContractor,
                            isEyEmployee,
                            costCenter,
                            office));
                    }
                }

                dataRowIndex++;
            }

            return new ResourcingParseResult(rankBudgets, rankMappings.Values.ToList(), employees, weekStartDates, issues);
        }

        private static int FindResourcingHeaderRow(IWorksheet table)
        {
            for (var rowIndex = 0; rowIndex < table.RowCount; rowIndex++)
            {
                var hasLevel = false;
                var hasEmployee = false;

                for (var columnIndex = 0; columnIndex < table.ColumnCount; columnIndex++)
                {
                    var text = NormalizeWhitespace(Convert.ToString(table.GetValue(rowIndex, columnIndex), CultureInfo.InvariantCulture));
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    if (string.Equals(text, "level", StringComparison.OrdinalIgnoreCase))
                    {
                        hasLevel = true;
                    }
                    else if (string.Equals(text, "employee", StringComparison.OrdinalIgnoreCase))
                    {
                        hasEmployee = true;
                    }

                    if (hasLevel && hasEmployee)
                    {
                        return rowIndex;
                    }
                }
            }

            return -1;
        }

        private static bool IsResourcingSummaryRow(string rankName)
        {
            if (string.IsNullOrWhiteSpace(rankName))
            {
                return false;
            }

            var normalized = NormalizeWhitespace(rankName);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            return normalized.Contains("incurred", StringComparison.OrdinalIgnoreCase) &&
                   normalized.Contains("hour", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, int> BuildHeaderMap(IWorksheet table, int headerRowIndex)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var columnIndex = 0; columnIndex < table.ColumnCount; columnIndex++)
            {
                var header = NormalizeWhitespace(Convert.ToString(table.GetValue(headerRowIndex, columnIndex), CultureInfo.InvariantCulture));
                if (string.IsNullOrEmpty(header))
                {
                    continue;
                }

                if (!map.ContainsKey(header))
                {
                    map[header] = columnIndex;
                }
            }

            return map;
        }

        private static int GetRequiredColumnIndex(IReadOnlyDictionary<string, int> headerMap, string keyword)
        {
            foreach (var kvp in headerMap)
            {
                if (kvp.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            throw new InvalidDataException($"The RESOURCING worksheet is missing the required column '{keyword}'.");
        }

        private static int GetOptionalColumnIndex(IReadOnlyDictionary<string, int> headerMap, string keyword)
        {
            foreach (var kvp in headerMap)
            {
                if (kvp.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return -1;
        }

        private static int FindFirstHeaderColumnIndex(IWorksheet table, int headerRowIndex, string headerValue)
        {
            for (var columnIndex = 0; columnIndex < table.ColumnCount; columnIndex++)
            {
                var value = NormalizeWhitespace(Convert.ToString(table.GetValue(headerRowIndex, columnIndex), CultureInfo.InvariantCulture));
                if (string.Equals(value, headerValue, StringComparison.OrdinalIgnoreCase))
                {
                    return columnIndex;
                }
            }

            return -1;
        }

        private static List<DateTime> ExtractWeekStartDates(IWorksheet table, int rowIndex, int startColumnIndex)
        {
            var weekStarts = new SortedSet<DateTime>();

            if (rowIndex < 0 || rowIndex >= table.RowCount)
            {
                return new List<DateTime>(0);
            }

            for (var columnIndex = startColumnIndex; columnIndex < table.ColumnCount; columnIndex++)
            {
                var cell = GetCellValue(table, rowIndex, columnIndex);
                if (TryParseDate(cell, out var date))
                {
                    weekStarts.Add(NormalizeWeekStart(date));
                }
            }

            if (weekStarts.Count == 0)
            {
                return new List<DateTime>(0);
            }

            var result = new List<DateTime>(weekStarts.Count);
            result.AddRange(weekStarts);
            return result;
        }

        private static bool TryParseDate(object? value, out DateTime date)
        {
            switch (value)
            {
                case DateTime dt:
                    date = dt.Date;
                    return true;
                case double oaDate:
                    date = DateTime.FromOADate(oaDate).Date;
                    return true;
                case float oaFloat:
                    date = DateTime.FromOADate(oaFloat).Date;
                    return true;
                case int intValue:
                    date = DateTime.FromOADate(intValue).Date;
                    return true;
                case long longValue:
                    date = DateTime.FromOADate(longValue).Date;
                    return true;
                case string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var parsed):
                    date = parsed.Date;
                    return true;
                default:
                    date = default;
                    return false;
            }
        }

        private static string NormalizeRankName(string rawRank)
        {
            var normalized = NormalizeWhitespace(rawRank);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            var parts = normalized.Split('-', 2, StringSplitOptions.TrimEntries);
            var candidate = parts.Length == 2 ? parts[1] : parts[0];
            candidate = TrailingDigitsRegex.Replace(candidate, string.Empty).Trim();

            return candidate;
        }

        private static string DeriveSpreadsheetRankName(string rawRank)
        {
            var normalized = NormalizeWhitespace(rawRank);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            var parts = normalized.Split('-', 2, StringSplitOptions.TrimEntries);
            return parts.Length == 2 ? parts[1] : normalized;
        }

        private static string NormalizeIdentifier(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            if (value is double dbl)
            {
                return Math.Round(dbl).ToString(CultureInfo.InvariantCulture);
            }

            if (value is float flt)
            {
                return Math.Round(flt).ToString(CultureInfo.InvariantCulture);
            }

            var text = NormalizeWhitespace(Convert.ToString(value, CultureInfo.InvariantCulture));
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            text = text.Replace("#", string.Empty, StringComparison.Ordinal);

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                return Math.Round(numeric).ToString(CultureInfo.InvariantCulture);
            }

            return text;
        }

        private static string? NormalizeOptionalString(string value)
        {
            var normalized = NormalizeWhitespace(value);
            if (string.IsNullOrEmpty(normalized) || normalized == "#")
            {
                return null;
            }

            return normalized;
        }

        private static string DetermineEmployeeIdentifier(string guiValue, string mrsValue, string employeeName)
        {
            if (!string.IsNullOrEmpty(guiValue))
            {
                return TrimIdentifier(guiValue);
            }

            if (!string.IsNullOrEmpty(mrsValue))
            {
                return TrimIdentifier($"MRS-{mrsValue}");
            }

            if (!string.IsNullOrEmpty(employeeName))
            {
                var filtered = new string(employeeName.Where(char.IsLetterOrDigit).ToArray());
                if (!string.IsNullOrEmpty(filtered))
                {
                    return TrimIdentifier($"EMP-{filtered.ToUpperInvariant()}");
                }
            }

            return string.Empty;
        }

        private static string TrimIdentifier(string value)
        {
            const int maxLength = 20;
            if (value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        private static DateTime? ExtractGeneratedTimestampUtc(IWorksheet planInfo)
        {
            var value = NormalizeWhitespace(GetCellString(planInfo, 1, 1));
            if (string.IsNullOrEmpty(value))
            {
                value = NormalizeWhitespace(GetCellString(planInfo, 0, 1));
            }

            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            value = value.Replace("Generated on:", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Generated On:", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (TryParseDateTimeOffset(value, out var utcTimestamp))
            {
                return utcTimestamp;
            }

            return null;
        }

        private static bool TryParseDateTimeOffset(string value, out DateTime utcTimestamp)
        {
            var styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal;
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, styles, out var dto) ||
                DateTimeOffset.TryParse(value, CultureInfo.GetCultureInfo("en-GB"), styles, out dto) ||
                DateTimeOffset.TryParse(value, CultureInfo.GetCultureInfo("en-US"), styles, out dto))
            {
                utcTimestamp = dto.ToUniversalTime().UtcDateTime;
                return true;
            }

            var sanitized = Regex.Replace(value, @"\b[A-Z]{2,}$", string.Empty).Trim();
            if (DateTimeOffset.TryParse(sanitized, CultureInfo.InvariantCulture, styles, out dto))
            {
                utcTimestamp = dto.ToUniversalTime().UtcDateTime;
                return true;
            }

            utcTimestamp = default;
            return false;
        }

        private sealed record RankBudgetRow(string RawRank, decimal Hours);

        private sealed record RankBudgetAggregate(string RankName, decimal Hours);

        private sealed record RankMappingCandidate(string RawRank, string NormalizedRank, string SpreadsheetRank);

        private sealed record ResourcingEmployee(
            string Identifier,
            string Name,
            bool IsContractor,
            bool IsEyEmployee,
            string? CostCenter,
            string? Office);

        private sealed record ResourcingParseResult(
            List<RankBudgetRow> RankBudgets,
            List<RankMappingCandidate> RankMappings,
            List<ResourcingEmployee> Employees,
            List<DateTime> WeekStartDates,
            List<string> Issues);

        private static async Task UpsertRankMappingsAsync(
            ApplicationDbContext context,
            IReadOnlyCollection<RankMappingCandidate> candidates,
            DateTime? lastSeenAtUtc)
        {
            if (candidates.Count == 0)
            {
                return;
            }

            var timestamp = lastSeenAtUtc ?? DateTime.UtcNow;
            var rawRanks = new List<string>(candidates.Count);
            foreach (var candidate in candidates)
            {
                rawRanks.Add(candidate.RawRank);
            }
            var existingMappings = await context.RankMappings
                .Where(r => rawRanks.Contains(r.RawRank))
                .ToDictionaryAsync(r => r.RawRank, StringComparer.OrdinalIgnoreCase)
                .ConfigureAwait(false);

            foreach (var candidate in candidates)
            {
                if (existingMappings.TryGetValue(candidate.RawRank, out var mapping))
                {
                    mapping.NormalizedRank = candidate.NormalizedRank;
                    mapping.SpreadsheetRank = candidate.SpreadsheetRank;
                    mapping.IsActive = true;
                    mapping.LastSeenAt = timestamp;
                }
                else
                {
                    context.RankMappings.Add(new RankMapping
                    {
                        RawRank = candidate.RawRank,
                        NormalizedRank = candidate.NormalizedRank,
                        SpreadsheetRank = candidate.SpreadsheetRank,
                        IsActive = true,
                        LastSeenAt = timestamp
                    });
                }
            }
        }

        private static async Task UpsertEmployeesAsync(
            ApplicationDbContext context,
            IReadOnlyCollection<ResourcingEmployee> employees)
        {
            if (employees.Count == 0)
            {
                return;
            }

            var distinctEmployees = employees
                .GroupBy(e => e.Identifier, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var identifiers = new List<string>(distinctEmployees.Count);
            foreach (var employee in distinctEmployees)
            {
                identifiers.Add(employee.Identifier);
            }
            var existingEmployees = await context.Employees
                .Where(e => identifiers.Contains(e.Gpn))
                .ToDictionaryAsync(e => e.Gpn, StringComparer.OrdinalIgnoreCase)
                .ConfigureAwait(false);

            foreach (var employee in distinctEmployees)
            {
                if (existingEmployees.TryGetValue(employee.Identifier, out var entity))
                {
                    entity.EmployeeName = employee.Name;
                    entity.IsEyEmployee = employee.IsEyEmployee;
                    entity.IsContractor = employee.IsContractor;
                    entity.CostCenter = employee.CostCenter;
                    entity.Office = employee.Office;
                }
                else
                {
                    context.Employees.Add(new Employee
                    {
                        Gpn = employee.Identifier,
                        EmployeeName = employee.Name,
                        IsEyEmployee = employee.IsEyEmployee,
                        IsContractor = employee.IsContractor,
                        CostCenter = employee.CostCenter,
                        Office = employee.Office
                    });
                }
            }
        }

        private static (decimal value, bool hasValue) ParseHours(object? cellValue)
        {
            if (cellValue == null || cellValue == DBNull.Value)
            {
                return (0m, false);
            }

            switch (cellValue)
            {
                case decimal dec:
                    return (dec, true);
                case double dbl:
                    return ((decimal)dbl, true);
                case float flt:
                    return ((decimal)flt, true);
                case int i:
                    return (i, true);
                case long l:
                    return (l, true);
                case short s:
                    return (s, true);
                case string str:
                    var trimmed = str.Trim();
                    if (trimmed.Length == 0)
                    {
                        return (0m, false);
                    }

                    if (decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantParsed))
                    {
                        return (invariantParsed, true);
                    }

                    if (decimal.TryParse(trimmed, NumberStyles.Float, PtBrCulture, out var ptBrParsed))
                    {
                        return (ptBrParsed, true);
                    }

                    throw new InvalidDataException($"Unable to parse hours value '{str}'.");
                default:
                    try
                    {
                        var converted = Convert.ToDecimal(cellValue, CultureInfo.InvariantCulture);
                        return (converted, true);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Unable to parse hours value '{cellValue}'.", ex);
                    }
            }
        }

        private static string ExtractDescription(string rawDescription)
        {
            if (string.IsNullOrEmpty(rawDescription))
            {
                return string.Empty;
            }

            const string marker = "Budget-";
            var index = rawDescription.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var value = rawDescription[(index + marker.Length)..];
                return NormalizeWhitespace(value);
            }

            return NormalizeWhitespace(rawDescription);
        }

        private static string NormalizeSheetName(string name)
        {
            var normalized = NormalizeWhitespace(name).ToLowerInvariant();
            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

        private static object? GetCellValue(IWorksheet table, int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || columnIndex < 0)
            {
                return null;
            }

            if (table.RowCount <= rowIndex)
            {
                return null;
            }

            if (table.ColumnCount <= columnIndex)
            {
                return null;
            }

            return table.GetValue(rowIndex, columnIndex);
        }

        private static string GetCellString(IWorksheet table, int rowIndex, int columnIndex)
        {
            var value = GetCellValue(table, rowIndex, columnIndex);
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static WorkbookData LoadWorkbook(string filePath)
        {
            return WorkbookData.Load(filePath);
        }

        private sealed class WorkbookData : IDisposable
        {
            private readonly IReadOnlyList<WorksheetData> _worksheets;
            private readonly Dictionary<string, WorksheetData> _worksheetLookup;

            private WorkbookData(IReadOnlyList<WorksheetData> worksheets, Dictionary<string, WorksheetData> worksheetLookup)
            {
                _worksheets = worksheets;
                _worksheetLookup = worksheetLookup;
            }

            public IReadOnlyList<IWorksheet> Worksheets => _worksheets;

            public IWorksheet? FirstWorksheet => _worksheets.Count > 0 ? _worksheets[0] : null;

            public IWorksheet? GetWorksheet(string worksheetName)
            {
                if (string.IsNullOrWhiteSpace(worksheetName))
                {
                    return null;
                }

                var key = NormalizeSheetName(worksheetName);
                return _worksheetLookup.TryGetValue(key, out var table) ? table : null;
            }

            public void Dispose()
            {
                // No unmanaged resources to release. Implemented for call-site symmetry.
            }

            public static WorkbookData Load(string filePath)
            {
                using var stream = new FileStream(filePath, SharedReadOptions);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                var worksheets = new List<WorksheetData>();
                var lookup = new Dictionary<string, WorksheetData>(StringComparer.Ordinal);

                do
                {
                    var worksheet = WorksheetData.Create(reader, reader.Name ?? string.Empty);
                    worksheets.Add(worksheet);

                    var normalizedName = NormalizeSheetName(worksheet.Name);
                    if (!lookup.ContainsKey(normalizedName))
                    {
                        lookup[normalizedName] = worksheet;
                    }
                }
                while (reader.NextResult());

                return new WorkbookData(worksheets, lookup);
            }
        }

        public interface IWorksheet
        {
            int RowCount { get; }
            int ColumnCount { get; }
            string Name { get; }
            object? GetValue(int rowIndex, int columnIndex);
        }

        private sealed class WorksheetData : IWorksheet
        {
            private readonly object?[][] _cells;

            private WorksheetData(string name, object?[][] cells, int columnCount)
            {
                Name = name;
                _cells = cells;
                ColumnCount = columnCount;
            }

            public string Name { get; }
            int IWorksheet.RowCount => _cells.Length;
            int IWorksheet.ColumnCount => ColumnCount;
            public int ColumnCount { get; }

            public object? GetValue(int rowIndex, int columnIndex)
            {
                if ((uint)rowIndex >= (uint)_cells.Length)
                {
                    return null;
                }

                var row = _cells[rowIndex];
                if ((uint)columnIndex >= (uint)row.Length)
                {
                    return null;
                }

                return row[columnIndex];
            }

            public static WorksheetData Create(IExcelDataReader reader, string name)
            {
                var rows = new List<object?[]>(128);
                var maxColumns = 0;

                while (reader.Read())
                {
                    var fieldCount = reader.FieldCount;
                    if (fieldCount > maxColumns)
                    {
                        maxColumns = fieldCount;
                        EnsureRowCapacity(rows, maxColumns);
                    }

                    var values = new object?[maxColumns];
                    for (var columnIndex = 0; columnIndex < fieldCount; columnIndex++)
                    {
                        values[columnIndex] = reader.GetValue(columnIndex);
                    }

                    rows.Add(values);
                }

                return new WorksheetData(name, rows.ToArray(), maxColumns);
            }

            private static void EnsureRowCapacity(List<object?[]> rows, int targetLength)
            {
                if (targetLength == 0 || rows.Count == 0)
                {
                    return;
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row.Length == targetLength)
                    {
                        continue;
                    }

                    var expanded = new object?[targetLength];
                    Array.Copy(row, expanded, row.Length);
                    rows[i] = expanded;
                }
            }
        }

        private static List<RankBudgetAggregate> AggregateRankBudgets(IReadOnlyCollection<RankBudgetRow> rows)
        {
            if (rows.Count == 0)
            {
                return new List<RankBudgetAggregate>(0);
            }

            var aggregates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.RawRank))
                {
                    continue;
                }

                if (aggregates.TryGetValue(row.RawRank, out var existing))
                {
                    aggregates[row.RawRank] = existing + row.Hours;
                }
                else
                {
                    aggregates[row.RawRank] = row.Hours;
                }
            }

            var result = new List<RankBudgetAggregate>(aggregates.Count);
            foreach (var pair in aggregates)
            {
                result.Add(new RankBudgetAggregate(pair.Key, pair.Value));
            }

            return result;
        }

        private static async Task EnsureCustomerCodeAsync(ApplicationDbContext context, Customer customer)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(customer);

            if (!string.IsNullOrWhiteSpace(customer.CustomerCode))
            {
                return;
            }

            const int randomSegmentLength = 12;

            while (true)
            {
                var randomSegment = Guid.NewGuid().ToString("N").Substring(0, randomSegmentLength).ToUpperInvariant();
                var placeholder = $"AUTO-{randomSegment}";

                var exists = await context.Customers
                    .AsNoTracking()
                    .AnyAsync(c => c.Id != customer.Id && c.CustomerCode == placeholder)
                    .ConfigureAwait(false);

                if (!exists)
                {
                    customer.CustomerCode = placeholder;
                    return;
                }
            }
        }

        private static int ApplyBudgetSnapshot(
            Engagement engagement,
            IReadOnlyCollection<FiscalYear> fiscalYears,
            IReadOnlyCollection<RankBudgetAggregate> rankBudgets,
            DateTime timestamp)
        {
            if (rankBudgets.Count == 0)
            {
                return 0;
            }

            if (fiscalYears.Count == 0)
            {
                throw new InvalidOperationException("No fiscal years have been configured. Add a fiscal year before importing allocation planning data.");
            }

            var currentFiscalYear = ResolveCurrentFiscalYear(fiscalYears);
            var openFiscalYears = fiscalYears.Where(fy => !fy.IsLocked).ToList();
            if (openFiscalYears.Count == 0)
            {
                openFiscalYears.Add(currentFiscalYear);
            }

            var inserted = 0;
            foreach (var budget in rankBudgets)
            {
                inserted += EnsureBudgetExists(engagement, currentFiscalYear.Id, budget.RankName, budget.Hours, timestamp);

                foreach (var fiscalYear in openFiscalYears)
                {
                    if (fiscalYear.Id == currentFiscalYear.Id)
                    {
                        continue;
                    }

                    inserted += EnsureBudgetExists(engagement, fiscalYear.Id, budget.RankName, 0m, timestamp);
                }
            }

            return inserted;
        }

        private static FiscalYear ResolveCurrentFiscalYear(IReadOnlyCollection<FiscalYear> fiscalYears)
        {
            var today = DateTime.UtcNow.Date;
            var current = fiscalYears.FirstOrDefault(fy => fy.StartDate.Date <= today && fy.EndDate.Date >= today);
            if (current is not null)
            {
                return current;
            }

            var firstOpen = fiscalYears.FirstOrDefault(fy => !fy.IsLocked);
            if (firstOpen is not null)
            {
                return firstOpen;
            }

            return fiscalYears.OrderBy(fy => fy.StartDate).Last();
        }

        private static int EnsureBudgetExists(
            Engagement engagement,
            int fiscalYearId,
            string rankName,
            decimal budgetHours,
            DateTime timestamp)
        {
            if (string.IsNullOrWhiteSpace(rankName))
            {
                return 0;
            }

            var roundedBudget = Math.Round(budgetHours, 2, MidpointRounding.AwayFromZero);

            var existing = engagement.RankBudgets.FirstOrDefault(b =>
                b.FiscalYearId == fiscalYearId &&
                string.Equals(b.RankName, rankName, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                var incurred = existing.CalculateIncurredHours();
                existing.BudgetHours = roundedBudget;
                var remaining = Math.Round(
                    roundedBudget + existing.AdditionalHours - (incurred + existing.ConsumedHours),
                    2,
                    MidpointRounding.AwayFromZero);
                existing.RemainingHours = remaining;
                existing.Status = remaining switch
                {
                    < 0m => nameof(TrafficLightStatus.Red),
                    > 0m => nameof(TrafficLightStatus.Yellow),
                    _ => nameof(TrafficLightStatus.Green)
                };
                existing.UpdatedAtUtc = timestamp;
                return 0;
            }

            engagement.RankBudgets.Add(new EngagementRankBudget
            {
                Engagement = engagement,
                EngagementId = engagement.Id,
                FiscalYearId = fiscalYearId,
                RankName = rankName,
                BudgetHours = roundedBudget,
                ConsumedHours = 0m,
                AdditionalHours = 0m,
                RemainingHours = roundedBudget,
                Status = nameof(TrafficLightStatus.Green),
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });

            return 1;
        }

        public async Task<string> UpdateStaffAllocationsAsync(string filePath, int closingPeriodId)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Staff allocation workbook could not be found.", filePath);
            }

            if (closingPeriodId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(closingPeriodId), closingPeriodId, "Closing period identifier must be positive.");
            }

            using var workbook = LoadWorkbook(filePath);

            var worksheet = workbook.GetWorksheet("Alocações_Staff") ??
                            workbook.GetWorksheet("Alocacoes_Staff") ??
                            throw new InvalidDataException("Worksheet 'Alocações_Staff' is missing from the staff allocation workbook.");

            await using var context = await _contextFactory
                .CreateDbContextAsync()
                .ConfigureAwait(false);

            var closingPeriod = await context.ClosingPeriods
                .Include(cp => cp.FiscalYear)
                .SingleOrDefaultAsync(cp => cp.Id == closingPeriodId)
                .ConfigureAwait(false);

            if (closingPeriod is null)
            {
                throw new InvalidOperationException($"Closing period {closingPeriodId} was not found.");
            }

            if (closingPeriod.FiscalYear is null)
            {
                await context.Entry(closingPeriod)
                    .Reference(cp => cp.FiscalYear)
                    .LoadAsync()
                    .ConfigureAwait(false);
            }

            if (closingPeriod.FiscalYear?.IsLocked == true)
            {
                var fiscalYearName = string.IsNullOrWhiteSpace(closingPeriod.FiscalYear.Name)
                    ? closingPeriod.FiscalYearId.ToString(CultureInfo.InvariantCulture)
                    : closingPeriod.FiscalYear.Name;

                throw new InvalidOperationException($"Fiscal year '{fiscalYearName}' is locked. Unlock it before importing staff allocations.");
            }

            var rankMappings = await context.RankMappings
                .AsNoTracking()
                .Where(mapping => mapping.IsActive)
                .ToListAsync()
                .ConfigureAwait(false);

            var parserLogger = _loggerFactory.CreateLogger<SimplifiedStaffAllocationParser>();
            var parser = new SimplifiedStaffAllocationParser(parserLogger);
            var groupedAllocations = parser.Parse(worksheet, closingPeriod, rankMappings);

            var allocationLookup = groupedAllocations
                .Where(a => !string.IsNullOrWhiteSpace(a.EngagementCode) && !string.IsNullOrWhiteSpace(a.RankCode))
                .ToDictionary(
                    a => new AllocationKey(NormalizeAllocationCode(a.EngagementCode), NormalizeAllocationCode(a.RankCode), a.FiscalYearId),
                    a => a,
                    AllocationKeyComparer.Instance);

            var existingHistories = await context.EngagementRankBudgetHistory
                .Where(h => h.ClosingPeriodId == closingPeriod.Id)
                .ToListAsync()
                .ConfigureAwait(false);

            var historyLookup = existingHistories.ToDictionary(
                h => new AllocationKey(NormalizeAllocationCode(h.EngagementCode), NormalizeAllocationCode(h.RankCode), h.FiscalYearId),
                h => h,
                AllocationKeyComparer.Instance);

            var processedHistoryKeys = new HashSet<AllocationKey>(AllocationKeyComparer.Instance);

            var engagementCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var allocation in groupedAllocations)
            {
                engagementCodes.Add(allocation.EngagementCode);
            }

            foreach (var history in existingHistories)
            {
                engagementCodes.Add(history.EngagementCode);
            }

            var engagements = await context.Engagements
                .Include(e => e.RankBudgets.Where(b => b.FiscalYearId == closingPeriod.FiscalYearId))
                .Where(e => engagementCodes.Contains(e.EngagementId))
                .ToListAsync()
                .ConfigureAwait(false);

            var engagementLookup = engagements.ToDictionary(e => NormalizeAllocationCode(e.EngagementId), e => e);

            var budgetLookup = new Dictionary<BudgetKey, EngagementRankBudget>(BudgetKeyComparer.Instance);
            foreach (var engagement in engagements)
            {
                var engagementKey = NormalizeAllocationCode(engagement.EngagementId);
                foreach (var budget in engagement.RankBudgets.Where(b => b.FiscalYearId == closingPeriod.FiscalYearId))
                {
                    var rankKey = NormalizeAllocationCode(budget.RankName);
                    budgetLookup[new BudgetKey(engagementKey, rankKey)] = budget;
                }
            }

            var nowUtc = DateTime.UtcNow;

            foreach (var (key, allocation) in allocationLookup)
            {
                if (!engagementLookup.TryGetValue(key.EngagementCode, out var engagement))
                {
                    _logger.LogWarning("Engagement {EngagementCode} was not found. Allocation skipped.", allocation.EngagementCode);
                    continue;
                }

                var budgetKey = new BudgetKey(key.EngagementCode, key.RankCode);
                var isNewBudget = false;
                if (!budgetLookup.TryGetValue(budgetKey, out var budget))
                {
                    budget = new EngagementRankBudget
                    {
                        EngagementId = engagement.Id,
                        FiscalYearId = closingPeriod.FiscalYearId,
                        RankName = allocation.RankCode,
                        BudgetHours = allocation.Hours,
                        ConsumedHours = 0m,
                        AdditionalHours = 0m,
                        RemainingHours = 0m,
                        Status = nameof(TrafficLightStatus.Green),
                        CreatedAtUtc = nowUtc,
                        UpdatedAtUtc = nowUtc
                    };

                    context.EngagementRankBudgets.Add(budget);
                    budgetLookup[budgetKey] = budget;
                    isNewBudget = true;
                }

                historyLookup.TryGetValue(key, out var historyEntry);
                var previousHours = historyEntry?.Hours ?? 0m;
                var diff = allocation.Hours - previousHours;

                if (isNewBudget)
                {
                    budget.ApplyIncurredHours(allocation.Hours);
                }
                else
                {
                    var currentIncurred = budget.CalculateIncurredHours();
                    budget.ApplyIncurredHours(currentIncurred + diff);
                }

                budget.UpdatedAtUtc = nowUtc;

                if (historyEntry is not null)
                {
                    historyEntry.Hours = allocation.Hours;
                    historyEntry.UploadedAt = nowUtc;
                    processedHistoryKeys.Add(key);
                }
                else
                {
                    var history = new EngagementRankBudgetHistory
                    {
                        EngagementCode = allocation.EngagementCode,
                        RankCode = allocation.RankCode,
                        FiscalYearId = allocation.FiscalYearId,
                        ClosingPeriodId = closingPeriod.Id,
                        Hours = allocation.Hours,
                        UploadedAt = nowUtc
                    };

                    context.EngagementRankBudgetHistory.Add(history);
                }
            }

            foreach (var historyPair in historyLookup)
            {
                if (processedHistoryKeys.Contains(historyPair.Key))
                {
                    continue;
                }

                if (!engagementLookup.TryGetValue(historyPair.Key.EngagementCode, out var engagement))
                {
                    continue;
                }

                var budgetKey = new BudgetKey(historyPair.Key.EngagementCode, historyPair.Key.RankCode);
                if (!budgetLookup.TryGetValue(budgetKey, out var budget))
                {
                    continue;
                }

                var previousHours = historyPair.Value.Hours;
                if (previousHours == 0m)
                {
                    continue;
                }

                var currentIncurred = budget.CalculateIncurredHours();
                budget.ApplyIncurredHours(currentIncurred - previousHours);
                budget.UpdatedAtUtc = nowUtc;
                historyPair.Value.Hours = 0m;
                historyPair.Value.UploadedAt = nowUtc;
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            var totalHours = groupedAllocations.Sum(a => a.Hours);

            _logger.LogInformation(
                "Imported CP {CPName} ({CPId}): {Count} engagement-rank combinations, total {Hours} hours.",
                closingPeriod.Name,
                closingPeriod.Id,
                groupedAllocations.Count,
                totalHours);

            var summary = FormattableString.Invariant(
                $"Imported CP {closingPeriod.Name} ({closingPeriod.Id}): {groupedAllocations.Count} engagement-rank combinations, total {totalHours} hours.");

            return summary;
        }

        private static (Dictionary<int, string> Map, int HeaderRowIndex) BuildFcsHeaderMap(IWorksheet worksheet)
        {
            Dictionary<int, string>? fallbackMap = null;
            var fallbackIndex = -1;
            var searchLimit = Math.Min(worksheet.RowCount, FcsHeaderSearchLimit);

            for (var rowIndex = 0; rowIndex < searchLimit; rowIndex++)
            {
                var currentMap = new Dictionary<int, string>();
                var hasContent = false;

                for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
                {
                    var headerText = NormalizeWhitespace(Convert.ToString(worksheet.GetValue(rowIndex, columnIndex), CultureInfo.InvariantCulture));
                    if (!string.IsNullOrEmpty(headerText))
                    {
                        hasContent = true;
                    }

                    currentMap[columnIndex] = headerText.ToLowerInvariant();
                }

                if (!hasContent)
                {
                    continue;
                }

                if (ContainsAnyHeader(currentMap, FcsEngagementIdHeaders))
                {
                    return (currentMap, rowIndex);
                }

                fallbackMap ??= currentMap;
                if (fallbackIndex < 0)
                {
                    fallbackIndex = rowIndex;
                }
            }

            return fallbackMap != null
                ? (fallbackMap, fallbackIndex)
                : (new Dictionary<int, string>(), -1);
        }

        private static int GetRequiredColumnIndex(
            Dictionary<int, string> headerMap,
            IEnumerable<string> candidates,
            string friendlyName)
        {
            int? firstPartialMatch = null;

            foreach (var candidate in candidates)
            {
                var normalizedCandidate = candidate.ToLowerInvariant();
                foreach (var kvp in headerMap)
                {
                    var header = kvp.Value;
                    if (string.IsNullOrEmpty(header) || !header.Contains(normalizedCandidate, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(header, normalizedCandidate, StringComparison.Ordinal))
                    {
                        return kvp.Key;
                    }

                    firstPartialMatch ??= kvp.Key;
                }
            }

            if (firstPartialMatch.HasValue)
            {
                return firstPartialMatch.Value;
            }

            throw new InvalidDataException($"The FCS backlog worksheet is missing required column '{friendlyName}'. Ensure the first sheet is selected and filters are cleared before importing.");
        }

        private static List<int> ResolveFutureFiscalYearColumns(Dictionary<int, string> headerMap, string nextFiscalYearName)
        {
            var indices = new List<int>();

            foreach (var candidate in FcsFutureFiscalYearBacklogHeaders)
            {
                var normalizedCandidate = candidate.ToLowerInvariant();
                foreach (var kvp in headerMap)
                {
                    if (!string.IsNullOrEmpty(kvp.Value) && kvp.Value.Contains(normalizedCandidate, StringComparison.Ordinal) &&
                        !kvp.Value.Contains("opp currency", StringComparison.Ordinal) &&
                        !kvp.Value.Contains("lead", StringComparison.Ordinal))
                    {
                        indices.Add(kvp.Key);
                        return indices;
                    }
                }
            }

            var fiscalYearDigits = ExtractDigits(nextFiscalYearName);
            foreach (var kvp in headerMap)
            {
                var header = kvp.Value;
                if (string.IsNullOrEmpty(header))
                {
                    continue;
                }

                if (!header.Contains("future", StringComparison.Ordinal) || !header.Contains("backlog", StringComparison.Ordinal))
                {
                    continue;
                }

                if (header.Contains("opp currency", StringComparison.Ordinal) || header.Contains("lead", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(fiscalYearDigits) && !header.Contains(fiscalYearDigits, StringComparison.Ordinal))
                {
                    continue;
                }

                indices.Add(kvp.Key);
            }

            if (indices.Count == 0)
            {
                foreach (var kvp in headerMap)
                {
                    var header = kvp.Value;
                    if (string.IsNullOrEmpty(header))
                    {
                        continue;
                    }

                    if (header.Contains("future fy backlog", StringComparison.Ordinal) &&
                        !header.Contains("opp currency", StringComparison.Ordinal) &&
                        !header.Contains("lead", StringComparison.Ordinal))
                    {
                        indices.Add(kvp.Key);
                    }
                }
            }

            return indices;
        }

        private static string ExtractDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var match = DigitsRegex.Match(value);
            return match.Success ? match.Value : string.Empty;
        }

        private static bool TryExtractFiscalYearCode(string? value, out string normalizedValue, out string fiscalYearCode)
        {
            normalizedValue = NormalizeWhitespace(value);
            if (string.IsNullOrEmpty(normalizedValue))
            {
                fiscalYearCode = string.Empty;
                return false;
            }

            var upper = normalizedValue.ToUpperInvariant();
            var match = FiscalYearCodeRegex.Match(upper);
            if (match.Success)
            {
                fiscalYearCode = match.Value.ToUpperInvariant();
                return true;
            }

            var fyIndex = upper.IndexOf("FY", StringComparison.Ordinal);
            if (fyIndex >= 0)
            {
                var digitsBuilder = new StringBuilder();
                for (var i = fyIndex + 2; i < upper.Length; i++)
                {
                    var ch = upper[i];
                    if (char.IsDigit(ch))
                    {
                        digitsBuilder.Append(ch);
                    }
                    else if (digitsBuilder.Length == 0 && !char.IsLetterOrDigit(ch))
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                if (digitsBuilder.Length > 0)
                {
                    fiscalYearCode = $"FY{digitsBuilder}";
                    return true;
                }
            }

            fiscalYearCode = string.Empty;
            return false;
        }

        private static bool ContainsAnyHeader(Dictionary<int, string> headerMap, IEnumerable<string> candidates)
        {
            foreach (var candidate in candidates)
            {
                var normalizedCandidate = candidate.ToLowerInvariant();
                foreach (var header in headerMap.Values)
                {
                    if (!string.IsNullOrEmpty(header) && header.Contains(normalizedCandidate, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public async Task<string> ImportFcsRevenueBacklogAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("FCS backlog workbook could not be found.", filePath);
            }

            using var workbook = LoadWorkbook(filePath);

            var worksheet = workbook.FirstWorksheet;
            if (worksheet == null)
            {
                throw new InvalidDataException("The FCS backlog workbook does not contain any worksheets.");
            }

            var (currentFiscalYearName, lastUpdateDate) = ParseFcsMetadata(worksheet);
            var nextFiscalYearName = IncrementFiscalYearName(currentFiscalYearName);

            var (headerMap, headerRowIndex) = BuildFcsHeaderMap(worksheet);

            if (headerMap.Count == 0)
            {
                throw new InvalidDataException("Unable to locate the header row in the FCS backlog worksheet. Ensure the first sheet is selected and filters are cleared before importing.");
            }

            var engagementIdIndex = GetRequiredColumnIndex(headerMap, FcsEngagementIdHeaders, "Engagement ID");
            var currentFiscalYearBacklogIndex = GetRequiredColumnIndex(
                headerMap,
                FcsCurrentFiscalYearBacklogHeaders,
                "FYTG Backlog");
            var futureFiscalYearIndexes = ResolveFutureFiscalYearColumns(headerMap, nextFiscalYearName);
            if (futureFiscalYearIndexes.Count == 0)
            {
                throw new InvalidDataException($"The FCS backlog worksheet is missing the Future FY Backlog columns for fiscal year {nextFiscalYearName}.");
            }

            var dataStartRowIndex = Math.Max(headerRowIndex + 1, FcsDataStartRowIndex);

            var parsedRows = new List<FcsBacklogRow>();
            for (var rowIndex = dataStartRowIndex; rowIndex < worksheet.RowCount; rowIndex++)
            {
                if (IsRowEmpty(worksheet, rowIndex))
                {
                    continue;
                }

                var engagementIdRaw = Convert.ToString(worksheet.GetValue(rowIndex, engagementIdIndex), CultureInfo.InvariantCulture);
                var engagementId = NormalizeWhitespace(engagementIdRaw);
                if (string.IsNullOrEmpty(engagementId))
                {
                    continue;
                }

                var currentBacklog = ParseDecimal(worksheet.GetValue(rowIndex, currentFiscalYearBacklogIndex), 2) ?? 0m;

                decimal futureBacklog = 0m;
                foreach (var index in futureFiscalYearIndexes)
                {
                    futureBacklog += ParseDecimal(worksheet.GetValue(rowIndex, index), 2) ?? 0m;
                }

                var excelRowNumber = rowIndex + 1; // Excel is 1-based
                parsedRows.Add(new FcsBacklogRow(engagementId, currentBacklog, futureBacklog, excelRowNumber));
            }

            if (parsedRows.Count == 0)
            {
                var emptyNotes = new List<string>
                {
                    $"Workbook did not contain any backlog data for fiscal year {currentFiscalYearName}."
                };

                if (lastUpdateDate.HasValue)
                {
                    emptyNotes.Add($"Workbook last update: {lastUpdateDate.Value:yyyy-MM-dd}");
                }

                return ImportSummaryFormatter.Build(
                    $"FCS backlog import ({currentFiscalYearName})",
                    inserted: 0,
                    updated: 0,
                    skipReasons: null,
                    notes: emptyNotes,
                    processed: 0);
            }

            var distinctEngagementIds = parsedRows
                .Select(r => r.EngagementId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await using var context = await _contextFactory
                .CreateDbContextAsync()
                .ConfigureAwait(false);

            var fiscalYears = await context.FiscalYears
                .AsNoTracking()
                .Where(fy => fy.Name == currentFiscalYearName || fy.Name == nextFiscalYearName)
                .ToListAsync()
                .ConfigureAwait(false);

            var fiscalYearLookup = fiscalYears.ToDictionary(fy => fy.Name, StringComparer.OrdinalIgnoreCase);

            if (!fiscalYearLookup.TryGetValue(currentFiscalYearName, out var currentFiscalYear))
            {
                throw new InvalidDataException(
                    $"Fiscal year '{currentFiscalYearName}' referenced by the FCS backlog workbook could not be found in the database.");
            }

            fiscalYearLookup.TryGetValue(nextFiscalYearName, out var nextFiscalYear);
            if (nextFiscalYear == null)
            {
                _logger.LogWarning(
                    "Next fiscal year {NextFiscalYear} referenced by the FCS backlog workbook was not found. Future backlog values will be skipped.",
                    nextFiscalYearName);
            }

            var engagements = await context.Engagements
                .Include(e => e.RevenueAllocations)
                .Where(e => distinctEngagementIds.Contains(e.EngagementId))
                .ToListAsync()
                .ConfigureAwait(false);

            var engagementLookup = engagements.ToDictionary(e => e.EngagementId, StringComparer.OrdinalIgnoreCase);

            var manualOnlyDetails = new List<string>();
            var closedEngagementDetails = new List<string>();
            var missingEngagementDetails = new List<string>();
            var lockedFiscalYearDetails = new List<string>();
            var touchedEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var warningMessages = new HashSet<string>(StringComparer.Ordinal);

            var createdAllocations = 0;
            var updatedAllocations = 0;

            foreach (var row in parsedRows)
            {
                if (!engagementLookup.TryGetValue(row.EngagementId, out var engagement))
                {
                    _logger.LogWarning(
                        "FCS backlog row {RowNumber} references engagement '{EngagementId}', which was not found in the database.",
                        row.RowNumber,
                        row.EngagementId);
                    missingEngagementDetails.Add($"{row.EngagementId} (row {row.RowNumber})");
                    continue;
                }

                if (EngagementImportSkipEvaluator.TryCreate(engagement, out var skipMetadata))
                {
                    var detail = $"{engagement.EngagementId} (row {row.RowNumber})";
                    switch (skipMetadata.ReasonKey)
                    {
                        case "ManualOnly":
                            manualOnlyDetails.Add(detail);
                            break;
                        case "ClosedEngagement":
                            closedEngagementDetails.Add(detail);
                            break;
                    }

                    warningMessages.Add(skipMetadata.WarningMessage);
                    _logger.LogWarning("{Warning} (row {RowNumber})", skipMetadata.WarningMessage, row.RowNumber);
                    continue;
                }

                var toGoCurrent = RoundMoney(row.CurrentBacklog);
                var toDateCurrent = RoundMoney(engagement.ValueToAllocate - row.CurrentBacklog - row.FutureBacklog);
                var toGoNext = RoundMoney(row.FutureBacklog);

                if (currentFiscalYear.IsLocked)
                {
                    _logger.LogInformation(
                        "Skipping FCS backlog update for engagement {EngagementId} in fiscal year {FiscalYear} because the fiscal year is locked.",
                        engagement.EngagementId,
                        currentFiscalYear.Name);
                    lockedFiscalYearDetails.Add($"{engagement.EngagementId} ({currentFiscalYear.Name})");
                }
                else
                {
                    var allocation = engagement.RevenueAllocations
                        .FirstOrDefault(a => a.FiscalYearId == currentFiscalYear.Id);

                    if (allocation == null)
                    {
                        allocation = new EngagementFiscalYearRevenueAllocation
                        {
                            EngagementId = engagement.Id,
                            FiscalYearId = currentFiscalYear.Id,
                            ToGoValue = toGoCurrent,
                            ToDateValue = toDateCurrent,
                            UpdatedAt = DateTime.UtcNow
                        };
                        engagement.RevenueAllocations.Add(allocation);
                        context.EngagementFiscalYearRevenueAllocations.Add(allocation);
                        createdAllocations++;
                    }
                    else
                    {
                        allocation.ToGoValue = toGoCurrent;
                        allocation.ToDateValue = toDateCurrent;
                        updatedAllocations++;
                    }

                    touchedEngagements.Add(engagement.EngagementId);
                }

                if (nextFiscalYear == null)
                {
                    continue;
                }

                if (nextFiscalYear.IsLocked)
                {
                    _logger.LogInformation(
                        "Skipping FCS backlog update for engagement {EngagementId} in fiscal year {FiscalYear} because the fiscal year is locked.",
                        engagement.EngagementId,
                        nextFiscalYear.Name);
                    lockedFiscalYearDetails.Add($"{engagement.EngagementId} ({nextFiscalYear.Name})");
                    continue;
                }

                var nextAllocation = engagement.RevenueAllocations
                    .FirstOrDefault(a => a.FiscalYearId == nextFiscalYear.Id);

                if (nextAllocation == null)
                {
                    nextAllocation = new EngagementFiscalYearRevenueAllocation
                    {
                        EngagementId = engagement.Id,
                        FiscalYearId = nextFiscalYear.Id,
                        ToGoValue = toGoNext,
                        ToDateValue = 0m,
                        UpdatedAt = DateTime.UtcNow
                    };
                    engagement.RevenueAllocations.Add(nextAllocation);
                    context.EngagementFiscalYearRevenueAllocations.Add(nextAllocation);
                    createdAllocations++;
                }
                else
                {
                    nextAllocation.ToGoValue = toGoNext;
                    nextAllocation.ToDateValue = 0m;
                    updatedAllocations++;
                }

                touchedEngagements.Add(engagement.EngagementId);
            }

            if (createdAllocations + updatedAllocations > 0)
            {
                await context.SaveChangesAsync().ConfigureAwait(false);
            }

            var skipReasons = new Dictionary<string, IReadOnlyCollection<string>>();

            if (manualOnlyDetails.Count > 0)
            {
                skipReasons["ManualOnly"] = manualOnlyDetails;
            }

            if (closedEngagementDetails.Count > 0)
            {
                skipReasons["ClosedEngagement"] = closedEngagementDetails;
            }

            if (lockedFiscalYearDetails.Count > 0)
            {
                skipReasons["LockedFiscalYear"] = lockedFiscalYearDetails;
            }

            if (missingEngagementDetails.Count > 0)
            {
                skipReasons["MissingEngagement"] = missingEngagementDetails;
            }

            var notes = new List<string>(4)
            {
                $"Engagements affected: {touchedEngagements.Count}"
            };

            if (warningMessages.Count > 0)
            {
                foreach (var warning in warningMessages)
                {
                    notes.Insert(0, warning);
                }
            }

            if (lastUpdateDate.HasValue)
            {
                notes.Insert(0, $"Workbook last update: {lastUpdateDate.Value:yyyy-MM-dd}");
            }

            if (nextFiscalYear == null)
            {
                notes.Add($"Future backlog values skipped because fiscal year {nextFiscalYearName} was not found.");
            }

            return ImportSummaryFormatter.Build(
                $"FCS backlog import ({currentFiscalYear.Name})",
                createdAllocations,
                updatedAllocations,
                skipReasons,
                notes,
                parsedRows.Count);
        }

        public async Task<FullManagementDataImportResult> ImportFullManagementDataAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            return await _fullManagementDataImporter
                .ImportAsync(filePath)
                .ConfigureAwait(false);
        }


        private static Dictionary<string, string> BuildRankLookupForCanonicalMapping(IReadOnlyList<RankMapping> rankMappings)
        {
            // Build rank lookup: maps normalized rank from spreadsheet to canonical RankCode (RawRank)
            var rankLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in rankMappings.Where(m => m.IsActive))
            {
                var rankCode = NormalizeCode(mapping.RawRank);
                if (string.IsNullOrEmpty(rankCode))
                {
                    continue;
                }

                // Map SpreadsheetRank to canonical RankCode
                if (!string.IsNullOrWhiteSpace(mapping.SpreadsheetRank))
                {
                    var normalizedSpreadsheetRank = NormalizeRank(mapping.SpreadsheetRank);
                    if (!string.IsNullOrEmpty(normalizedSpreadsheetRank))
                    {
                        rankLookup[normalizedSpreadsheetRank] = rankCode;
                    }
                }

                // Map RawRank to canonical RankCode
                if (!string.IsNullOrWhiteSpace(mapping.RawRank))
                {
                    var normalizedRawRank = NormalizeRank(mapping.RawRank);
                    if (!string.IsNullOrEmpty(normalizedRawRank) && !rankLookup.ContainsKey(normalizedRawRank))
                    {
                        rankLookup[normalizedRawRank] = rankCode;
                    }
                }

                // Map NormalizedRank to canonical RankCode
                if (!string.IsNullOrWhiteSpace(mapping.NormalizedRank))
                {
                    var normalizedRank = NormalizeRank(mapping.NormalizedRank);
                    if (!string.IsNullOrEmpty(normalizedRank) && !rankLookup.ContainsKey(normalizedRank))
                    {
                        rankLookup[normalizedRank] = rankCode;
                    }
                }
            }

            return rankLookup;
        }

        private static string NormalizeRank(string? value)
        {
            return NormalizeRankKey(value); // Reuse existing method for consistency
        }

        private static string NormalizeCode(string? value)
        {
            return NormalizeEngagementCode(value); // Reuse existing method for consistency
        }

        private static string? TryExtractEngagementCode(object? value)
        {
            return WorksheetValueHelper.TryExtractEngagementCode(value);
        }

        private static string GetString(object? value)
        {
            return WorksheetValueHelper.GetString(value);
        }

        private static DateTime? TryParseWeekDate(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is DateTime directDate)
            {
                return directDate.Date;
            }

            var text = GetString(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var dateFormats = new[]
            {
                "dd/MM/yyyy",
                "d/M/yyyy",
                "dd/MM/yy",
                "d/M/yy"
            };

            var cultures = new[]
            {
                CultureInfo.InvariantCulture,
                new CultureInfo("pt-BR")
            };

            foreach (var culture in cultures)
            {
                if (DateTime.TryParseExact(text, dateFormats, culture, DateTimeStyles.AssumeLocal, out var parsed))
                {
                    return parsed.Date;
                }
            }

            return null;
        }

        private sealed record EmployeeRowData(int RowIndex, string Rank);

        private static List<EmployeeRowData> ExtractEmployeeRows(IWorksheet worksheet)
        {
            var rows = new List<EmployeeRowData>();
            var consecutiveBlanks = 0;

            for (var rowIndex = 1; rowIndex < worksheet.RowCount; rowIndex++)
            {
                var gpn = GetString(worksheet.GetValue(rowIndex, 0));
                if (string.IsNullOrWhiteSpace(gpn))
                {
                    consecutiveBlanks++;
                    if (consecutiveBlanks >= 3)
                    {
                        break;
                    }

                    continue;
                }

                consecutiveBlanks = 0;

                var rank = GetString(worksheet.GetValue(rowIndex, 2)); // Column 2 is the Rank column
                rows.Add(new EmployeeRowData(rowIndex, rank));
            }

            return rows;
        }

        private sealed record WeekColumnData(int ColumnIndex, DateTime WeekDate);

        private static List<WeekColumnData> ExtractWeekColumns(IWorksheet worksheet, IReadOnlyList<FiscalYear> fiscalYears)
        {
            var columns = new List<WeekColumnData>();
            var consecutiveBlanks = 0;
            const int maxConsecutiveBlanks = 5;

            for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
            {
                var cellValue = worksheet.GetValue(0, columnIndex);
                var date = TryParseWeekDate(cellValue);

                if (!date.HasValue)
                {
                    // Check if cell is blank
                    if (IsBlank(cellValue))
                    {
                        consecutiveBlanks++;
                        if (consecutiveBlanks >= maxConsecutiveBlanks)
                        {
                            break; // Stop after 5 consecutive blank cells
                        }
                    }
                    else
                    {
                        consecutiveBlanks = 0; // Reset counter if non-blank non-date found
                    }
                    continue;
                }

                consecutiveBlanks = 0; // Reset counter when date found

                var weekDate = date.Value.Date;
                
                // Only include columns that fall within a fiscal year
                var fiscalYear = fiscalYears.FirstOrDefault(fy =>
                    weekDate >= fy.StartDate.Date && weekDate <= fy.EndDate.Date);

                if (fiscalYear != null)
                {
                    columns.Add(new WeekColumnData(columnIndex, weekDate));
                }
            }

            return columns;
        }

        private static bool IsBlank(object? value)
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

        public async Task<string> ImportAllocationPlanningAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Allocation planning workbook could not be found.", filePath);
            }

            // Ensure fiscal calendar metadata is aligned before resolving the active closing period.
            await _fiscalCalendarConsistencyService.EnsureConsistencyAsync().ConfigureAwait(false);

            using var workbook = LoadWorkbook(filePath);

            var worksheet = workbook.GetWorksheet("Alocações_Staff") ??
                            workbook.GetWorksheet("Alocacoes_Staff") ??
                            throw new InvalidDataException("Worksheet 'Alocações_Staff' is missing from the allocation planning workbook.");

            await using var context = await _contextFactory
                .CreateDbContextAsync()
                .ConfigureAwait(false);

            var nowUtc = DateTime.UtcNow;
            var today = nowUtc.Date;

            // Locate the active closing period for labeling purposes only.
            var activeClosingPeriod = await context.ClosingPeriods
                .Include(cp => cp.FiscalYear)
                .FirstOrDefaultAsync(cp => nowUtc >= cp.PeriodStart && nowUtc <= cp.PeriodEnd)
                .ConfigureAwait(false);

            var closingPeriodLabel = activeClosingPeriod != null && !string.IsNullOrWhiteSpace(activeClosingPeriod.Name)
                ? activeClosingPeriod.Name
                : $"Id={activeClosingPeriod?.Id ?? 0}";

            // Load all fiscal years to map column dates correctly
            var fiscalYears = await context.FiscalYears
                .AsNoTracking()
                .ToListAsync()
                .ConfigureAwait(false);

            if (fiscalYears.Count == 0)
            {
                throw new InvalidOperationException("No fiscal years have been configured. Add a fiscal year before importing allocation planning data.");
            }

            // Load rank mappings for normalization
            var rankMappings = await context.RankMappings
                .AsNoTracking()
                .Where(mapping => mapping.IsActive)
                .ToListAsync()
                .ConfigureAwait(false);

            var skipReasons = new Dictionary<string, IReadOnlyCollection<string>>();
            var allocationClosedEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allocationWarningMessages = new HashSet<string>(StringComparer.Ordinal);
            var unknownEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // STEP A & B: Read the file and list all ranks per engagement
            var employees = ExtractEmployeeRows(worksheet);
            var weekColumns = ExtractWeekColumns(worksheet, fiscalYears);

            if (employees.Count == 0 || weekColumns.Count == 0)
            {
                var emptyNotes = new List<string>
                {
                    "The worksheet did not contain any staff allocation records.",
                    $"Employees found: {employees.Count}",
                    $"Week columns found: {weekColumns.Count}"
                };

                return ImportSummaryFormatter.Build(
                    "Allocation planning import",
                    inserted: 0,
                    updated: 0,
                    skipReasons: null,
                    notes: emptyNotes,
                    processed: 0);
            }

            // Collect all engagement codes from the spreadsheet
            var engagementCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var employee in employees)
            {
                foreach (var week in weekColumns)
                {
                    var engagementCode = TryExtractEngagementCode(worksheet.GetValue(employee.RowIndex, week.ColumnIndex));
                    if (!string.IsNullOrEmpty(engagementCode))
                    {
                        var normalizedCode = NormalizeEngagementCode(engagementCode);
                        if (!string.IsNullOrEmpty(normalizedCode))
                        {
                            engagementCodes.Add(normalizedCode);
                        }
                    }
                }
            }

            // Load engagements and their existing budgets
            var fiscalYearIds = fiscalYears.Select(fy => fy.Id).Distinct().ToList();
            var closingPeriodId = activeClosingPeriod?.Id ?? 0;

            List<Engagement> engagements;
            if (engagementCodes.Count > 0)
            {
                engagements = await context.Engagements
                    .Include(e => e.RankBudgets.Where(b => fiscalYearIds.Contains(b.FiscalYearId)))
                    .Where(e => e.EngagementId != null && engagementCodes.Contains(e.EngagementId))
                    .ToListAsync()
                    .ConfigureAwait(false);
            }
            else
            {
                engagements = new List<Engagement>();
            }

            var engagementLookup = engagements
                .ToDictionary(e => NormalizeEngagementCode(e.EngagementId), e => e, StringComparer.OrdinalIgnoreCase);

            var budgetLookup = new Dictionary<(int EngagementId, int FiscalYearId, string RankCode), EngagementRankBudget>();
            foreach (var engagement in engagements)
            {
                if (engagement.RankBudgets is null)
                {
                    continue;
                }

                foreach (var budget in engagement.RankBudgets.Where(b => fiscalYearIds.Contains(b.FiscalYearId)))
                {
                    var normalizedRank = NormalizeRankKey(budget.RankName);
                    if (string.IsNullOrEmpty(normalizedRank))
                    {
                        continue;
                    }

                    budgetLookup[(engagement.Id, budget.FiscalYearId, normalizedRank)] = budget;
                }
            }

            // STEP C & D: Normalize ranks and ensure EngagementRankBudgets records exist
            var updateTimestamp = DateTime.UtcNow;
            var inserted = 0;
            var allEngagementRankCombinations = new HashSet<(string EngagementCode, string RawRank, int FiscalYearId)>();

            // Build rank lookup for canonical ID mapping
            var rankLookup = BuildRankLookupForCanonicalMapping(rankMappings);

            // Extract all engagement/rank/fiscal year combinations from spreadsheet
            foreach (var employee in employees)
            {
                if (string.IsNullOrWhiteSpace(employee.Rank))
                {
                    continue;
                }

                var normalizedRawRank = NormalizeRank(employee.Rank);

                foreach (var week in weekColumns)
                {
                    var engagementCode = TryExtractEngagementCode(worksheet.GetValue(employee.RowIndex, week.ColumnIndex));
                    if (string.IsNullOrEmpty(engagementCode))
                    {
                        continue;
                    }

                    var normalizedEngagement = NormalizeEngagementCode(engagementCode);
                    if (string.IsNullOrEmpty(normalizedEngagement))
                    {
                        continue;
                    }

                    // Map week date to fiscal year
                    var fiscalYear = fiscalYears.FirstOrDefault(fy =>
                        week.WeekDate >= fy.StartDate.Date && week.WeekDate <= fy.EndDate.Date);

                    if (fiscalYear is null || !fiscalYearIds.Contains(fiscalYear.Id))
                    {
                        continue;
                    }

                    allEngagementRankCombinations.Add((normalizedEngagement, normalizedRawRank, fiscalYear.Id));
                }
            }

            // Ensure all engagement/rank combinations exist in EngagementRankBudgets
            foreach (var (engagementCode, rawRank, fiscalYearId) in allEngagementRankCombinations)
            {
                if (!engagementLookup.TryGetValue(engagementCode, out var engagement))
                {
                    var rawEngagement = NormalizeWhitespace(engagementCode);
                    unknownEngagements.Add(string.IsNullOrEmpty(rawEngagement) ? "<unknown>" : rawEngagement);
                    continue;
                }

                // Skip closed engagements
                if (EngagementImportSkipEvaluator.TryCreate(engagement, out var skipMetadata) &&
                    skipMetadata.ReasonKey == "ClosedEngagement")
                {
                    var detail = $"{engagement.EngagementId} (closing period {closingPeriodLabel})";
                    allocationClosedEngagements.Add(detail);
                    allocationWarningMessages.Add(skipMetadata.WarningMessage);
                    continue;
                }

                // Convert raw rank to canonical RankCode
                if (!rankLookup.TryGetValue(rawRank, out var canonicalRankCode))
                {
                    continue;
                }

                var normalizedCanonicalRank = NormalizeRankKey(canonicalRankCode);
                if (string.IsNullOrEmpty(normalizedCanonicalRank))
                {
                    continue;
                }

                // Check if EngagementRankBudget already exists, if not create it
                var budgetKey = (engagement.Id, fiscalYearId, normalizedCanonicalRank);
                if (!budgetLookup.ContainsKey(budgetKey))
                {
                    var newBudget = new EngagementRankBudget
                    {
                        EngagementId = engagement.Id,
                        FiscalYearId = fiscalYearId,
                        RankName = canonicalRankCode, // Use canonical ID (RawRank from RankMapping)
                        BudgetHours = 0m, // BudgetHours = 0 and editable
                        ConsumedHours = 0m,
                        AdditionalHours = 0m,
                        RemainingHours = 0m,
                        Status = nameof(TrafficLightStatus.Green),
                        CreatedAtUtc = updateTimestamp,
                        UpdatedAtUtc = updateTimestamp
                    };

                    await context.EngagementRankBudgets.AddAsync(newBudget).ConfigureAwait(false);
                    budgetLookup[budgetKey] = newBudget;
                    inserted++;
                }
            }

            // Save changes from first pass
            await context.SaveChangesAsync().ConfigureAwait(false);

            // STEP E, F, G, H, I: Go back to the file, calculate consumed hours (40 hours per engagement week)
            // and aggregate by engagement/rank/fiscal year, then update ConsumedHours
            const decimal HoursPerEngagementWeek = 40m;
            var consumedHoursByEngagementRank = new Dictionary<(int EngagementId, int FiscalYearId, string RankCode), decimal>();

            foreach (var employee in employees)
            {
                if (string.IsNullOrWhiteSpace(employee.Rank))
                {
                    continue;
                }

                var normalizedRawRank = NormalizeRank(employee.Rank);
                if (!rankLookup.TryGetValue(normalizedRawRank, out var canonicalRankCode))
                {
                    continue;
                }

                var normalizedCanonicalRank = NormalizeRankKey(canonicalRankCode);
                if (string.IsNullOrEmpty(normalizedCanonicalRank))
                {
                    continue;
                }

                foreach (var week in weekColumns)
                {
                    var engagementCode = TryExtractEngagementCode(worksheet.GetValue(employee.RowIndex, week.ColumnIndex));
                    if (string.IsNullOrEmpty(engagementCode))
                    {
                        continue;
                    }

                    var normalizedEngagement = NormalizeEngagementCode(engagementCode);
                    if (string.IsNullOrEmpty(normalizedEngagement))
                    {
                        continue;
                    }

                    if (!engagementLookup.TryGetValue(normalizedEngagement, out var engagement))
                    {
                        continue;
                    }

                    // Skip closed engagements
                    if (EngagementImportSkipEvaluator.TryCreate(engagement, out var skipMetadata) &&
                        skipMetadata.ReasonKey == "ClosedEngagement")
                    {
                        continue;
                    }

                    // Map week date to fiscal year
                    var fiscalYear = fiscalYears.FirstOrDefault(fy =>
                        week.WeekDate >= fy.StartDate.Date && week.WeekDate <= fy.EndDate.Date);

                    if (fiscalYear is null || !fiscalYearIds.Contains(fiscalYear.Id))
                    {
                        continue;
                    }

                    // Aggregate hours: 40 hours per engagement week
                    var key = (engagement.Id, fiscalYear.Id, normalizedCanonicalRank);
                    if (consumedHoursByEngagementRank.TryGetValue(key, out var existingHours))
                    {
                        consumedHoursByEngagementRank[key] = existingHours + HoursPerEngagementWeek;
                    }
                    else
                    {
                        consumedHoursByEngagementRank[key] = HoursPerEngagementWeek;
                    }
                }
            }

            // Load histories for updating
            var existingHistories = closingPeriodId > 0
                ? await context.EngagementRankBudgetHistory
                    .Where(h => h.ClosingPeriodId == closingPeriodId && fiscalYearIds.Contains(h.FiscalYearId))
                    .ToListAsync()
                    .ConfigureAwait(false)
                : new List<EngagementRankBudgetHistory>();

            var historyLookup = new Dictionary<(string EngagementCode, int FiscalYearId, int ClosingPeriodId, string RankCode), EngagementRankBudgetHistory>();
            foreach (var history in existingHistories)
            {
                var normalizedCode = NormalizeEngagementCode(history.EngagementCode);
                var normalizedRank = NormalizeRankKey(history.RankCode);
                if (string.IsNullOrEmpty(normalizedCode) || string.IsNullOrEmpty(normalizedRank))
                {
                    continue;
                }

                historyLookup[(normalizedCode, history.FiscalYearId, history.ClosingPeriodId, normalizedRank)] = history;
            }

            // Update ConsumedHours based on aggregated values
            var updated = 0;
            var historyUpserts = 0;
            var processedHistoryKeys = new HashSet<(string EngagementCode, int FiscalYearId, int ClosingPeriodId, string RankCode)>();

            foreach (var (key, totalHours) in consumedHoursByEngagementRank)
            {
                var (engagementId, fiscalYearId, rankCode) = key;

                if (!budgetLookup.TryGetValue(key, out var budget))
                {
                    continue;
                }

                var engagement = engagements.FirstOrDefault(e => e.Id == engagementId);
                if (engagement is null)
                {
                    continue;
                }

                var normalizedEngagementCode = NormalizeEngagementCode(engagement.EngagementId);
                var roundedHours = Math.Round(totalHours, 2, MidpointRounding.AwayFromZero);

                // Update ConsumedHours
                var previousConsumed = budget.ConsumedHours;
                var previousRemaining = budget.RemainingHours;
                budget.ConsumedHours = roundedHours;

                // Recalculate remaining hours
                var remaining = budget.BudgetHours + budget.AdditionalHours - budget.ConsumedHours;
                budget.RemainingHours = Math.Round(remaining, 2, MidpointRounding.AwayFromZero);

                if (Math.Abs(previousConsumed - budget.ConsumedHours) > 0.005m ||
                    Math.Abs(previousRemaining - budget.RemainingHours) > 0.005m)
                {
                    budget.UpdatedAtUtc = updateTimestamp;
                    updated++;
                }

                // Update history
                var historyKey = (normalizedEngagementCode, fiscalYearId, closingPeriodId, rankCode);
                if (historyLookup.TryGetValue(historyKey, out var historyEntry))
                {
                    if (historyEntry.Hours != roundedHours)
                    {
                        historyEntry.Hours = roundedHours;
                    }

                    historyEntry.UploadedAt = updateTimestamp;
                }
                else
                {
                    var history = new EngagementRankBudgetHistory
                    {
                        EngagementCode = normalizedEngagementCode,
                        RankCode = rankCode,
                        FiscalYearId = fiscalYearId,
                        ClosingPeriodId = closingPeriodId,
                        Hours = roundedHours,
                        UploadedAt = updateTimestamp
                    };

                    await context.EngagementRankBudgetHistory.AddAsync(history).ConfigureAwait(false);
                    historyLookup[historyKey] = history;
                }

                processedHistoryKeys.Add(historyKey);
                historyUpserts++;
            }

            // Any history rows that were not present in the import must be reverted from the live budgets.
            foreach (var (historyKey, historyEntry) in historyLookup)
            {
                if (processedHistoryKeys.Contains(historyKey))
                {
                    continue;
                }

                var previousHours = historyEntry.Hours;
                if (previousHours == 0m)
                {
                    continue;
                }

                if (engagementLookup.TryGetValue(historyKey.EngagementCode, out var engagement) &&
                    budgetLookup.TryGetValue((engagement.Id, historyKey.FiscalYearId, historyKey.RankCode), out var budget))
                {
                    var previousConsumed = budget.ConsumedHours;
                    var previousRemaining = budget.RemainingHours;
                    budget.ConsumedHours = Math.Max(0m, budget.ConsumedHours - previousHours);

                    // Recalculate remaining hours
                    var remaining = budget.BudgetHours + budget.AdditionalHours - budget.ConsumedHours;
                    budget.RemainingHours = Math.Round(remaining, 2, MidpointRounding.AwayFromZero);

                    if (Math.Abs(previousConsumed - budget.ConsumedHours) > 0.005m ||
                        Math.Abs(previousRemaining - budget.RemainingHours) > 0.005m)
                    {
                        budget.UpdatedAtUtc = updateTimestamp;
                        updated++;
                    }
                }

                historyEntry.Hours = 0m;
                historyEntry.UploadedAt = updateTimestamp;
                processedHistoryKeys.Add(historyKey);
                historyUpserts++;
            }

            // Save all changes to the database
            await context.SaveChangesAsync().ConfigureAwait(false);

            // Note: ManualOnly (S/4 projects) are no longer skipped for GRC allocation planning import

            if (allocationClosedEngagements.Count > 0)
            {
                skipReasons["ClosedEngagement"] = allocationClosedEngagements.ToList();
            }

            if (unknownEngagements.Count > 0)
            {
                skipReasons[MissingEngagementWarningKey] = unknownEngagements
                    .Select(e => $"{e} (closing period {closingPeriodLabel})")
                    .ToList();
            }

            var processedRowCount = consumedHoursByEngagementRank.Count;
            var distinctEngagementCount = consumedHoursByEngagementRank.Keys
                .Select(k => k.EngagementId)
                .Distinct()
                .Count();
            var distinctRankCount = consumedHoursByEngagementRank.Keys
                .Select(k => k.RankCode)
                .Distinct()
                .Count();

            var notes = new List<string>(8 + allocationWarningMessages.Count)
            {
                $"Engagement/rank combinations processed: {processedRowCount}",
                $"Distinct engagements detected: {distinctEngagementCount}",
                $"Distinct ranks detected: {distinctRankCount}",
                $"History entries updated: {historyUpserts}",
                $"Engagement budgets inserted: {inserted}",
                $"Engagement budgets updated: {updated}",
                $"Unknown engagements skipped: {unknownEngagements.Count}"
            };

            if (allocationWarningMessages.Count > 0)
            {
                foreach (var warning in allocationWarningMessages.OrderBy(message => message, StringComparer.Ordinal))
                {
                    notes.Insert(0, warning);
                }
            }

            return ImportSummaryFormatter.Build(
                "Allocation planning import",
                inserted,
                updated,
                skipReasons.Count > 0 ? skipReasons : null,
                notes,
                processed: processedRowCount);
        }

        private static (string FiscalYearName, DateTime? LastUpdateDate) ParseFcsMetadata(IWorksheet worksheet)
        {
            var rawValue = GetCellString(worksheet, 3, 0);
            string normalized;
            string fiscalYearName;

            if (!TryExtractFiscalYearCode(rawValue, out normalized, out fiscalYearName))
            {
                for (var rowIndex = 0; rowIndex < Math.Min(worksheet.RowCount, FcsHeaderSearchLimit); rowIndex++)
                {
                    var candidate = GetCellString(worksheet, rowIndex, 0);
                    if (!TryExtractFiscalYearCode(candidate, out normalized, out fiscalYearName))
                    {
                        continue;
                    }

                    rawValue = candidate;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                throw new InvalidDataException("Cell A4 must contain the fiscal year metadata for the FCS backlog workbook.");
            }

            if (string.IsNullOrEmpty(normalized))
            {
                normalized = NormalizeWhitespace(rawValue);
            }

            var resolvedFiscalYearName = fiscalYearName;
            if (string.IsNullOrEmpty(resolvedFiscalYearName))
            {
                var digits = ExtractDigits(normalized);
                if (string.IsNullOrEmpty(digits))
                {
                    throw new InvalidDataException($"Cell A4 must specify the current fiscal year (e.g., FY26). Detected value: '{normalized}'.");
                }

                resolvedFiscalYearName = $"FY{digits}";
            }

            DateTime? lastUpdateDate = null;
            foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryParseAsOfDate(token, out var parsedDate))
                {
                    lastUpdateDate = parsedDate.Date;
                    break;
                }
            }

            return (resolvedFiscalYearName, lastUpdateDate);
        }

        private static string IncrementFiscalYearName(string fiscalYearName)
        {
            if (string.IsNullOrWhiteSpace(fiscalYearName))
            {
                throw new ArgumentException("Fiscal year name must be provided.", nameof(fiscalYearName));
            }

            var match = DigitsRegex.Match(fiscalYearName);
            if (!match.Success)
            {
                throw new InvalidDataException($"Unable to determine next fiscal year based on '{fiscalYearName}'.");
            }

            if (!int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentYear))
            {
                throw new InvalidDataException($"Unable to parse fiscal year component from '{fiscalYearName}'.");
            }

            var prefix = fiscalYearName[..match.Index];
            var suffix = fiscalYearName[(match.Index + match.Length)..];

            var format = new string('0', match.Length);
            var nextYearText = currentYear + 1;
            var formattedNumber = format.Length > 0
                ? nextYearText.ToString(format, CultureInfo.InvariantCulture)
                : nextYearText.ToString(CultureInfo.InvariantCulture);

            return (prefix + formattedNumber + suffix).ToUpperInvariant();
        }

        private static bool IsRowEmpty(IWorksheet worksheet, int rowIndex)
        {
            for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
            {
                var item = worksheet.GetValue(rowIndex, columnIndex);
                if (item == null || item == DBNull.Value)
                {
                    continue;
                }

                var text = NormalizeWhitespace(Convert.ToString(item, CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(text))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseAsOfDate(string text, out DateTime parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (DateTime.TryParseExact(text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate))
            {
                parsed = exactDate;
                return true;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var fallback))
            {
                parsed = fallback;
                return true;
            }

            return false;
        }

        private static decimal? ParseDecimal(object? value, int? decimals)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            decimal parsed;
            switch (value)
            {
                case decimal dec:
                    parsed = dec;
                    break;
                case double dbl:
                    parsed = Convert.ToDecimal(dbl);
                    break;
                case float flt:
                    parsed = Convert.ToDecimal(flt);
                    break;
                case int i:
                    parsed = i;
                    break;
                case long l:
                    parsed = l;
                    break;
                case string str:
                    var sanitized = SanitizeNumericString(str);
                    if (sanitized.Length == 0)
                    {
                        return null;
                    }

                    if (!decimal.TryParse(sanitized, NumberStyles.Number | NumberStyles.AllowLeadingSign, PtBrCulture, out parsed) &&
                        !decimal.TryParse(sanitized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsed))
                    {
                        throw new InvalidDataException($"Unable to parse decimal value '{str}'.");
                    }

                    break;
                default:
                    try
                    {
                        parsed = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Unable to parse decimal value '{value}'.", ex);
                    }

                    break;
            }

            if (decimals.HasValue)
            {
                parsed = Math.Round(parsed, decimals.Value, MidpointRounding.AwayFromZero);
            }

            return parsed;
        }

        private static string SanitizeNumericString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sanitized = value
                .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("%", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("\u00A0", string.Empty, StringComparison.Ordinal);

            sanitized = NormalizeWhitespace(sanitized);

            var isNegative = sanitized.StartsWith("(") && sanitized.EndsWith(")");
            if (isNegative)
            {
                sanitized = sanitized.Trim('(', ')');
            }

            sanitized = sanitized.Replace(".", string.Empty, StringComparison.Ordinal);
            sanitized = sanitized.Replace(",", ".", StringComparison.Ordinal);

            if (isNegative)
            {
                sanitized = string.Concat("-", sanitized);
            }

            return sanitized;
        }

        private static string NormalizeEngagementCode(string? value)
        {
            var normalized = NormalizeWhitespace(value);
            return string.IsNullOrEmpty(normalized) ? string.Empty : normalized.ToUpperInvariant();
        }

        private static string NormalizeRankKey(string? value)
        {
            var normalized = NormalizeWhitespace(value);
            return string.IsNullOrEmpty(normalized) ? string.Empty : normalized.ToUpperInvariant();
        }

        private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed record FcsBacklogRow(string EngagementId, decimal CurrentBacklog, decimal FutureBacklog, int RowNumber);

        private static string NormalizeAllocationCode(string? value)
        {
            return NormalizeEngagementCode(value); // Reuse existing method for consistency
        }

        private readonly record struct AllocationKey(string EngagementCode, string RankCode, int FiscalYearId);

        private sealed class AllocationKeyComparer : IEqualityComparer<AllocationKey>
        {
            public static AllocationKeyComparer Instance { get; } = new();

            public bool Equals(AllocationKey x, AllocationKey y)
            {
                return string.Equals(x.EngagementCode, y.EngagementCode, StringComparison.Ordinal) &&
                       string.Equals(x.RankCode, y.RankCode, StringComparison.Ordinal) &&
                       x.FiscalYearId == y.FiscalYearId;
            }

            public int GetHashCode(AllocationKey obj)
            {
                return HashCode.Combine(obj.EngagementCode, obj.RankCode, obj.FiscalYearId);
            }
        }

        private readonly record struct BudgetKey(string EngagementCode, string RankCode);

        private sealed class BudgetKeyComparer : IEqualityComparer<BudgetKey>
        {
            public static BudgetKeyComparer Instance { get; } = new();

            public bool Equals(BudgetKey x, BudgetKey y)
            {
                return string.Equals(x.EngagementCode, y.EngagementCode, StringComparison.Ordinal) &&
                       string.Equals(x.RankCode, y.RankCode, StringComparison.Ordinal);
            }

            public int GetHashCode(BudgetKey obj)
            {
                return HashCode.Combine(obj.EngagementCode, obj.RankCode);
            }
        }

        private sealed class EtcpImportRow
        {
            public int RowNumber { get; init; }
            public string CustomerName { get; init; } = string.Empty;
            public string CustomerCode { get; init; } = string.Empty;
            public string EngagementDescription { get; init; } = string.Empty;
            public string EngagementId { get; init; } = string.Empty;
            public string Currency { get; init; } = string.Empty;
            public string StatusText { get; init; } = string.Empty;
            public decimal? BudgetHours { get; init; }
            public decimal? EstimatedToCompleteHours { get; init; }
            public decimal? BudgetValue { get; init; }
            public decimal? EtcpValue { get; init; }
            public decimal? MarginBudget { get; init; }
            public decimal? MarginEtcp { get; init; }
            public decimal? BudgetExpenses { get; init; }
            public decimal? EtcpExpenses { get; init; }
            public int? EtcpAgeDays { get; init; }
        }
    }
}