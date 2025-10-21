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
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services
{
    public class ImportService : IImportService
    {
        private static readonly FileStreamOptions SharedReadOptions = new()
        {
            Access = FileAccess.Read,
            Mode = FileMode.Open,
            Share = FileShare.ReadWrite
        };

        static ImportService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<ImportService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IFiscalCalendarConsistencyService _fiscalCalendarConsistencyService;
        private readonly IStaffAllocationForecastService _staffAllocationForecastService;
        private const string FinancialEvolutionInitialPeriodId = "INITIAL";
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
        private static readonly Regex MultiWhitespaceRegex = new Regex("\\s+", RegexOptions.Compiled);
        private static readonly Regex DigitsRegex = new Regex("\\d+", RegexOptions.Compiled);
        private static readonly Regex TrailingDigitsRegex = new Regex(@"\d+$", RegexOptions.Compiled);
        private static readonly Regex FiscalYearCodeRegex = new Regex(@"FY\\d{2,4}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EngagementIdRegex = new Regex(@"\\bE-\\d+\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LastUpdateDateRegex = new Regex(@"Last Update\\s*:\\s*(\\d{1,2}\\s+[A-Za-z]{3}\\s+\\d{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");

        public ImportService(IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<ImportService> logger,
            ILoggerFactory loggerFactory,
            IFiscalCalendarConsistencyService fiscalCalendarConsistencyService,
            IStaffAllocationForecastService staffAllocationForecastService)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(fiscalCalendarConsistencyService);
            ArgumentNullException.ThrowIfNull(staffAllocationForecastService);

            _contextFactory = contextFactory;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _fiscalCalendarConsistencyService = fiscalCalendarConsistencyService;
            _staffAllocationForecastService = staffAllocationForecastService;
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

            var dataSet = LoadWorkbookDataSet(filePath);

            var planInfo = ResolveWorksheet(dataSet, "PLAN INFO") ??
                           throw new InvalidDataException("Worksheet 'PLAN INFO' is missing from the budget workbook.");
            var resourcing = ResolveWorksheet(dataSet, "RESOURCING") ??
                             throw new InvalidDataException("Worksheet 'RESOURCING' is missing from the budget workbook.");

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
            var rankBudgetsFromFile = resourcingParseResult.RankBudgets;
            var totalBudgetHours = rankBudgetsFromFile.Sum(r => r.Hours);
            var generatedAtUtc = ExtractGeneratedTimestampUtc(planInfo);

            await using var strategyContext = await _contextFactory.CreateDbContextAsync();
            var strategy = strategyContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    var normalizedCustomerName = customerName;
                    var normalizedCustomerLookup = normalizedCustomerName.ToLowerInvariant();
                    var existingCustomer = await context.Customers
                        .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedCustomerLookup);

                    bool customerCreated = false;
                    Customer customer;
                    if (existingCustomer == null)
                    {
                        customer = new Customer
                        {
                            Name = normalizedCustomerName
                        };
                        await context.Customers.AddAsync(customer);
                        customerCreated = true;
                    }
                    else
                    {
                        existingCustomer.Name = normalizedCustomerName;
                        customer = existingCustomer;
                    }

                    var engagement = await context.Engagements
                        .Include(e => e.RankBudgets)
                        .FirstOrDefaultAsync(e => e.EngagementId == engagementKey);

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

                        await context.Engagements.AddAsync(engagement);
                        engagementCreated = true;
                    }

                    else
                    {
                        if (engagement.Source == EngagementSource.S4Project)
                        {
                            var manualOnlyMessage =
                                $"Engagement '{engagement.EngagementId}' is sourced from S/4Project and must be managed manually. " +
                                "Budget workbook import skipped.";

                            _logger.LogInformation(
                                "Skipping budget import for engagement {EngagementId} from file {FilePath} because it is manual-only (source: {Source}).",
                                engagement.EngagementId,
                                filePath,
                                engagement.Source);

                            await transaction.RollbackAsync();
                            var skipReasons = new Dictionary<string, IReadOnlyCollection<string>>
                            {
                                ["ManualOnly"] = new[] { engagement.EngagementId }
                            };

                            var skipNotes = new List<string>
                            {
                                manualOnlyMessage
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
                    }

                    await UpsertRankMappingsAsync(context, resourcingParseResult.RankMappings, generatedAtUtc);
                    await UpsertEmployeesAsync(context, resourcingParseResult.Employees);

                    engagement.Customer = customer;
                    if (customer.Id > 0)
                    {
                        engagement.CustomerId = customer.Id;
                    }

                    if (engagement.RankBudgets == null)
                    {
                        engagement.RankBudgets = new List<EngagementRankBudget>();
                    }
                    else
                    {
                        // Full replace behavior
                        engagement.RankBudgets.Clear();
                    }

                    var now = DateTime.UtcNow;
                    foreach (var rankBudget in rankBudgetsFromFile)
                    {
                        var budget = new EngagementRankBudget
                        {
                            Engagement = engagement,
                            RankName = rankBudget.RawRank,
                            Hours = rankBudget.Hours,
                            CreatedAtUtc = now
                        };

                        engagement.RankBudgets.Add(budget);
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var customersInserted = customerCreated ? 1 : 0;
                    var customersUpdated = customerCreated ? 0 : 1;
                    var engagementsInserted = engagementCreated ? 1 : 0;
                    var engagementsUpdated = engagementCreated ? 0 : 1;

                    var notes = new List<string>
                    {
                        $"Customers inserted: {customersInserted}, updated: {customersUpdated}",
                        $"Engagements inserted: {engagementsInserted}, updated: {engagementsUpdated}",
                        $"Rank budgets processed: {rankBudgetsFromFile.Count}",
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
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        private static ResourcingParseResult ParseResourcing(DataTable resourcing)
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

            var rankBudgets = new List<RankBudgetRow>();
            var rankMappings = new Dictionary<string, RankMappingCandidate>(StringComparer.OrdinalIgnoreCase);
            var employees = new List<ResourcingEmployee>();
            var issues = new List<string>();

            var levelColumnIndex = GetRequiredColumnIndex(headerMap, "level");
            var employeeColumnIndex = GetRequiredColumnIndex(headerMap, "employee");
            var guiColumnIndex = GetOptionalColumnIndex(headerMap, "gui number");
            var mrsColumnIndex = GetOptionalColumnIndex(headerMap, "mrs");
            var gdsColumnIndex = GetOptionalColumnIndex(headerMap, "gds");
            var costCenterColumnIndex = GetOptionalColumnIndex(headerMap, "cost center");
            var officeColumnIndex = GetOptionalColumnIndex(headerMap, "office");

            var dataRowIndex = headerRowIndex + 1;
            var consecutiveBlankRows = 0;

            while (dataRowIndex < resourcing.Rows.Count && consecutiveBlankRows < 10)
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

                rankBudgets.Add(new RankBudgetRow(rawRank, hours));

                if (!rankMappings.ContainsKey(rawRank))
                {
                    rankMappings[rawRank] = new RankMappingCandidate(rawRank, NormalizeRankName(rawRank));
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

        private static int FindResourcingHeaderRow(DataTable table)
        {
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                var hasLevel = false;
                var hasEmployee = false;

                for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                {
                    var text = NormalizeWhitespace(Convert.ToString(row[columnIndex], CultureInfo.InvariantCulture));
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

        private static Dictionary<string, int> BuildHeaderMap(DataTable table, int headerRowIndex)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = table.Rows[headerRowIndex];

            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                var header = NormalizeWhitespace(Convert.ToString(headerRow[columnIndex], CultureInfo.InvariantCulture));
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

        private static int FindFirstHeaderColumnIndex(DataTable table, int headerRowIndex, string headerValue)
        {
            var headerRow = table.Rows[headerRowIndex];

            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                var value = NormalizeWhitespace(Convert.ToString(headerRow[columnIndex], CultureInfo.InvariantCulture));
                if (string.Equals(value, headerValue, StringComparison.OrdinalIgnoreCase))
                {
                    return columnIndex;
                }
            }

            return -1;
        }

        private static List<DateTime> ExtractWeekStartDates(DataTable table, int rowIndex, int startColumnIndex)
        {
            var weekStarts = new SortedSet<DateTime>();

            if (rowIndex < 0 || rowIndex >= table.Rows.Count)
            {
                return weekStarts.ToList();
            }

            for (var columnIndex = startColumnIndex; columnIndex < table.Columns.Count; columnIndex++)
            {
                var cell = GetCellValue(table, rowIndex, columnIndex);
                if (TryParseDate(cell, out var date))
                {
                    weekStarts.Add(NormalizeWeekStart(date));
                }
            }

            return weekStarts.ToList();
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

        private static DateTime NormalizeWeekStart(DateTime date)
        {
            var start = date.Date;
            var offset = ((int)start.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return start.AddDays(-offset);
        }

        private static DateTime? ExtractGeneratedTimestampUtc(DataTable planInfo)
        {
            if (planInfo == null)
            {
                return null;
            }

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

        private sealed record RankMappingCandidate(string RawRank, string NormalizedRank);

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
            var rawRanks = candidates.Select(c => c.RawRank).ToList();
            var existingMappings = await context.RankMappings
                .Where(r => rawRanks.Contains(r.RawRank))
                .ToDictionaryAsync(r => r.RawRank, StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in candidates)
            {
                if (existingMappings.TryGetValue(candidate.RawRank, out var mapping))
                {
                    mapping.NormalizedRank = candidate.NormalizedRank;
                    mapping.IsActive = true;
                    mapping.LastSeenAt = timestamp;
                }
                else
                {
                    context.RankMappings.Add(new RankMapping
                    {
                        RawRank = candidate.RawRank,
                        NormalizedRank = candidate.NormalizedRank,
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

            var identifiers = distinctEmployees.Select(e => e.Identifier).ToList();
            var existingEmployees = await context.Employees
                .Where(e => identifiers.Contains(e.Gpn))
                .ToDictionaryAsync(e => e.Gpn, StringComparer.OrdinalIgnoreCase);

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

        public async Task<StaffAllocationProcessingResult> AnalyzeStaffAllocationsAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Staff allocation workbook could not be found.", filePath);
            }

            var dataSet = LoadWorkbookDataSet(filePath);

            var worksheet = ResolveWorksheet(dataSet, "Alocações_Staff") ??
                            ResolveWorksheet(dataSet, "Alocacoes_Staff") ??
                            throw new InvalidDataException("Worksheet 'Alocações_Staff' is missing from the staff allocation workbook.");

            await using var context = await _contextFactory.CreateDbContextAsync();
            var employees = await context.Employees.AsNoTracking().ToListAsync();
            var employeeLookup = employees.ToDictionary(e => e.Gpn, StringComparer.OrdinalIgnoreCase);
            var fiscalYears = await context.FiscalYears.AsNoTracking().ToListAsync();
            var closingPeriods = await context.ClosingPeriods.AsNoTracking().ToListAsync();

            var uploadTimestampUtc = File.GetLastWriteTimeUtc(filePath);
            if (uploadTimestampUtc == DateTime.MinValue)
            {
                uploadTimestampUtc = DateTime.UtcNow;
            }

            var parserLogger = _loggerFactory.CreateLogger<StaffAllocationWorksheetParser>();
            var parser = new StaffAllocationWorksheetParser(new StaffAllocationSchemaAnalyzer(), parserLogger);
            var processor = new StaffAllocationProcessor(parser);

            var processingResult = processor.Process(worksheet, employeeLookup, uploadTimestampUtc, fiscalYears, closingPeriods);

            var summary = processingResult.Summary;
            _logger.LogInformation(
                "Staff allocation summary — rows processed: {Rows}, distinct engagements: {Engagements}, distinct ranks: {Ranks}.",
                summary.ProcessedRowCount,
                summary.DistinctEngagementCount,
                summary.DistinctRankCount);

            StaffAllocationForecastUpdateResult? forecastUpdate = null;
            if (processingResult.MappedRecords.Count > 0)
            {
                forecastUpdate = await _staffAllocationForecastService
                    .UpdateForecastAsync(processingResult.MappedRecords)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Forecast update — processed {Records} records, updated {Engagements} engagements, missing engagements: {MissingEngagementsCount}, missing budgets: {MissingBudgetsCount}, unknown ranks: {UnknownRanksCount}.",
                    forecastUpdate.ProcessedRecords,
                    forecastUpdate.UpdatedEngagements,
                    forecastUpdate.MissingEngagements.Count,
                    forecastUpdate.MissingBudgets.Count,
                    forecastUpdate.UnknownRanks.Count);

                if (forecastUpdate.MissingEngagements.Count > 0)
                {
                    _logger.LogWarning("Missing engagements during forecast update: {MissingEngagements}.", string.Join(", ", forecastUpdate.MissingEngagements));
                }

                if (forecastUpdate.MissingBudgets.Count > 0)
                {
                    _logger.LogWarning("Missing rank budgets during forecast update: {MissingBudgets}.", string.Join(", ", forecastUpdate.MissingBudgets));
                }

                if (forecastUpdate.UnknownRanks.Count > 0)
                {
                    _logger.LogWarning("Unknown ranks encountered during forecast update: {UnknownRanks}.", string.Join(", ", forecastUpdate.UnknownRanks));
                }

                var inconsistentRows = forecastUpdate.Rows
                    .Where(row => !string.Equals(row.Status, "OK", StringComparison.OrdinalIgnoreCase))
                    .Select(row => $"{row.EngagementCode}/{row.FiscalYearName}/{row.Rank}:{row.Status}")
                    .ToList();

                if (inconsistentRows.Count > 0)
                {
                    var sample = string.Join("; ", inconsistentRows.Take(5));
                    _logger.LogWarning(
                        "Forecast inconsistencies detected: {Total} (risks: {RiskCount}, overruns: {OverrunCount}). Sample: {Sample}",
                        inconsistentRows.Count,
                        forecastUpdate.RiskCount,
                        forecastUpdate.OverrunCount,
                        sample);
                }
                else
                {
                    _logger.LogInformation("No forecast inconsistencies detected in the current import.");
                }
            }

            return new StaffAllocationProcessingResult(
                processingResult.ParseResult,
                processingResult.Summary,
                processingResult.MappedRecords,
                forecastUpdate);
        }

        private static string ExtractDescription(string rawDescription)
        {
            if (string.IsNullOrEmpty(rawDescription))
            {
                return string.Empty;
            }

            var marker = "Budget-";
            var index = rawDescription.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var value = rawDescription[(index + marker.Length)..];
                return NormalizeWhitespace(value);
            }

            return NormalizeWhitespace(rawDescription);
        }

        private static DataTable? ResolveWorksheet(DataSet dataSet, string worksheetName)
        {
            var target = NormalizeSheetName(worksheetName);

            foreach (DataTable table in dataSet.Tables)
            {
                if (NormalizeSheetName(table.TableName ?? string.Empty) == target)
                {
                    return table;
                }
            }

            return null;
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

        private static string NormalizeWhitespace(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return MultiWhitespaceRegex.Replace(value.Trim(), " ");
        }

        private static object? GetCellValue(DataTable table, int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || columnIndex < 0)
            {
                return null;
            }

            if (table.Rows.Count <= rowIndex)
            {
                return null;
            }

            if (table.Columns.Count <= columnIndex)
            {
                return null;
            }

            return table.Rows[rowIndex][columnIndex];
        }

        private static string GetCellString(DataTable table, int rowIndex, int columnIndex)
        {
            var value = GetCellValue(table, rowIndex, columnIndex);
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static DataSet LoadWorkbookDataSet(string filePath)
        {
            using var stream = new FileStream(filePath, SharedReadOptions);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            return reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });
        }

        private static (Dictionary<int, string> Map, int HeaderRowIndex) BuildFcsHeaderMap(DataTable worksheet)
        {
            Dictionary<int, string>? fallbackMap = null;
            var fallbackIndex = -1;
            var searchLimit = Math.Min(worksheet.Rows.Count, FcsHeaderSearchLimit);

            for (var rowIndex = 0; rowIndex < searchLimit; rowIndex++)
            {
                var row = worksheet.Rows[rowIndex];
                var currentMap = new Dictionary<int, string>();
                var hasContent = false;

                for (var columnIndex = 0; columnIndex < worksheet.Columns.Count; columnIndex++)
                {
                    var headerText = NormalizeWhitespace(Convert.ToString(row[columnIndex], CultureInfo.InvariantCulture));
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

        private static int GetRequiredColumnIndex(Dictionary<int, string> headerMap, IEnumerable<string> candidates, string friendlyName)
        {
            foreach (var candidate in candidates)
            {
                var normalizedCandidate = candidate.ToLowerInvariant();
                foreach (var kvp in headerMap)
                {
                    if (!string.IsNullOrEmpty(kvp.Value) && kvp.Value.Contains(normalizedCandidate, StringComparison.Ordinal))
                    {
                        return kvp.Key;
                    }
                }
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

            var dataSet = LoadWorkbookDataSet(filePath);

            if (dataSet.Tables.Count == 0)
            {
                throw new InvalidDataException("The FCS backlog workbook does not contain any worksheets.");
            }

            var worksheet = dataSet.Tables[0];

            var (currentFiscalYearName, lastUpdateDate) = ParseFcsMetadata(worksheet);
            var nextFiscalYearName = IncrementFiscalYearName(currentFiscalYearName);

            var (headerMap, headerRowIndex) = BuildFcsHeaderMap(worksheet);

            if (headerMap.Count == 0)
            {
                throw new InvalidDataException("Unable to locate the header row in the FCS backlog worksheet. Ensure the first sheet is selected and filters are cleared before importing.");
            }

            var engagementIdIndex = GetRequiredColumnIndex(headerMap, FcsEngagementIdHeaders, "Engagement ID");
            var currentFiscalYearBacklogIndex = GetRequiredColumnIndex(headerMap, FcsCurrentFiscalYearBacklogHeaders, "FYTG Backlog");
            var futureFiscalYearIndexes = ResolveFutureFiscalYearColumns(headerMap, nextFiscalYearName);
            if (futureFiscalYearIndexes.Count == 0)
            {
                throw new InvalidDataException($"The FCS backlog worksheet is missing the Future FY Backlog columns for fiscal year {nextFiscalYearName}.");
            }

            var dataStartRowIndex = Math.Max(headerRowIndex + 1, FcsDataStartRowIndex);

            var parsedRows = new List<FcsBacklogRow>();
            for (var rowIndex = dataStartRowIndex; rowIndex < worksheet.Rows.Count; rowIndex++)
            {
                var row = worksheet.Rows[rowIndex];
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var engagementIdRaw = Convert.ToString(row[engagementIdIndex], CultureInfo.InvariantCulture);
                var engagementId = NormalizeWhitespace(engagementIdRaw);
                if (string.IsNullOrEmpty(engagementId))
                {
                    continue;
                }

                var currentBacklog = ParseDecimal(row[currentFiscalYearBacklogIndex], 2) ?? 0m;

                decimal futureBacklog = 0m;
                foreach (var index in futureFiscalYearIndexes)
                {
                    futureBacklog += ParseDecimal(row[index], 2) ?? 0m;
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

            await using var context = await _contextFactory.CreateDbContextAsync();

            var fiscalYears = await context.FiscalYears
                .Where(fy => fy.Name == currentFiscalYearName || fy.Name == nextFiscalYearName)
                .ToListAsync();

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
                .ToListAsync();

            var engagementLookup = engagements.ToDictionary(e => e.EngagementId, StringComparer.OrdinalIgnoreCase);

            var manualOnlyDetails = new List<string>();
            var missingEngagementDetails = new List<string>();
            var lockedFiscalYearDetails = new List<string>();
            var touchedEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                if (engagement.Source == EngagementSource.S4Project)
                {
                    _logger.LogInformation(
                        "Skipping FCS backlog row {RowNumber} for engagement {EngagementId} from file {FilePath} because it is manual-only (source: {Source}).",
                        row.RowNumber,
                        engagement.EngagementId,
                        filePath,
                        engagement.Source);
                    manualOnlyDetails.Add($"{engagement.EngagementId} (row {row.RowNumber})");
                    continue;
                }

                var toGoCurrent = RoundMoney(row.CurrentBacklog);
                var toDateCurrent = RoundMoney(engagement.OpeningValue - row.CurrentBacklog - row.FutureBacklog);
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
                await context.SaveChangesAsync();
            }

            var skipReasons = new Dictionary<string, IReadOnlyCollection<string>>();

            if (manualOnlyDetails.Count > 0)
            {
                skipReasons["ManualOnly"] = manualOnlyDetails;
            }

            if (lockedFiscalYearDetails.Count > 0)
            {
                skipReasons["LockedFiscalYear"] = lockedFiscalYearDetails;
            }

            if (missingEngagementDetails.Count > 0)
            {
                skipReasons["MissingEngagement"] = missingEngagementDetails;
            }

            var notes = new List<string>
            {
                $"Engagements affected: {touchedEngagements.Count}"
            };

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

        public async Task<string> ImportFullManagementDataAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Full Management Data workbook could not be found.", filePath);
            }

            var dataSet = LoadWorkbookDataSet(filePath);
            if (dataSet.Tables.Count == 0)
            {
                throw new InvalidDataException("Expected sheet not found (Engagement Detail/GRC).");
            }

            var worksheet = ResolveWorksheet(dataSet, "Engagement Detail") ??
                            ResolveWorksheet(dataSet, "GRC");
            if (worksheet == null)
            {
                throw new InvalidDataException("Expected sheet not found (Engagement Detail/GRC).");
            }

            if (worksheet.Rows.Count <= FullManagementHeaderRowIndex)
            {
                throw new InvalidDataException("The Full Management Data worksheet is missing the required header row.");
            }

            var headerText = GetCellString(worksheet, 3, 0);
            var header = ParseFullManagementHeader(headerText);

            var engagementColumnIndex = ColumnNameToIndex("A");
            var currentToGoColumnIndex = ColumnNameToIndex("IN");
            var nextToGoColumnIndex = ColumnNameToIndex("IO");
            var openingColumnIndex = ResolveOpeningColumnIndex(worksheet.Columns.Count);

            EnsureColumnExists(worksheet, engagementColumnIndex, "Engagement ID (column A)");
            EnsureColumnExists(worksheet, currentToGoColumnIndex, "FYTG Backlog (column IN)");
            EnsureColumnExists(worksheet, nextToGoColumnIndex, "Future FY Backlog (column IO)");
            EnsureColumnExists(worksheet, openingColumnIndex, "Original Budget (column JN)");

            var parsedRows = new List<FullManagementRevenueRow>();
            var skippedMissingEngagement = 0;
            var skippedInvalidNumbers = 0;

            for (var rowIndex = FullManagementDataStartRowIndex; rowIndex < worksheet.Rows.Count; rowIndex++)
            {
                var row = worksheet.Rows[rowIndex];
                var engagementRaw = NormalizeWhitespace(Convert.ToString(row[engagementColumnIndex], CultureInfo.InvariantCulture));

                if (string.IsNullOrEmpty(engagementRaw))
                {
                    if (IsAllocationRowEmpty(row, openingColumnIndex, currentToGoColumnIndex, nextToGoColumnIndex))
                    {
                        continue;
                    }

                    skippedMissingEngagement++;
                    continue;
                }

                var engagementCode = ExtractEngagementCode(engagementRaw);
                if (engagementCode is null)
                {
                    if (IsAllocationRowEmpty(row, openingColumnIndex, currentToGoColumnIndex, nextToGoColumnIndex))
                    {
                        continue;
                    }

                    skippedInvalidNumbers++;
                    continue;
                }

                var openingValue = ParseMoneyOrDefault(row[openingColumnIndex], ref skippedInvalidNumbers);
                var currentToGoValue = ParseMoneyOrDefault(row[currentToGoColumnIndex], ref skippedInvalidNumbers);
                var nextToGoValue = ParseMoneyOrDefault(row[nextToGoColumnIndex], ref skippedInvalidNumbers);

                var currentToDateValue = RoundMoney(openingValue - currentToGoValue - nextToGoValue);

                parsedRows.Add(new FullManagementRevenueRow(
                    engagementCode,
                    RoundMoney(currentToGoValue),
                    RoundMoney(nextToGoValue),
                    currentToDateValue));
            }

            await using var context = await _contextFactory.CreateDbContextAsync();

            var engagements = await LoadEngagementsAsync(context, parsedRows.Select(r => r.EngagementId));
            var currentFiscalYear = await FindFiscalYearByCodeAsync(context, header.CurrentFiscalYear)
                ?? throw new InvalidDataException($"Fiscal year '{header.CurrentFiscalYear}' referenced by the workbook could not be found in the database.");
            var nextFiscalYear = await FindFiscalYearByCodeAsync(context, header.NextFiscalYear)
                ?? throw new InvalidDataException($"Fiscal year '{header.NextFiscalYear}' referenced by the workbook could not be found in the database.");

            var allocationLookup = await LoadExistingRevenueAllocationsAsync(
                context,
                engagements.Values.Select(e => e.Id),
                currentFiscalYear.Id,
                nextFiscalYear.Id);

            var upserts = 0;
            var skippedLockedFiscalYears = 0;
            var isCurrentLocked = IsFiscalYearLocked(currentFiscalYear);
            var isNextLocked = IsFiscalYearLocked(nextFiscalYear);

            foreach (var row in parsedRows)
            {
                if (!engagements.TryGetValue(row.EngagementId, out var engagement))
                {
                    skippedMissingEngagement++;
                    continue;
                }

                if (isCurrentLocked)
                {
                    skippedLockedFiscalYears++;
                }
                else
                {
                    await UpsertEngagementFYAllocationAsync(
                        context,
                        allocationLookup,
                        engagement.Id,
                        currentFiscalYear.Id,
                        row.CurrentFiscalYearToDate,
                        row.CurrentFiscalYearToGo,
                        header.LastUpdateDate);
                    upserts++;
                }

                if (isNextLocked)
                {
                    skippedLockedFiscalYears++;
                    continue;
                }

                await UpsertEngagementFYAllocationAsync(
                    context,
                    allocationLookup,
                    engagement.Id,
                    nextFiscalYear.Id,
                    0m,
                    row.NextFiscalYearToGo,
                    header.LastUpdateDate);
                upserts++;
            }

            await context.SaveChangesAsync();

            var summary = string.Join('\n', new[]
            {
                $"FYc={header.CurrentFiscalYear}, FYn={header.NextFiscalYear}, LastUpdateDate={header.LastUpdateDate:yyyy-MM-dd}",
                $"Upserts={upserts}",
                $"SkippedMissingEngagement={skippedMissingEngagement}",
                $"SkippedLockedFY={skippedLockedFiscalYears}",
                $"SkippedInvalidNumbers={skippedInvalidNumbers}"
            });

            return summary;
        }

        private static FullManagementHeader ParseFullManagementHeader(string headerCell)
        {
            if (!TryExtractFiscalYearCode(headerCell, out var normalized, out var currentFiscalYear))
            {
                var messageValue = string.IsNullOrEmpty(normalized) ? string.Empty : normalized;
                throw new InvalidDataException($"Cell A4 must specify the current fiscal year (e.g., FY26). Detected value: '{messageValue}'.");
            }

            if (!TryExtractLastUpdateDate(normalized, out var lastUpdateDate, out var invalidCandidate, out var hasCandidate))
            {
                if (hasCandidate && !string.IsNullOrWhiteSpace(invalidCandidate))
                {
                    throw new InvalidDataException($"The Full Management Data header contains an unrecognized Last Update date: '{invalidCandidate}'.");
                }

                throw new InvalidDataException("The Full Management Data header must specify the last update date (e.g., 'Last Update : 29 Sep 2025').");
            }

            var nextFiscalYear = IncrementFiscalYearName(currentFiscalYear);

            return new FullManagementHeader(currentFiscalYear, nextFiscalYear, lastUpdateDate);
        }

        private static bool TryExtractLastUpdateDate(string normalized, out DateTime lastUpdateDate, out string? invalidCandidate, out bool hasCandidate)
        {
            invalidCandidate = null;
            hasCandidate = false;

            var dateMatch = LastUpdateDateRegex.Match(normalized);
            if (dateMatch.Success)
            {
                hasCandidate = true;
                var candidate = dateMatch.Groups[1].Value;
                if (TryParseHeaderDate(candidate, out lastUpdateDate))
                {
                    return true;
                }

                invalidCandidate = candidate;
                lastUpdateDate = default;
                return false;
            }

            foreach (var segment in normalized.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separatorIndex = segment.IndexOf(':');
                if (separatorIndex < 0)
                {
                    continue;
                }

                var label = segment[..separatorIndex].Trim();
                if (!IsLastUpdateLabel(label))
                {
                    continue;
                }

                hasCandidate = true;
                var value = segment[(separatorIndex + 1)..].Trim();
                if (TryParseHeaderDate(value, out lastUpdateDate))
                {
                    return true;
                }

                invalidCandidate = value;
                lastUpdateDate = default;
                return false;
            }

            lastUpdateDate = default;
            return false;
        }

        private static bool IsLastUpdateLabel(string label)
        {
            return label.Equals("Last Update", StringComparison.OrdinalIgnoreCase) ||
                   label.Equals("Última Atualização", StringComparison.OrdinalIgnoreCase) ||
                   label.Equals("Ultima Atualizacao", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseHeaderDate(string candidate, out DateTime date)
        {
            if (DateTime.TryParseExact(candidate, "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedExact))
            {
                date = DateTime.SpecifyKind(parsedExact.Date, DateTimeKind.Unspecified);
                return true;
            }

            if (DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedInvariant))
            {
                date = DateTime.SpecifyKind(parsedInvariant.Date, DateTimeKind.Unspecified);
                return true;
            }

            if (DateTime.TryParse(candidate, PtBrCulture, DateTimeStyles.AssumeLocal, out var parsedPtBr))
            {
                date = DateTime.SpecifyKind(parsedPtBr.Date, DateTimeKind.Unspecified);
                return true;
            }

            date = default;
            return false;
        }

        private static int ResolveOpeningColumnIndex(int columnCount)
        {
            var primaryIndex = ColumnNameToIndex("JN");
            if (primaryIndex < columnCount)
            {
                return primaryIndex;
            }

            var fallbackIndex = ColumnNameToIndex("JF");
            if (fallbackIndex < columnCount)
            {
                return fallbackIndex;
            }

            throw new InvalidDataException("The Full Management Data worksheet is missing the Original Budget column (JN).");
        }

        private static string? ExtractEngagementCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var match = EngagementIdRegex.Match(value);
            if (match.Success)
            {
                return match.Value.ToUpperInvariant();
            }

            var trimmed = value.Trim();
            return trimmed.StartsWith("E-", StringComparison.OrdinalIgnoreCase)
                ? trimmed.ToUpperInvariant()
                : null;
        }

        private static decimal ParseMoneyOrDefault(object? value, ref int skippedInvalidNumbers)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0m;
            }

            try
            {
                var parsed = ParseDecimal(value, 2);
                return parsed ?? 0m;
            }
            catch (InvalidDataException)
            {
                skippedInvalidNumbers++;
            }
            catch (Exception)
            {
                skippedInvalidNumbers++;
            }

            return 0m;
        }

        private static bool IsAllocationRowEmpty(DataRow row, params int[] columnIndexes)
        {
            foreach (var columnIndex in columnIndexes)
            {
                if (columnIndex < 0 || columnIndex >= row.Table.Columns.Count)
                {
                    continue;
                }

                var value = row[columnIndex];
                if (value == null || value == DBNull.Value)
                {
                    continue;
                }

                var text = NormalizeWhitespace(Convert.ToString(value, CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(text))
                {
                    return false;
                }
            }

            return true;
        }

        private static async Task<Dictionary<string, Engagement>> LoadEngagementsAsync(
            ApplicationDbContext context,
            IEnumerable<string> engagementCodes)
        {
            var codes = engagementCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codes.Count == 0)
            {
                return new Dictionary<string, Engagement>(StringComparer.OrdinalIgnoreCase);
            }

            var engagements = await context.Engagements
                .Where(e => codes.Contains(e.EngagementId))
                .ToListAsync();

            return engagements.ToDictionary(e => e.EngagementId, StringComparer.OrdinalIgnoreCase);
        }

        private static Task<FiscalYear?> FindFiscalYearByCodeAsync(ApplicationDbContext context, string fiscalYearCode)
        {
            return context.FiscalYears.FirstOrDefaultAsync(fy => fy.Name == fiscalYearCode);
        }

        private static bool IsFiscalYearLocked(FiscalYear fiscalYear) => fiscalYear?.IsLocked ?? false;

        private static async Task<Dictionary<(int EngagementId, int FiscalYearId), EngagementFiscalYearRevenueAllocation>> LoadExistingRevenueAllocationsAsync(
            ApplicationDbContext context,
            IEnumerable<int> engagementIds,
            int currentFiscalYearId,
            int nextFiscalYearId)
        {
            var engagementIdList = engagementIds
                .Distinct()
                .ToList();

            if (engagementIdList.Count == 0)
            {
                return new Dictionary<(int, int), EngagementFiscalYearRevenueAllocation>();
            }

            var fiscalYearIds = new HashSet<int> { currentFiscalYearId, nextFiscalYearId };

            var allocations = await context.EngagementFiscalYearRevenueAllocations
                .Where(a => engagementIdList.Contains(a.EngagementId) && fiscalYearIds.Contains(a.FiscalYearId))
                .ToListAsync();

            return allocations.ToDictionary(a => (a.EngagementId, a.FiscalYearId));
        }

        private static async Task UpsertEngagementFYAllocationAsync(
            ApplicationDbContext context,
            IDictionary<(int EngagementId, int FiscalYearId), EngagementFiscalYearRevenueAllocation> allocationLookup,
            int engagementId,
            int fiscalYearId,
            decimal toDateValue,
            decimal toGoValue,
            DateTime lastUpdateDate)
        {
            var key = (engagementId, fiscalYearId);
            if (allocationLookup.TryGetValue(key, out var allocation))
            {
                allocation.ToDateValue = toDateValue;
                allocation.ToGoValue = toGoValue;
                allocation.LastUpdateDate = lastUpdateDate;
                allocation.UpdatedAt = DateTime.UtcNow;
                return;
            }

            var newAllocation = new EngagementFiscalYearRevenueAllocation
            {
                EngagementId = engagementId,
                FiscalYearId = fiscalYearId,
                ToDateValue = toDateValue,
                ToGoValue = toGoValue,
                LastUpdateDate = lastUpdateDate,
                UpdatedAt = DateTime.UtcNow
            };

            await context.EngagementFiscalYearRevenueAllocations.AddAsync(newAllocation);
            allocationLookup[key] = newAllocation;
        }

        private sealed record FullManagementHeader(string CurrentFiscalYear, string NextFiscalYear, DateTime LastUpdateDate);

        private sealed record FullManagementRevenueRow(
            string EngagementId,
            decimal CurrentFiscalYearToGo,
            decimal NextFiscalYearToGo,
            decimal CurrentFiscalYearToDate);

        private static (string FiscalYearName, DateTime? LastUpdateDate) ParseFcsMetadata(DataTable worksheet)
        {
            var rawValue = GetCellString(worksheet, 3, 0);
            string normalized;
            string fiscalYearName;

            if (!TryExtractFiscalYearCode(rawValue, out normalized, out fiscalYearName))
            {
                for (var rowIndex = 0; rowIndex < Math.Min(worksheet.Rows.Count, FcsHeaderSearchLimit); rowIndex++)
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

        private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed record FcsBacklogRow(string EngagementId, decimal CurrentBacklog, decimal FutureBacklog, int RowNumber);

        public async Task<string> ImportActualsAsync(string filePath, int closingPeriodId)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("ETC-P workbook could not be found.", filePath);
            }

            await using var context = await _contextFactory.CreateDbContextAsync();

            var closingPeriod = await context.ClosingPeriods
                .Include(cp => cp.FiscalYear)
                .FirstOrDefaultAsync(cp => cp.Id == closingPeriodId);
            if (closingPeriod == null)
            {
                return "Selected closing period could not be found. Please refresh and try again.";
            }

            if (closingPeriod.FiscalYear?.IsLocked ?? false)
            {
                var fiscalYearName = string.IsNullOrWhiteSpace(closingPeriod.FiscalYear.Name)
                    ? $"Id={closingPeriod.FiscalYear.Id}"
                    : closingPeriod.FiscalYear.Name;

                return $"Closing period '{closingPeriod.Name}' belongs to locked fiscal year '{fiscalYearName}'. Unlock it before running the ETC-P import.";
            }

            var customerCache = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
            var engagementCache = new Dictionary<string, Engagement>(StringComparer.OrdinalIgnoreCase);
            var processedEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rowErrors = new List<string>();

            var customersCreated = 0;
            var engagementsCreated = 0;
            var engagementsUpdated = 0;
            var rowsProcessed = 0;
            var manualOnlyDetails = new List<string>();

            var dataSet = LoadWorkbookDataSet(filePath);

            var etcpTable = ResolveEtcpWorksheet(dataSet);
            if (etcpTable == null)
            {
                return "The ETC-P workbook does not contain the expected worksheet.";
            }

            const int headerRowIndex = 4; // Row 5 in Excel (1-based)
            if (etcpTable.Rows.Count <= headerRowIndex)
            {
                return $"The ETC-P worksheet does not contain any data rows for closing period '{closingPeriod.Name}'.";
            }

            EnsureColumnExists(etcpTable, 2, "Client Name (ID)");
            EnsureColumnExists(etcpTable, 3, "Engagement Name (ID) Currency");
            EnsureColumnExists(etcpTable, 4, "Engagement Status");
            EnsureColumnExists(etcpTable, 8, "Charged Hours Bud");
            EnsureColumnExists(etcpTable, 9, "Charged Hours ETC-P");
            EnsureColumnExists(etcpTable, 11, "TER Bud");
            EnsureColumnExists(etcpTable, 12, "TER ETC-P");
            EnsureColumnExists(etcpTable, 14, "Margin % Bud");
            EnsureColumnExists(etcpTable, 15, "Margin % ETC-P");
            EnsureColumnExists(etcpTable, 17, "Expenses Bud");
            EnsureColumnExists(etcpTable, 18, "Expenses ETC-P");
            EnsureColumnExists(etcpTable, 20, "ETC Age Days");

            var etcpAsOfDate = ExtractEtcAsOfDate(etcpTable);

            var parsedRows = new List<EtcpImportRow>();
            for (var rowIndex = headerRowIndex + 1; rowIndex < etcpTable.Rows.Count; rowIndex++)
            {
                var row = etcpTable.Rows[rowIndex];
                var rowNumber = rowIndex + 1; // Excel is 1-based

                try
                {
                    if (IsRowEmpty(row))
                    {
                        continue;
                    }

                    var parsedRow = ParseEtcpRow(row, rowNumber);
                    if (parsedRow == null)
                    {
                        continue;
                    }

                    parsedRows.Add(parsedRow);
                }
                catch (Exception ex)
                {
                    rowErrors.Add($"Row {rowNumber}: {ex.Message}");
                    _logger.LogError(ex, "Failed to import ETC-P row {RowNumber} from file {FilePath}", rowNumber, filePath);
                }
            }

            var pendingCustomerCodes = parsedRows.Count > 0
                ? await PrefetchCustomersAsync(context, customerCache, parsedRows.Select(r => r.CustomerCode))
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var pendingEngagementIds = parsedRows.Count > 0
                ? await PrefetchEngagementsAsync(context, engagementCache, parsedRows.Select(r => r.EngagementId))
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var parsedRow in parsedRows)
            {
                var rowNumber = parsedRow.RowNumber;

                try
                {
                    var (customer, customerCreated) = await GetOrCreateCustomerAsync(
                        context,
                        customerCache,
                        parsedRow,
                        pendingCustomerCodes);
                    if (customerCreated)
                    {
                        customersCreated++;
                    }

                    var (engagement, engagementCreated) = await GetOrCreateEngagementAsync(
                        context,
                        engagementCache,
                        parsedRow,
                        pendingEngagementIds);
                    if (engagementCreated)
                    {
                        engagementsCreated++;
                        processedEngagements.Add(engagement.EngagementId);
                    }
                    else if (engagement.Source == EngagementSource.S4Project)
                    {
                        manualOnlyDetails.Add($"{engagement.EngagementId} (row {rowNumber})");
                        _logger.LogInformation(
                            "Skipping ETC-P row {RowNumber} for engagement {EngagementId} from file {FilePath} because it is manual-only (source: {Source}).",
                            rowNumber,
                            engagement.EngagementId,
                            filePath,
                            engagement.Source);
                        continue;
                    }
                    else if (processedEngagements.Add(engagement.EngagementId))
                    {
                        engagementsUpdated++;
                    }

                    UpdateCustomer(customer, parsedRow);

                    UpdateEngagement(engagement, customer, parsedRow, closingPeriod, etcpAsOfDate);

                    UpsertFinancialEvolution(
                        context,
                        engagement,
                        FinancialEvolutionInitialPeriodId,
                        parsedRow.BudgetHours,
                        parsedRow.BudgetValue,
                        parsedRow.MarginBudget,
                        parsedRow.BudgetExpenses);
                    UpsertFinancialEvolution(
                        context,
                        engagement,
                        closingPeriod.Name,
                        parsedRow.EstimatedToCompleteHours,
                        parsedRow.EtcpValue,
                        parsedRow.MarginEtcp,
                        parsedRow.EtcpExpenses);

                    rowsProcessed++;
                }
                catch (Exception ex)
                {
                    rowErrors.Add($"Row {rowNumber}: {ex.Message}");
                    _logger.LogError(ex, "Failed to import ETC-P row {RowNumber} from file {FilePath}", rowNumber, filePath);
                }
            }

            await context.SaveChangesAsync();

            var skipReasons = new Dictionary<string, IReadOnlyCollection<string>>();

            if (manualOnlyDetails.Count > 0)
            {
                skipReasons["ManualOnly"] = manualOnlyDetails;
            }

            if (rowErrors.Count > 0)
            {
                skipReasons["RowError"] = rowErrors;
            }

            var notes = new List<string>
            {
                $"Customers created: {customersCreated}",
                $"Engagements created: {engagementsCreated}",
                $"Engagements updated: {engagementsUpdated}"
            };

            if (rowsProcessed == 0)
            {
                notes.Insert(0, $"No ETC-P rows were imported for closing period '{closingPeriod.Name}'.");
            }

            if (manualOnlyDetails.Count > 0)
            {
                notes.Add($"Manual-only rows skipped: {manualOnlyDetails.Count}");
            }

            if (rowErrors.Count > 0)
            {
                notes.Add($"Rows with issues: {rowErrors.Count} (see logs for details)");
            }

            return ImportSummaryFormatter.Build(
                $"ETC-P import ({closingPeriod.Name})",
                customersCreated + engagementsCreated,
                engagementsUpdated,
                skipReasons,
                notes,
                rowsProcessed);
        }

        private static DataTable? ResolveEtcpWorksheet(DataSet dataSet)
        {
            foreach (DataTable table in dataSet.Tables)
            {
                if (table.Rows.Count <= 4 || table.Columns.Count <= 3)
                {
                    continue;
                }

                var headerRow = table.Rows[4];
                var clientHeader = NormalizeWhitespace(Convert.ToString(headerRow[2], CultureInfo.InvariantCulture));
                var engagementHeader = NormalizeWhitespace(Convert.ToString(headerRow[3], CultureInfo.InvariantCulture));

                if (clientHeader.Contains("client", StringComparison.OrdinalIgnoreCase) &&
                    engagementHeader.Contains("engagement", StringComparison.OrdinalIgnoreCase))
                {
                    return table;
                }
            }

            return dataSet.Tables.Count > 0 ? dataSet.Tables[0] : null;
        }

        private static void EnsureColumnExists(DataTable table, int columnIndex, string friendlyName)
        {
            if (columnIndex < table.Columns.Count)
            {
                return;
            }

            var columnName = ColumnIndexToName(columnIndex);
            throw new InvalidDataException($"The ETC-P worksheet is missing expected column '{friendlyName}' at position {columnName}.");
        }

        private DateTime? ExtractEtcAsOfDate(DataTable etcpTable)
        {
            const int rowIndex = 2;
            const int columnIndex = 0;

            var rawValue = GetCellString(etcpTable, rowIndex, columnIndex);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var normalized = NormalizeWhitespace(rawValue);
            var lower = normalized.ToLowerInvariant();
            const string marker = "etd as of:";
            var markerIndex = lower.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                _logger.LogWarning("Unable to locate 'ETD as of' marker in ETC-P cell A3 value '{Value}'.", rawValue);
                return null;
            }

            var remainder = normalized[(markerIndex + marker.Length)..].Trim();
            var gmtIndex = remainder.IndexOf("GMT", StringComparison.OrdinalIgnoreCase);
            if (gmtIndex >= 0)
            {
                remainder = remainder[..gmtIndex].Trim();
            }

            remainder = remainder.Trim(':').Trim();

            if (TryParseAsOfDate(remainder, out var parsed))
            {
                return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Unspecified);
            }

            _logger.LogWarning("Unable to parse ETC-P as-of date from cell A3 value '{Value}'.", rawValue);
            return null;
        }

        private static bool TryParseAsOfDate(string text, out DateTime parsed)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                parsed = default;
                return false;
            }

            var cultures = new[]
            {
                CultureInfo.InvariantCulture,
                CultureInfo.GetCultureInfo("en-US"),
                PtBrCulture
            };

            var stylesWithTimezone = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
            foreach (var culture in cultures)
            {
                if (DateTime.TryParse(text, culture, stylesWithTimezone, out parsed))
                {
                    return true;
                }

                if (DateTime.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                {
                    return true;
                }
            }

            var formats = new[]
            {
                "dd/MM/yyyy",
                "dd/MM/yyyy HH:mm",
                "dd/MM/yyyy HH:mm:ss",
                "dd-MMM-yyyy",
                "dd-MMM-yyyy HH:mm",
                "dd-MMM-yyyy HH:mm:ss",
                "MM/dd/yyyy",
                "MM/dd/yyyy HH:mm",
                "MM/dd/yyyy HH:mm:ss",
                "yyyy-MM-dd",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd HH:mm:ss"
            };

            foreach (var culture in cultures)
            {
                if (DateTime.TryParseExact(text, formats, culture, stylesWithTimezone, out parsed))
                {
                    return true;
                }

                if (DateTime.TryParseExact(text, formats, culture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                {
                    return true;
                }
            }

            parsed = default;
            return false;
        }

        private static int ColumnNameToIndex(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException("Column name must be provided.", nameof(columnName));
            }

            var normalized = columnName.Trim().ToUpperInvariant();
            var index = 0;

            foreach (var ch in normalized)
            {
                if (ch is < 'A' or > 'Z')
                {
                    throw new ArgumentException($"Invalid column name '{columnName}'.", nameof(columnName));
                }

                index = (index * 26) + (ch - 'A' + 1);
            }

            return index - 1;
        }

        private static string ColumnIndexToName(int columnIndex)
        {
            var dividend = columnIndex + 1;
            var columnName = new StringBuilder();

            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName.Insert(0, Convert.ToChar('A' + modulo));
                dividend = (dividend - modulo) / 26;
            }

            return columnName.ToString();
        }

        private static bool IsRowEmpty(DataRow row)
        {
            foreach (var item in row.ItemArray)
            {
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

        private EtcpImportRow? ParseEtcpRow(DataRow row, int rowNumber)
        {
            var customerCell = NormalizeWhitespace(Convert.ToString(row[2], CultureInfo.InvariantCulture));
            var engagementCell = NormalizeWhitespace(Convert.ToString(row[3], CultureInfo.InvariantCulture));

            if (string.IsNullOrWhiteSpace(customerCell) && string.IsNullOrWhiteSpace(engagementCell))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(customerCell))
            {
                throw new InvalidDataException("Client Name (ID) is required.");
            }

            if (string.IsNullOrWhiteSpace(engagementCell))
            {
                throw new InvalidDataException("Engagement Name (ID) Currency is required.");
            }

            var (customerName, customerCode) = ParseEtcpCustomerCell(customerCell);
            var (engagementDescription, engagementId, currency) = ParseEngagementCell(engagementCell);

            var statusText = NormalizeWhitespace(Convert.ToString(row[4], CultureInfo.InvariantCulture));

            var budgetHours = ParsePtBrNumber(row[8]);
            var estimatedToCompleteHours = ParsePtBrNumber(row[9]);
            var budgetValue = ParsePtBrMoney(row[11]);
            var etcpValue = ParsePtBrMoney(row[12]);
            var marginBudget = ParsePtBrPercent(row[14]);
            var marginEtcp = ParsePtBrPercent(row[15]);
            var budgetExpenses = ParsePtBrMoney(row[17]);
            var etcpExpenses = ParsePtBrMoney(row[18]);
            var ageDays = ParseInt(row[20]);

            return new EtcpImportRow
            {
                RowNumber = rowNumber,
                CustomerName = customerName,
                CustomerCode = customerCode,
                EngagementDescription = engagementDescription,
                EngagementId = engagementId,
                Currency = currency,
                StatusText = statusText,
                BudgetHours = budgetHours,
                EstimatedToCompleteHours = estimatedToCompleteHours,
                BudgetValue = budgetValue,
                EtcpValue = etcpValue,
                MarginBudget = marginBudget,
                MarginEtcp = marginEtcp,
                BudgetExpenses = budgetExpenses,
                EtcpExpenses = etcpExpenses,
                EtcpAgeDays = ageDays
            };
        }

        private static EngagementStatus ParseStatus(string? statusText)
        {
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return EngagementStatus.Active;
            }

            return statusText.Trim().ToLowerInvariant() switch
            {
                "closing" => EngagementStatus.Closed,
                "closed" => EngagementStatus.Closed,
                "inactive" => EngagementStatus.Inactive,
                _ => EngagementStatus.Active
            };
        }

        private static decimal? ParsePtBrNumber(object? value) => ParseDecimal(value, null);

        private static decimal? ParsePtBrMoney(object? value) => ParseDecimal(value, 2);

        private static decimal? ParsePtBrPercent(object? value)
        {
            var parsed = ParseDecimal(value, 4);
            if (!parsed.HasValue)
            {
                return null;
            }

            var normalized = parsed.Value;
            if (Math.Abs(normalized) <= 1m)
            {
                normalized *= 100m;
            }

            return Math.Round(normalized, 4, MidpointRounding.AwayFromZero);
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
                sanitized = sanitized[1..^1];
            }

            sanitized = sanitized.Replace(" ", string.Empty);
            sanitized = sanitized.Trim();

            if (isNegative && sanitized.Length > 0)
            {
                sanitized = "-" + sanitized;
            }

            return sanitized;
        }

        private static int? ParseInt(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            switch (value)
            {
                case int i:
                    return i;
                case long l:
                    return (int)l;
                case short s:
                    return s;
                case decimal dec:
                    return (int)Math.Round(dec, MidpointRounding.AwayFromZero);
                case double dbl:
                    return (int)Math.Round(dbl, MidpointRounding.AwayFromZero);
                case float flt:
                    return (int)Math.Round(flt, MidpointRounding.AwayFromZero);
                case string str:
                    var trimmed = NormalizeWhitespace(str);
                    if (trimmed.Length == 0)
                    {
                        return null;
                    }

                    if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var invariantParsed))
                    {
                        return invariantParsed;
                    }

                    if (int.TryParse(trimmed, NumberStyles.Integer, PtBrCulture, out var ptBrParsed))
                    {
                        return ptBrParsed;
                    }

                    throw new InvalidDataException($"Unable to parse integer value '{str}'.");
                default:
                    try
                    {
                        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Unable to parse integer value '{value}'.", ex);
                    }
            }
        }

        private static (string EngagementDisplayName, string EngagementCode, string Currency) ParseEngagementCell(string value)
        {
            var normalized = NormalizeWhitespace(value);
            var match = Regex.Match(normalized, @"\((E-[^)]+)\)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                throw new InvalidDataException($"Engagement code could not be found in '{value}'.");
            }

            var engagementCode = NormalizeWhitespace(match.Groups[1].Value).ToUpperInvariant();
            var displayName = NormalizeWhitespace(normalized[..match.Index]);

            var remainder = NormalizeWhitespace(normalized[(match.Index + match.Length)..]);
            var currency = string.Empty;
            if (!string.IsNullOrEmpty(remainder))
            {
                var tokens = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0)
                {
                    currency = tokens[^1];
                }
            }

            if (string.IsNullOrEmpty(currency))
            {
                throw new InvalidDataException($"Currency could not be determined from '{value}'.");
            }

            return (displayName, engagementCode, currency);
        }

        private static (string CustomerName, string CustomerCode) ParseEtcpCustomerCell(string value)
        {
            var normalized = NormalizeWhitespace(value);
            var match = Regex.Match(normalized, @"\(([^)]+)\)\s*$");
            if (!match.Success)
            {
                throw new InvalidDataException($"Client identifier could not be parsed from '{value}'.");
            }

            var digits = DigitsRegex.Match(match.Groups[1].Value);
            if (!digits.Success)
            {
                throw new InvalidDataException($"Client identifier must contain digits in '{value}'.");
            }

            var name = NormalizeWhitespace(normalized[..match.Index]);
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException($"Client name is missing in '{value}'.");
            }

            return (name, digits.Value);
        }

        private static async Task<HashSet<string>> PrefetchCustomersAsync(
            ApplicationDbContext context,
            IDictionary<string, Customer> cache,
            IEnumerable<string> customerCodes)
        {
            var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var code in customerCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (cache.ContainsKey(code))
                {
                    continue;
                }

                pending.Add(code);
            }

            if (pending.Count == 0)
            {
                return pending;
            }

            var lookup = pending.ToList();
            var existingCustomers = await context.Customers
                .Where(c => lookup.Contains(c.CustomerCode))
                .ToListAsync();

            foreach (var customer in existingCustomers)
            {
                cache[customer.CustomerCode] = customer;
                pending.Remove(customer.CustomerCode);
            }

            return pending;
        }

        private static async Task<HashSet<string>> PrefetchEngagementsAsync(
            ApplicationDbContext context,
            IDictionary<string, Engagement> cache,
            IEnumerable<string> engagementIds)
        {
            var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var engagementId in engagementIds)
            {
                if (string.IsNullOrWhiteSpace(engagementId))
                {
                    continue;
                }

                if (cache.ContainsKey(engagementId))
                {
                    continue;
                }

                pending.Add(engagementId);
            }

            if (pending.Count == 0)
            {
                return pending;
            }

            var lookup = pending.ToList();
            var existingEngagements = await context.Engagements
                .Include(e => e.FinancialEvolutions)
                .Include(e => e.LastClosingPeriod)
                .Where(e => lookup.Contains(e.EngagementId))
                .ToListAsync();

            foreach (var engagement in existingEngagements)
            {
                cache[engagement.EngagementId] = engagement;
                pending.Remove(engagement.EngagementId);
            }

            return pending;
        }

        private static async Task<(Customer customer, bool created)> GetOrCreateCustomerAsync(
            ApplicationDbContext context,
            IDictionary<string, Customer> cache,
            EtcpImportRow row,
            ISet<string> pendingCustomerCodes)
        {
            if (cache.TryGetValue(row.CustomerCode, out var cachedCustomer))
            {
                return (cachedCustomer, false);
            }

            if (pendingCustomerCodes.Contains(row.CustomerCode))
            {
                var newCustomer = new Customer
                {
                    CustomerCode = row.CustomerCode,
                    Name = row.CustomerName
                };

                await context.Customers.AddAsync(newCustomer);
                cache[row.CustomerCode] = newCustomer;
                pendingCustomerCodes.Remove(row.CustomerCode);
                return (newCustomer, true);
            }

            var customer = await context.Customers
                .FirstOrDefaultAsync(c => c.CustomerCode == row.CustomerCode);

            var created = false;
            if (customer == null)
            {
                customer = new Customer
                {
                    CustomerCode = row.CustomerCode,
                    Name = row.CustomerName
                };

                await context.Customers.AddAsync(customer);
                created = true;
            }

            cache[row.CustomerCode] = customer;
            return (customer, created);
        }

        private static void UpdateCustomer(Customer customer, EtcpImportRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.CustomerName) &&
                !string.Equals(customer.Name, row.CustomerName, StringComparison.Ordinal))
            {
                customer.Name = row.CustomerName;
            }
        }

        private static async Task<(Engagement engagement, bool created)> GetOrCreateEngagementAsync(
            ApplicationDbContext context,
            IDictionary<string, Engagement> cache,
            EtcpImportRow row,
            ISet<string> pendingEngagementIds)
        {
            if (cache.TryGetValue(row.EngagementId, out var cachedEngagement))
            {
                return (cachedEngagement, false);
            }

            if (pendingEngagementIds.Contains(row.EngagementId))
            {
                var newEngagement = new Engagement
                {
                    EngagementId = row.EngagementId
                };

                await context.Engagements.AddAsync(newEngagement);
                cache[row.EngagementId] = newEngagement;
                pendingEngagementIds.Remove(row.EngagementId);
                return (newEngagement, true);
            }

            var engagement = await context.Engagements
                .Include(e => e.FinancialEvolutions)
                .Include(e => e.LastClosingPeriod)
                .FirstOrDefaultAsync(e => e.EngagementId == row.EngagementId);

            var created = false;
            if (engagement == null)
            {
                engagement = new Engagement
                {
                    EngagementId = row.EngagementId
                };

                await context.Engagements.AddAsync(engagement);
                created = true;
            }

            cache[row.EngagementId] = engagement;
            return (engagement, created);
        }

        private static void UpdateEngagement(
            Engagement engagement,
            Customer customer,
            EtcpImportRow row,
            ClosingPeriod closingPeriod,
            DateTime? etcpAsOfDate)
        {
            if (!string.IsNullOrWhiteSpace(row.EngagementDescription))
            {
                engagement.Description = row.EngagementDescription;
            }

            engagement.Currency = row.Currency;
            engagement.Customer = customer;
            if (customer.Id > 0)
            {
                engagement.CustomerId = customer.Id;
            }

            if (!string.IsNullOrWhiteSpace(row.StatusText))
            {
                engagement.StatusText = row.StatusText;
            }

            engagement.Status = ParseStatus(row.StatusText);

            if (row.MarginBudget.HasValue)
            {
                engagement.MarginPctBudget = row.MarginBudget;
            }

            if (row.BudgetValue.HasValue)
            {
                engagement.OpeningValue = row.BudgetValue.Value;
            }

            if (row.BudgetExpenses.HasValue)
            {
                engagement.OpeningExpenses = row.BudgetExpenses.Value;
            }

            if (row.BudgetHours.HasValue)
            {
                engagement.InitialHoursBudget = row.BudgetHours.Value;
            }

            engagement.MarginPctEtcp = row.MarginEtcp;
            engagement.EstimatedToCompleteHours = row.EstimatedToCompleteHours ?? 0m;
            engagement.ValueEtcp = row.EtcpValue ?? 0m;
            engagement.ExpensesEtcp = row.EtcpExpenses ?? 0m;
            var lastEtcDate = DetermineLastEtcDate(etcpAsOfDate, row.EtcpAgeDays, closingPeriod);
            engagement.LastEtcDate = lastEtcDate;
            engagement.ProposedNextEtcDate = CalculateProposedNextEtcDate(lastEtcDate);
            engagement.LastClosingPeriodId = closingPeriod.Id;
            engagement.LastClosingPeriod = closingPeriod;
        }

        private static DateTime? DetermineLastEtcDate(DateTime? etcpAsOfDate, int? ageDays, ClosingPeriod closingPeriod)
        {
            var normalizedAge = ageDays.HasValue ? Math.Max(ageDays.Value, 0) : (int?)null;

            if (etcpAsOfDate.HasValue)
            {
                var baseDate = etcpAsOfDate.Value.Date;
                if (normalizedAge.HasValue)
                {
                    return DateTime.SpecifyKind(baseDate.AddDays(-normalizedAge.Value), DateTimeKind.Unspecified);
                }

                return DateTime.SpecifyKind(baseDate, DateTimeKind.Unspecified);
            }

            var periodEndDate = closingPeriod.PeriodEnd.Date;

            if (normalizedAge.HasValue)
            {
                return DateTime.SpecifyKind(periodEndDate.AddDays(-normalizedAge.Value), DateTimeKind.Unspecified);
            }

            return DateTime.SpecifyKind(periodEndDate, DateTimeKind.Unspecified);
        }

        private static DateTime? CalculateProposedNextEtcDate(DateTime? lastEtcDate)
        {
            if (!lastEtcDate.HasValue)
            {
                return null;
            }

            var proposal = lastEtcDate.Value.Date.AddMonths(1);
            return DateTime.SpecifyKind(proposal, DateTimeKind.Unspecified);
        }

        private static void UpsertFinancialEvolution(
            ApplicationDbContext context,
            Engagement engagement,
            string closingPeriodId,
            decimal? hours,
            decimal? value,
            decimal? margin,
            decimal? expenses)
        {
            var evolution = engagement.FinancialEvolutions
                .FirstOrDefault(fe => string.Equals(fe.ClosingPeriodId, closingPeriodId, StringComparison.OrdinalIgnoreCase));

            if (evolution == null)
            {
                evolution = new FinancialEvolution
                {
                    ClosingPeriodId = closingPeriodId,
                    Engagement = engagement
                };

                engagement.FinancialEvolutions.Add(evolution);
                context.FinancialEvolutions.Add(evolution);
            }

            evolution.EngagementId = engagement.Id;
            evolution.Engagement = engagement;
            evolution.HoursData = hours;
            evolution.ValueData = value;
            evolution.MarginData = margin;
            evolution.ExpenseData = expenses;
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