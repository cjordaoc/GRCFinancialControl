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
using GRCFinancialControl.Persistence.Services.Importers.Budget;
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static GRCFinancialControl.Persistence.Services.Importers.WorksheetValueHelper;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Orchestrator service for all data import operations.
    /// Delegates Full Management Data imports to FullManagementDataImporter.
    /// 
    /// Architecture Note: Budget and Allocation imports are currently embedded in this class.
    /// Future work: Extract BudgetImporter and AllocationPlanningImporter as separate classes
    /// inheriting from BaseImporter for better maintainability.
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
        private readonly BudgetImporter _budgetImporter;
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
            IFullManagementDataImporter fullManagementDataImporter,
            BudgetImporter budgetImporter)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ArgumentNullException.ThrowIfNull(fiscalCalendarConsistencyService);
            ArgumentNullException.ThrowIfNull(fullManagementDataImporter);
            ArgumentNullException.ThrowIfNull(budgetImporter);

            _contextFactory = contextFactory;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _fiscalCalendarConsistencyService = fiscalCalendarConsistencyService;
            _fullManagementDataImporter = fullManagementDataImporter;
            _budgetImporter = budgetImporter;
        }

        /// <summary>
        /// Imports a budget workbook. Delegates to BudgetImporter.
        /// Phase 3: Fully extracted to Budget/BudgetImporter.cs
        /// </summary>
        /// <param name="filePath">Path to the budget workbook.</param>
        /// <param name="closingPeriodId">Optional closing period ID. If not provided, uses the latest closing period.</param>
        public async Task<string> ImportBudgetAsync(string filePath, int? closingPeriodId = null)
        {
            _logger.LogInformation("Delegating budget import to BudgetImporter for file: {FilePath}", filePath);
            return await _budgetImporter.ImportAsync(filePath, closingPeriodId).ConfigureAwait(false);
        }

        // ============================================================================
        // OLD BUDGET CODE BELOW THIS LINE - TO BE REMOVED
        // All Budget logic has been extracted to Budget/BudgetImporter.cs
        // The code below is kept temporarily for reference and will be deleted
        // ============================================================================

        public async Task<string> UpdateStaffAllocationsAsync(string filePath, int closingPeriodId)

        // ============================================================================
        // Budget Import Helper Methods - REMOVED
        // All Budget-related logic extracted to Budget/BudgetImporter.cs
        // Removed ~1208 lines of implementation (OLD_ImportBudgetAsync_TODELETE + helpers)
        // ============================================================================

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
                    a => new AllocationKey(DataNormalizationService.NormalizeIdentifier(a.EngagementCode), DataNormalizationService.NormalizeIdentifier(a.RankCode), a.FiscalYearId),
                    a => a,
                    AllocationKeyComparer.Instance);

            var existingHistories = await context.EngagementRankBudgetHistory
                .Where(h => h.ClosingPeriodId == closingPeriod.Id)
                .ToListAsync()
                .ConfigureAwait(false);

            var historyLookup = existingHistories.ToDictionary(
                h => new AllocationKey(DataNormalizationService.NormalizeIdentifier(h.EngagementCode), DataNormalizationService.NormalizeIdentifier(h.RankCode), h.FiscalYearId),
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

            var engagementLookup = engagements.ToDictionary(e => DataNormalizationService.NormalizeIdentifier(e.EngagementId), e => e);

            var budgetLookup = new Dictionary<BudgetKey, EngagementRankBudget>(BudgetKeyComparer.Instance);
            foreach (var engagement in engagements)
            {
                var engagementKey = DataNormalizationService.NormalizeIdentifier(engagement.EngagementId);
                foreach (var budget in engagement.RankBudgets.Where(b => b.FiscalYearId == closingPeriod.FiscalYearId))
                {
                    var rankKey = DataNormalizationService.NormalizeIdentifier(budget.RankName);
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


        public async Task<FullManagementDataImportResult> ImportFullManagementDataAsync(string filePath, int closingPeriodId)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (closingPeriodId <= 0)
            {
                throw new ArgumentException("Valid closing period ID must be provided.", nameof(closingPeriodId));
            }

            return await _fullManagementDataImporter
                .ImportAsync(filePath, closingPeriodId)
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
            return DataNormalizationService.NormalizeIdentifier(value); // Use DataNormalizationService
        }

        private static string NormalizeCode(string? value)
        {
            return DataNormalizationService.NormalizeIdentifier(value); // Use DataNormalizationService
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
                        var normalizedCode = DataNormalizationService.NormalizeIdentifier(engagementCode);
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
                .ToDictionary(e => DataNormalizationService.NormalizeIdentifier(e.EngagementId), e => e, StringComparer.OrdinalIgnoreCase);

            var budgetLookup = new Dictionary<(int EngagementId, int FiscalYearId, string RankCode), EngagementRankBudget>();
            foreach (var engagement in engagements)
            {
                if (engagement.RankBudgets is null)
                {
                    continue;
                }

                foreach (var budget in engagement.RankBudgets.Where(b => fiscalYearIds.Contains(b.FiscalYearId)))
                {
                    var normalizedRank = DataNormalizationService.NormalizeIdentifier(budget.RankName);
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

                    var normalizedEngagement = DataNormalizationService.NormalizeIdentifier(engagementCode);
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

                var normalizedCanonicalRank = DataNormalizationService.NormalizeIdentifier(canonicalRankCode);
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

                var normalizedCanonicalRank = DataNormalizationService.NormalizeIdentifier(canonicalRankCode);
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

                    var normalizedEngagement = DataNormalizationService.NormalizeIdentifier(engagementCode);
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
                var normalizedCode = DataNormalizationService.NormalizeIdentifier(history.EngagementCode);
                var normalizedRank = DataNormalizationService.NormalizeIdentifier(history.RankCode);
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

                var normalizedEngagementCode = DataNormalizationService.NormalizeIdentifier(engagement.EngagementId);
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

                if (!engagementLookup.TryGetValue(historyKey.EngagementCode, out var engagement))
                {
                    continue;
                }

                if (budgetLookup.TryGetValue((engagement.Id, historyKey.FiscalYearId, historyKey.RankCode), out var budget))
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

        // Removed duplicate normalization methods - use DataNormalizationService.NormalizeIdentifier() instead
        // - NormalizeEngagementCode() -> DataNormalizationService.NormalizeIdentifier()
        // - NormalizeRankKey() -> DataNormalizationService.NormalizeIdentifier()
        // - NormalizeAllocationCode() -> DataNormalizationService.NormalizeIdentifier()

        private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed record FcsBacklogRow(string EngagementId, decimal CurrentBacklog, decimal FutureBacklog, int RowNumber);

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

        #region Excel Worksheet Abstraction (shared with Allocation Planning)

        private static WorkbookData LoadWorkbook(string filePath)
        {
            return WorkbookData.Load(filePath);
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
                // No unmanaged resources to release.
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
                var rowCount = reader.RowCount;
                var columnCount = reader.FieldCount;
                var cells = new object?[rowCount][];

                for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    if (!reader.Read())
                    {
                        break;
                    }

                    var row = new object?[columnCount];
                    for (var colIndex = 0; colIndex < columnCount; colIndex++)
                    {
                        row[colIndex] = reader.IsDBNull(colIndex) ? null : reader.GetValue(colIndex);
                    }

                    cells[rowIndex] = row;
                }

                return new WorksheetData(name, cells, columnCount);
            }
        }

        #endregion
    }
}
