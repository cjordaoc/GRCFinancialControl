using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static GRCFinancialControl.Persistence.Services.Utilities.DataNormalizationService;

namespace GRCFinancialControl.Persistence.Services.Importers.Budget
{
    /// <summary>
    /// Imports budget Excel workbooks (PLAN INFO + RESOURCING sheets).
    /// Fully extracted from ImportService - no delegation.
    /// 
    /// Creates/updates:
    /// - Engagements (with initial budget)
    /// - Customers  
    /// - RankBudgets (by fiscal year and rank)
    /// - Employees (with rank mapping)
    /// - RankMappings (active mappings from spreadsheet)
    /// </summary>
    public sealed class BudgetImporter : ImportServiceBase
    {
        private static readonly Regex TrailingDigitsRegex = new(@"\d+$", RegexOptions.Compiled);
        private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly FileStreamOptions SharedReadOptions = new()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite
        };

        private readonly IFiscalCalendarConsistencyService _fiscalCalendarConsistencyService;

        public BudgetImporter(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<BudgetImporter> logger,
            IFiscalCalendarConsistencyService fiscalCalendarConsistencyService)
            : base(contextFactory, logger)
        {
            _fiscalCalendarConsistencyService = fiscalCalendarConsistencyService ?? 
                throw new ArgumentNullException(nameof(fiscalCalendarConsistencyService));
        }

        /// <summary>
        /// Imports a budget workbook and creates/updates engagement with rank budgets.
        /// </summary>
        public async Task<string> ImportAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Budget workbook could not be found.", filePath);
            }

            Logger.LogInformation("Budget import started for file: {FilePath}", filePath);

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

            await using var strategyContext = await ContextFactory
                .CreateDbContextAsync()
                .ConfigureAwait(false);
            var strategy = strategyContext.Database.CreateExecutionStrategy();

            var result = await strategy.ExecuteAsync(async () =>
            {
                await using var context = await ContextFactory
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
                        Logger.LogWarning(skipMetadata.WarningMessage);
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

            Logger.LogInformation("Budget import completed successfully");
            return result;
        }

        #region Excel Loading and Parsing

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

            var headerMap = BuildHeaderMapFromWorksheet(resourcing, headerRowIndex);
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

            var levelColumnIndex = GetRequiredColumnIndexFromMap(headerMap, "level");
            var employeeColumnIndex = GetRequiredColumnIndexFromMap(headerMap, "employee");
            var guiColumnIndex = GetOptionalColumnIndexFromMap(headerMap, "gui number");
            var mrsColumnIndex = GetOptionalColumnIndexFromMap(headerMap, "mrs");
            var gdsColumnIndex = GetOptionalColumnIndexFromMap(headerMap, "gds");
            var costCenterColumnIndex = GetOptionalColumnIndexFromMap(headerMap, "cost center");
            var officeColumnIndex = GetOptionalColumnIndexFromMap(headerMap, "office");

            var dataRowIndex = headerRowIndex + 1;
            var consecutiveBlankRows = 0;

            while (dataRowIndex < resourcing.RowCount && consecutiveBlankRows < 10)
            {
                var rawRank = NormalizeWhitespace(GetCellString(resourcing, dataRowIndex, levelColumnIndex));
                var (hours, hasHoursValue) = ParseHoursValue(GetCellValue(resourcing, dataRowIndex, hoursColumnIndex));

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
                    var guiValue = guiColumnIndex >= 0 ? NormalizeEmployeeIdentifier(GetCellValue(resourcing, dataRowIndex, guiColumnIndex)) : string.Empty;
                    var mrsValue = mrsColumnIndex >= 0 ? NormalizeEmployeeIdentifier(GetCellValue(resourcing, dataRowIndex, mrsColumnIndex)) : string.Empty;
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

        private static Dictionary<string, int> BuildHeaderMapFromWorksheet(IWorksheet table, int headerRowIndex)
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

        private static int GetRequiredColumnIndexFromMap(IReadOnlyDictionary<string, int> headerMap, string keyword)
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

        private static int GetOptionalColumnIndexFromMap(IReadOnlyDictionary<string, int> headerMap, string keyword)
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
                var dateValue = TryParseDate(cell);
                if (dateValue.HasValue)
                {
                    weekStarts.Add(NormalizeWeekStart(dateValue.Value));
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

        #endregion

        #region Normalization and Parsing

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

        private static string NormalizeEmployeeIdentifier(object? value)
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

        private static (decimal value, bool hasValue) ParseHoursValue(object? cellValue)
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

        #endregion

        #region Database Operations

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

        private static async Task EnsureCustomerCodeAsync(ApplicationDbContext context, Customer customer)
        {
            if (customer == null)
            {
                throw new ArgumentNullException(nameof(customer));
            }

            if (customer.Id > 0 && !string.IsNullOrEmpty(customer.CustomerCode))
            {
                return;
            }

            var highestCode = await context.Customers
                .Where(c => c.CustomerCode != null && c.CustomerCode.StartsWith("CLI-"))
                .Select(c => c.CustomerCode)
                .ToListAsync()
                .ConfigureAwait(false);

            var maxNumber = 0;
            foreach (var code in highestCode)
            {
                if (code.Length > 4 && int.TryParse(code.Substring(4), out var number))
                {
                    if (number > maxNumber)
                    {
                        maxNumber = number;
                    }
                }
            }

            customer.CustomerCode = $"CLI-{maxNumber + 1:D4}";
        }

        private static List<RankBudgetAggregate> AggregateRankBudgets(IReadOnlyCollection<RankBudgetRow> rows)
        {
            if (rows.Count == 0)
            {
                return new List<RankBudgetAggregate>(0);
            }

            var aggregated = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var normalized = NormalizeRankName(row.RawRank);
                if (string.IsNullOrEmpty(normalized))
                {
                    continue;
                }

                if (aggregated.ContainsKey(normalized))
                {
                    aggregated[normalized] += row.Hours;
                }
                else
                {
                    aggregated[normalized] = row.Hours;
                }
            }

            var result = new List<RankBudgetAggregate>(aggregated.Count);
            foreach (var kvp in aggregated)
            {
                result.Add(new RankBudgetAggregate(kvp.Key, kvp.Value));
            }

            return result;
        }

        private static int ApplyBudgetSnapshot(
            Engagement engagement,
            IReadOnlyList<FiscalYear> fiscalYears,
            IReadOnlyCollection<RankBudgetAggregate> aggregatedBudgets,
            DateTime snapshotTimestamp)
        {
            if (engagement == null)
            {
                throw new ArgumentNullException(nameof(engagement));
            }

            if (fiscalYears == null || fiscalYears.Count == 0)
            {
                throw new ArgumentException("At least one fiscal year must be provided.", nameof(fiscalYears));
            }

            var firstFiscalYear = fiscalYears[0];
            var insertedCount = 0;

            foreach (var budget in aggregatedBudgets)
            {
                var existing = engagement.RankBudgets
                    .FirstOrDefault(r =>
                        string.Equals(r.RankName, budget.RankName, StringComparison.OrdinalIgnoreCase) &&
                        r.FiscalYearId == firstFiscalYear.Id);

                if (existing != null)
                {
                    existing.BudgetHours = budget.Hours;
                    existing.UpdatedAtUtc = snapshotTimestamp;
                }
                else
                {
                    engagement.RankBudgets.Add(new EngagementRankBudget
                    {
                        RankName = budget.RankName,
                        BudgetHours = budget.Hours,
                        FiscalYearId = firstFiscalYear.Id,
                        CreatedAtUtc = snapshotTimestamp,
                        UpdatedAtUtc = snapshotTimestamp
                    });
                    insertedCount++;
                }
            }

            return insertedCount;
        }

        #endregion

        #region Helper Types

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

        #endregion
    }
}
