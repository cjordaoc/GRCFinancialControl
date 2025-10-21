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
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static GRCFinancialControl.Persistence.Services.Importers.WorksheetValueHelper;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    public sealed class FullManagementDataImporter : IFullManagementDataImporter
    {
        private const string FinancialEvolutionInitialPeriodId = "INITIAL";
        private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");

        private static readonly string[] EngagementIdHeaders =
        {
            "engagement id",
            "project id",
            "eng id"
        };

        private static readonly string[] EngagementNameHeaders =
        {
            "engagement name",
            "project name",
            "description"
        };

        private static readonly string[] ClosingPeriodHeaders =
        {
            "closing period",
            "period",
            "closing"
        };

        private static readonly string[] BudgetHoursHeaders =
        {
            "budget hours",
            "hours budget",
            "bud hours"
        };

        private static readonly string[] BudgetValueHeaders =
        {
            "budget value",
            "value bud",
            "revenue bud"
        };

        private static readonly string[] BudgetMarginHeaders =
        {
            "margin % bud",
            "budget margin",
            "margin budget"
        };

        private static readonly string[] BudgetExpensesHeaders =
        {
            "expenses bud",
            "budget expenses"
        };

        private static readonly string[] EstimatedToCompleteHoursHeaders =
        {
            "etcp hours",
            "hours etc-p",
            "etp hours",
            "etc hours"
        };

        private static readonly string[] EtcpValueHeaders =
        {
            "etcp value",
            "value etc-p",
            "etp value",
            "etc value"
        };

        private static readonly string[] EtcpMarginHeaders =
        {
            "margin % etc-p",
            "etcp margin",
            "margin etc"
        };

        private static readonly string[] EtcpExpensesHeaders =
        {
            "expenses etc-p",
            "etcp expenses",
            "expenses etc"
        };

        private static readonly string[] StatusHeaders =
        {
            "status",
            "engagement status"
        };

        private static readonly string[] EtcAgeDaysHeaders =
        {
            "etc age days",
            "age days"
        };

        private static readonly string[] LastEtcDateHeaders =
        {
            "last etc date",
            "last etc-p",
            "last etc"
        };

        private static readonly string[] NextEtcDateHeaders =
        {
            "next etc date",
            "proposed next etc",
            "next etc-p"
        };

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<FullManagementDataImporter> _logger;

        public FullManagementDataImporter(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FullManagementDataImporter> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private static readonly IReadOnlyList<string[]> HeaderGroups = new[]
        {
            EngagementIdHeaders,
            EngagementNameHeaders,
            ClosingPeriodHeaders,
            BudgetHoursHeaders,
            BudgetValueHeaders,
            BudgetMarginHeaders,
            BudgetExpensesHeaders,
            EstimatedToCompleteHoursHeaders,
            EtcpValueHeaders,
            EtcpMarginHeaders,
            EtcpExpensesHeaders,
            StatusHeaders,
            EtcAgeDaysHeaders,
            LastEtcDateHeaders,
            NextEtcDateHeaders
        };

        public async Task<FullManagementDataImportResult> ImportAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Full Management Data workbook could not be found.", filePath);
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });

            var worksheet = ResolveWorksheet(dataSet);
            if (worksheet == null)
            {
                throw new InvalidDataException("The Full Management Data workbook does not contain any worksheets.");
            }

            var metadata = ExtractReportMetadata(worksheet);
            var parsedRows = ParseRows(worksheet, metadata.ClosingPeriodName);
            if (parsedRows.Count == 0)
            {
                return new FullManagementDataImportResult(
                    "Full Management Data workbook did not contain any engagement rows to process.",
                    0,
                    0,
                    0,
                    0,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>());
            }

            await using var strategyContext = await _contextFactory.CreateDbContextAsync();
            var strategy = strategyContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                await using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    var engagementIds = parsedRows.Select(r => r.EngagementId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var closingPeriodNames = parsedRows
                        .Select(r => r.ClosingPeriodName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var engagements = await context.Engagements
                        .Include(e => e.FinancialEvolutions)
                        .Where(e => engagementIds.Contains(e.EngagementId))
                        .ToListAsync();

                    var engagementLookup = engagements.ToDictionary(e => e.EngagementId, StringComparer.OrdinalIgnoreCase);

                    var closingPeriods = await context.ClosingPeriods
                        .Include(cp => cp.FiscalYear)
                        .Where(cp => closingPeriodNames.Contains(cp.Name))
                        .ToListAsync();

                    var closingPeriodLookup = closingPeriods.ToDictionary(cp => cp.Name, StringComparer.OrdinalIgnoreCase);

                    var createdEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var updatedEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var manualOnlySkips = new List<string>();
                    var lockedFiscalYearSkips = new List<string>();
                    var missingClosingPeriodSkips = new List<string>();
                    var errors = new List<string>();

                    var financialEvolutionUpserts = 0;

                    foreach (var row in parsedRows)
                    {
                        try
                        {
                            if (!closingPeriodLookup.TryGetValue(row.ClosingPeriodName, out var closingPeriod))
                            {
                                var missingLabel = string.IsNullOrWhiteSpace(row.ClosingPeriodName)
                                    ? "<blank>"
                                    : row.ClosingPeriodName;
                                missingClosingPeriodSkips.Add($"{missingLabel} (row {row.RowNumber})");
                                _logger.LogWarning(
                                    "Skipping row {RowNumber} for engagement {EngagementId} because closing period '{ClosingPeriod}' was not found.",
                                    row.RowNumber,
                                    row.EngagementId,
                                    row.ClosingPeriodName);
                                continue;
                            }

                            if (closingPeriod.FiscalYear?.IsLocked ?? false)
                            {
                                var fiscalYearName = string.IsNullOrWhiteSpace(closingPeriod.FiscalYear.Name)
                                    ? $"Id={closingPeriod.FiscalYear.Id}"
                                    : closingPeriod.FiscalYear.Name;
                                lockedFiscalYearSkips.Add($"{row.EngagementId} ({fiscalYearName}, row {row.RowNumber})");
                                _logger.LogInformation(
                                    "Skipping row {RowNumber} for engagement {EngagementId} because fiscal year '{FiscalYear}' is locked.",
                                    row.RowNumber,
                                    row.EngagementId,
                                    fiscalYearName);
                                continue;
                            }

                            if (!engagementLookup.TryGetValue(row.EngagementId, out var engagement))
                            {
                                engagement = new Engagement
                                {
                                    EngagementId = row.EngagementId,
                                    Description = string.IsNullOrWhiteSpace(row.EngagementName)
                                        ? row.EngagementId
                                        : row.EngagementName,
                                    Status = EngagementStatus.Active,
                                    Source = EngagementSource.GrcProject
                                };

                                await context.Engagements.AddAsync(engagement);
                                engagementLookup[row.EngagementId] = engagement;
                                createdEngagements.Add(row.EngagementId);
                            }
                            else
                            {
                                if (engagement.Source == EngagementSource.S4Project)
                                {
                                    manualOnlySkips.Add($"{engagement.EngagementId} (row {row.RowNumber})");
                                    _logger.LogInformation(
                                        "Skipping row {RowNumber} for engagement {EngagementId} because the engagement is sourced from S/4Project and must be managed manually.",
                                        row.RowNumber,
                                        engagement.EngagementId);
                                    continue;
                                }

                                updatedEngagements.Add(engagement.EngagementId);
                            }

                            if (!string.IsNullOrWhiteSpace(row.EngagementName))
                            {
                                engagement.Description = row.EngagementName;
                            }

                            if (!string.IsNullOrWhiteSpace(row.StatusText))
                            {
                                engagement.StatusText = row.StatusText;
                                engagement.Status = ParseStatus(row.StatusText);
                            }

                            if (row.BudgetHours.HasValue)
                            {
                                engagement.InitialHoursBudget = row.BudgetHours.Value;
                            }

                            if (row.BudgetValue.HasValue)
                            {
                                engagement.OpeningValue = row.BudgetValue.Value;
                            }

                            if (row.BudgetMargin.HasValue)
                            {
                                engagement.MarginPctBudget = row.BudgetMargin;
                            }

                            if (row.BudgetExpenses.HasValue)
                            {
                                engagement.OpeningExpenses = row.BudgetExpenses.Value;
                            }

                            if (row.EstimatedToCompleteHours.HasValue)
                            {
                                engagement.EstimatedToCompleteHours = row.EstimatedToCompleteHours.Value;
                            }

                            if (row.EtcpValue.HasValue)
                            {
                                engagement.ValueEtcp = row.EtcpValue.Value;
                            }

                            if (row.EtcpMargin.HasValue)
                            {
                                engagement.MarginPctEtcp = row.EtcpMargin;
                            }

                            if (row.EtcpExpenses.HasValue)
                            {
                                engagement.ExpensesEtcp = row.EtcpExpenses.Value;
                            }

                            var lastEtcDate = ResolveLastEtcDate(row, closingPeriod);
                            if (lastEtcDate.HasValue)
                            {
                                engagement.LastEtcDate = lastEtcDate;
                                engagement.ProposedNextEtcDate = CalculateProposedNextEtcDate(lastEtcDate);
                            }
                            else if (row.NextEtcDate.HasValue)
                            {
                                engagement.ProposedNextEtcDate = DateTime.SpecifyKind(row.NextEtcDate.Value.Date, DateTimeKind.Unspecified);
                            }

                            engagement.LastClosingPeriodId = closingPeriod.Id;
                            engagement.LastClosingPeriod = closingPeriod;

                            financialEvolutionUpserts += UpsertFinancialEvolution(
                                context,
                                engagement,
                                FinancialEvolutionInitialPeriodId,
                                row.BudgetHours,
                                row.BudgetValue,
                                row.BudgetMargin,
                                row.BudgetExpenses);

                            financialEvolutionUpserts += UpsertFinancialEvolution(
                                context,
                                engagement,
                                closingPeriod.Name,
                                row.EstimatedToCompleteHours,
                                row.EtcpValue,
                                row.EtcpMargin,
                                row.EtcpExpenses);
                        }
                        catch (Exception ex)
                        {
                            var errorMessage = $"Row {row.RowNumber}: {ex.Message}";
                            errors.Add(errorMessage);
                            _logger.LogError(ex, "Error processing Full Management Data row {RowNumber} for engagement {EngagementId}.", row.RowNumber, row.EngagementId);
                        }
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var skipReasons = new Dictionary<string, IReadOnlyCollection<string>>();

                    if (manualOnlySkips.Count > 0)
                    {
                        skipReasons["ManualOnly"] = manualOnlySkips;
                    }

                    if (lockedFiscalYearSkips.Count > 0)
                    {
                        skipReasons["LockedFiscalYear"] = lockedFiscalYearSkips;
                    }

                    if (missingClosingPeriodSkips.Count > 0)
                    {
                        skipReasons["MissingClosingPeriod"] = missingClosingPeriodSkips;
                    }

                    if (errors.Count > 0)
                    {
                        skipReasons["Error"] = errors;
                    }

                    var notes = new List<string>
                    {
                        $"Financial evolution entries upserted: {financialEvolutionUpserts}",
                        $"Distinct engagements touched: {createdEngagements.Count + updatedEngagements.Count}"
                    };

                    if (!string.IsNullOrWhiteSpace(metadata.ClosingPeriodName))
                    {
                        notes.Add($"Workbook closing period: {metadata.ClosingPeriodName}");
                    }

                    if (metadata.LastUpdateDate.HasValue)
                    {
                        notes.Add($"Workbook last update: {metadata.LastUpdateDate:yyyy-MM-dd}");
                    }

                    var summary = ImportSummaryFormatter.Build(
                        "Full Management Data import",
                        createdEngagements.Count,
                        updatedEngagements.Count,
                        skipReasons,
                        notes,
                        parsedRows.Count);
                    _logger.LogInformation(summary);

                    return new FullManagementDataImportResult(
                        summary,
                        parsedRows.Count,
                        createdEngagements.Count,
                        updatedEngagements.Count,
                        financialEvolutionUpserts,
                        manualOnlySkips.ToArray(),
                        lockedFiscalYearSkips.ToArray(),
                        missingClosingPeriodSkips.ToArray(),
                        errors.ToArray());
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        private static DataTable? ResolveWorksheet(DataSet dataSet)
        {
            if (dataSet.Tables.Count == 0)
            {
                return null;
            }

            return dataSet.Tables[0];
        }

        private static List<FullManagementDataRow> ParseRows(DataTable worksheet, string? defaultClosingPeriodName)
        {
            var (headerMap, headerRowIndex) = BuildHeaderMap(worksheet);

            if (headerRowIndex < 0)
            {
                throw new InvalidDataException("Unable to locate the header row in the Full Management Data worksheet. Ensure the first sheet is selected and any filters are cleared before importing.");
            }

            var engagementIdIndex = GetRequiredColumnIndex(headerMap, EngagementIdHeaders, "Engagement ID");
            var closingPeriodIndex = GetOptionalColumnIndex(headerMap, ClosingPeriodHeaders);
            var engagementNameIndex = GetOptionalColumnIndex(headerMap, EngagementNameHeaders);
            var budgetHoursIndex = GetOptionalColumnIndex(headerMap, BudgetHoursHeaders);
            var budgetValueIndex = GetOptionalColumnIndex(headerMap, BudgetValueHeaders);
            var budgetMarginIndex = GetOptionalColumnIndex(headerMap, BudgetMarginHeaders);
            var budgetExpensesIndex = GetOptionalColumnIndex(headerMap, BudgetExpensesHeaders);
            var estimatedToCompleteHoursIndex = GetOptionalColumnIndex(headerMap, EstimatedToCompleteHoursHeaders);
            var etcpValueIndex = GetOptionalColumnIndex(headerMap, EtcpValueHeaders);
            var etcpMarginIndex = GetOptionalColumnIndex(headerMap, EtcpMarginHeaders);
            var etcpExpensesIndex = GetOptionalColumnIndex(headerMap, EtcpExpensesHeaders);
            var statusIndex = GetOptionalColumnIndex(headerMap, StatusHeaders);
            var etcAgeDaysIndex = GetOptionalColumnIndex(headerMap, EtcAgeDaysHeaders);
            var lastEtcDateIndex = GetOptionalColumnIndex(headerMap, LastEtcDateHeaders);
            var nextEtcDateIndex = GetOptionalColumnIndex(headerMap, NextEtcDateHeaders);

            if (!closingPeriodIndex.HasValue && string.IsNullOrWhiteSpace(defaultClosingPeriodName))
            {
                throw new InvalidDataException("The Full Management Data workbook is missing the Closing Period column and cell A4 did not specify a closing period.");
            }

            var rows = new List<FullManagementDataRow>();

            for (var rowIndex = headerRowIndex + 1; rowIndex < worksheet.Rows.Count; rowIndex++)
            {
                var row = worksheet.Rows[rowIndex];
                var rowNumber = rowIndex + 1; // Excel rows are 1-based

                if (IsRowEmpty(row))
                {
                    continue;
                }

                var engagementId = NormalizeWhitespace(Convert.ToString(row[engagementIdIndex], CultureInfo.InvariantCulture));
                if (string.IsNullOrWhiteSpace(engagementId))
                {
                    continue;
                }

                var closingPeriod = closingPeriodIndex.HasValue
                    ? NormalizeWhitespace(Convert.ToString(row[closingPeriodIndex.Value], CultureInfo.InvariantCulture))
                    : defaultClosingPeriodName ?? string.Empty;

                if (string.IsNullOrWhiteSpace(closingPeriod) && !string.IsNullOrWhiteSpace(defaultClosingPeriodName))
                {
                    closingPeriod = defaultClosingPeriodName!;
                }

                if (string.IsNullOrWhiteSpace(closingPeriod))
                {
                    continue;
                }

                rows.Add(new FullManagementDataRow
                {
                    RowNumber = rowNumber,
                    EngagementId = engagementId,
                    EngagementName = engagementNameIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[engagementNameIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    ClosingPeriodName = closingPeriod,
                    BudgetHours = budgetHoursIndex.HasValue ? ParseDecimal(row[budgetHoursIndex.Value], 2) : null,
                    BudgetValue = budgetValueIndex.HasValue ? ParseDecimal(row[budgetValueIndex.Value], 2) : null,
                    BudgetMargin = budgetMarginIndex.HasValue ? ParsePercent(row[budgetMarginIndex.Value]) : null,
                    BudgetExpenses = budgetExpensesIndex.HasValue ? ParseDecimal(row[budgetExpensesIndex.Value], 2) : null,
                    EstimatedToCompleteHours = estimatedToCompleteHoursIndex.HasValue ? ParseDecimal(row[estimatedToCompleteHoursIndex.Value], 2) : null,
                    EtcpValue = etcpValueIndex.HasValue ? ParseDecimal(row[etcpValueIndex.Value], 2) : null,
                    EtcpMargin = etcpMarginIndex.HasValue ? ParsePercent(row[etcpMarginIndex.Value]) : null,
                    EtcpExpenses = etcpExpensesIndex.HasValue ? ParseDecimal(row[etcpExpensesIndex.Value], 2) : null,
                    StatusText = statusIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[statusIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    EtcpAgeDays = etcAgeDaysIndex.HasValue ? ParseInt(row[etcAgeDaysIndex.Value]) : null,
                    LastEtcDate = lastEtcDateIndex.HasValue ? ParseDate(row[lastEtcDateIndex.Value]) : null,
                    NextEtcDate = nextEtcDateIndex.HasValue ? ParseDate(row[nextEtcDateIndex.Value]) : null
                });
            }

            return rows;
        }

        private static (Dictionary<int, string> Map, int HeaderRowIndex) BuildHeaderMap(DataTable table)
        {
            Dictionary<int, string>? bestMap = null;
            var bestIndex = -1;
            var bestScore = -1;

            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                var currentMap = new Dictionary<int, string>();
                var hasContent = false;

                for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
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

                if (!ContainsAnyHeader(currentMap, EngagementIdHeaders))
                {
                    continue;
                }

                var score = CountHeaderMatches(currentMap);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMap = currentMap;
                    bestIndex = rowIndex;
                }
            }

            return bestMap != null
                ? (bestMap, bestIndex)
                : (new Dictionary<int, string>(), -1);
        }

        private static int CountHeaderMatches(Dictionary<int, string> headerMap)
        {
            var score = 0;

            foreach (var group in HeaderGroups)
            {
                if (ContainsAnyHeader(headerMap, group))
                {
                    score++;
                }
            }

            return score;
        }

        private static int GetRequiredColumnIndex(Dictionary<int, string> headerMap, string[] candidates, string friendlyName)
        {
            var index = GetOptionalColumnIndex(headerMap, candidates);
            if (!index.HasValue)
            {
                throw new InvalidDataException($"The Full Management Data worksheet is missing required column '{friendlyName}'. Ensure the first sheet is selected and filters are cleared before importing.");
            }

            return index.Value;
        }

        private static int? GetOptionalColumnIndex(Dictionary<int, string> headerMap, string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                foreach (var kvp in headerMap)
                {
                    if (kvp.Value.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp.Key;
                    }
                }
            }

            return null;
        }

        private static bool ContainsAnyHeader(Dictionary<int, string> headerMap, IEnumerable<string> candidates)
        {
            foreach (var candidate in candidates)
            {
                foreach (var value in headerMap.Values)
                {
                    if (!string.IsNullOrEmpty(value) && value.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsRowEmpty(DataRow row)
        {
            foreach (var item in row.ItemArray)
            {
                if (item != null && item != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(item, CultureInfo.InvariantCulture)))
                {
                    return false;
                }
            }

            return true;
        }

        private static string? TryGetCellString(DataTable worksheet, int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || columnIndex < 0 || rowIndex >= worksheet.Rows.Count || columnIndex >= worksheet.Columns.Count)
            {
                return null;
            }

            return Convert.ToString(worksheet.Rows[rowIndex][columnIndex], CultureInfo.InvariantCulture);
        }

        private static FullManagementReportMetadata ExtractReportMetadata(DataTable worksheet)
        {
            var rawValue = TryGetCellString(worksheet, 3, 0);

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                var searchLimit = Math.Min(worksheet.Rows.Count, 12);
                for (var rowIndex = 0; rowIndex < searchLimit; rowIndex++)
                {
                    var candidate = TryGetCellString(worksheet, rowIndex, 0);
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    var normalizedCandidate = NormalizeWhitespace(candidate);
                    if (normalizedCandidate.Contains("period", StringComparison.OrdinalIgnoreCase) ||
                        normalizedCandidate.Contains("last update", StringComparison.OrdinalIgnoreCase) ||
                        normalizedCandidate.Contains("última atualização", StringComparison.OrdinalIgnoreCase) ||
                        normalizedCandidate.Contains("ultima atualizacao", StringComparison.OrdinalIgnoreCase))
                    {
                        rawValue = candidate;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return new FullManagementReportMetadata(null, null);
            }

            var normalized = NormalizeWhitespace(rawValue);
            string? closingPeriod = null;
            DateTime? lastUpdate = null;

            var segments = normalized.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                closingPeriod = segments[0].Trim();
            }

            foreach (var segment in segments)
            {
                var separatorIndex = segment.IndexOf(':');
                if (separatorIndex < 0)
                {
                    continue;
                }

                var label = segment[..separatorIndex].Trim();
                var value = segment[(separatorIndex + 1)..].Trim();

                if (label.Equals("Last Update", StringComparison.OrdinalIgnoreCase) ||
                    label.Equals("Última Atualização", StringComparison.OrdinalIgnoreCase) ||
                    label.Equals("Ultima Atualizacao", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(value, PtBrCulture, DateTimeStyles.AssumeLocal, out var parsedPtBr))
                    {
                        lastUpdate = DateTime.SpecifyKind(parsedPtBr.Date, DateTimeKind.Unspecified);
                    }
                    else if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedInvariant))
                    {
                        lastUpdate = DateTime.SpecifyKind(parsedInvariant.Date, DateTimeKind.Unspecified);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(closingPeriod))
            {
                closingPeriod = null;
            }

            return new FullManagementReportMetadata(closingPeriod, lastUpdate);
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
                    parsed = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    break;
            }

            if (decimals.HasValue)
            {
                parsed = Math.Round(parsed, decimals.Value, MidpointRounding.AwayFromZero);
            }

            return parsed;
        }

        private static decimal? ParsePercent(object? value)
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
                    return Convert.ToInt32(l);
                case double d:
                    return Convert.ToInt32(Math.Round(d, MidpointRounding.AwayFromZero));
                case decimal dec:
                    return Convert.ToInt32(Math.Round(dec, MidpointRounding.AwayFromZero));
                case string str:
                    var normalized = NormalizeWhitespace(str);
                    if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedString))
                    {
                        return parsedString;
                    }

                    if (double.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsedDouble))
                    {
                        return Convert.ToInt32(Math.Round(parsedDouble, MidpointRounding.AwayFromZero));
                    }

                    return null;
                default:
                    return null;
            }
        }

        private static DateTime? ParseDate(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            switch (value)
            {
                case DateTime dt:
                    return DateTime.SpecifyKind(dt.Date, DateTimeKind.Unspecified);
                case double oa:
                    return DateTime.SpecifyKind(DateTime.FromOADate(oa).Date, DateTimeKind.Unspecified);
                case string str:
                    var normalized = NormalizeWhitespace(str);
                    if (string.IsNullOrWhiteSpace(normalized))
                    {
                        return null;
                    }

                    var cultures = new[]
                    {
                        CultureInfo.InvariantCulture,
                        CultureInfo.GetCultureInfo("en-US"),
                        PtBrCulture
                    };

                    foreach (var culture in cultures)
                    {
                        if (DateTime.TryParse(normalized, culture, DateTimeStyles.AllowWhiteSpaces, out var parsedGeneric))
                        {
                            return DateTime.SpecifyKind(parsedGeneric.Date, DateTimeKind.Unspecified);
                        }
                    }

                    var formats = new[]
                    {
                        "dd/MM/yyyy",
                        "MM/dd/yyyy",
                        "yyyy-MM-dd"
                    };

                    foreach (var culture in cultures)
                    {
                        if (DateTime.TryParseExact(normalized, formats, culture, DateTimeStyles.AllowWhiteSpaces, out var parsedExact))
                        {
                            return DateTime.SpecifyKind(parsedExact.Date, DateTimeKind.Unspecified);
                        }
                    }

                    return null;
                default:
                    return null;
            }
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

        private static EngagementStatus ParseStatus(string? statusText)
        {
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return EngagementStatus.Active;
            }

            var normalized = statusText.Trim().ToLowerInvariant();
            return normalized switch
            {
                "active" => EngagementStatus.Active,
                "in progress" => EngagementStatus.Active,
                "closing" => EngagementStatus.Closed,
                "closed" => EngagementStatus.Closed,
                "inactive" => EngagementStatus.Inactive,
                _ => EngagementStatus.Active
            };
        }

        private static DateTime? ResolveLastEtcDate(FullManagementDataRow row, ClosingPeriod closingPeriod)
        {
            if (row.LastEtcDate.HasValue)
            {
                return DateTime.SpecifyKind(row.LastEtcDate.Value.Date, DateTimeKind.Unspecified);
            }

            if (row.EtcpAgeDays.HasValue)
            {
                var normalizedAge = Math.Max(row.EtcpAgeDays.Value, 0);
                var baseDate = closingPeriod.PeriodEnd.Date;
                return DateTime.SpecifyKind(baseDate.AddDays(-normalizedAge), DateTimeKind.Unspecified);
            }

            return null;
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

        private static int UpsertFinancialEvolution(
            ApplicationDbContext context,
            Engagement engagement,
            string closingPeriodId,
            decimal? hours,
            decimal? value,
            decimal? margin,
            decimal? expenses)
        {
            if (!hours.HasValue && !value.HasValue && !margin.HasValue && !expenses.HasValue)
            {
                return 0;
            }

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
            evolution.HoursData = hours;
            evolution.ValueData = value;
            evolution.MarginData = margin;
            evolution.ExpenseData = expenses;

            return 1;
        }

        private sealed record FullManagementReportMetadata(string? ClosingPeriodName, DateTime? LastUpdateDate);

        private sealed class FullManagementDataRow
        {
            public int RowNumber { get; init; }
            public string EngagementId { get; init; } = string.Empty;
            public string EngagementName { get; init; } = string.Empty;
            public string ClosingPeriodName { get; init; } = string.Empty;
            public decimal? BudgetHours { get; init; }
            public decimal? BudgetValue { get; init; }
            public decimal? BudgetMargin { get; init; }
            public decimal? BudgetExpenses { get; init; }
            public decimal? EstimatedToCompleteHours { get; init; }
            public decimal? EtcpValue { get; init; }
            public decimal? EtcpMargin { get; init; }
            public decimal? EtcpExpenses { get; init; }
            public string StatusText { get; init; } = string.Empty;
            public int? EtcpAgeDays { get; init; }
            public DateTime? LastEtcDate { get; init; }
            public DateTime? NextEtcDate { get; init; }
        }
    }
}
