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
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services.Infrastructure;
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
            "engagement description",
            "engagement name",
            "project name",
            "engagement",
            "description"
        };

        private static readonly string[] ClosingPeriodHeaders =
        {
            "closing period",
            "period",
            "closing"
        };

        private static readonly string[] CustomerNameHeaders =
        {
            "customer name",
            "client name",
            "client name (id)"
        };

        private static readonly string[] CustomerIdHeaders =
        {
            "customer id",
            "client id",
            "customer code",
            "client code"
        };

        private static readonly string[] OpportunityCurrencyHeaders =
        {
            "opportunity currency",
            "engagement currency"
        };

        private static readonly string[] OriginalBudgetHoursHeaders =
        {
            "original budget hours",
            "budget hours",
            "hours budget",
            "bud hours"
        };

        private static readonly string[] OriginalBudgetTerHeaders =
        {
            "original budget ter",
            "budget value",
            "value bud",
            "revenue bud"
        };

        private static readonly string[] OriginalBudgetMarginPercentHeaders =
        {
            "original budget margin %",
            "margin % bud",
            "budget margin",
            "margin budget"
        };

        private static readonly string[] OriginalBudgetExpensesHeaders =
        {
            "original budget expenses",
            "expenses bud",
            "budget expenses"
        };

        private static readonly string[] ChargedHoursMercuryProjectedHeaders =
        {
            "charged hours mercury projected",
            "etcp hours",
            "hours etc-p",
            "etp hours",
            "etc hours"
        };

        private static readonly string[] TermMercuryProjectedHeaders =
        {
            "ter mercury projected opp currency",
            "etcp value",
            "value etc-p",
            "etp value",
            "etc value"
        };

        private static readonly string[] MarginPercentMercuryProjectedHeaders =
        {
            "margin % mercury projected",
            "margin % etc-p",
            "etcp margin",
            "margin etc"
        };

        private static readonly string[] ExpensesMercuryProjectedHeaders =
        {
            "expenses mercury projected",
            "expenses etc-p",
            "etcp expenses",
            "expenses etc"
        };

        private static readonly string[] StatusHeaders =
        {
            "engagement status",
            "status"
        };

        private static readonly string[] EtcAgeDaysHeaders =
        {
            "etc age days",
            "age days"
        };

        private static readonly string[] UnbilledRevenueDaysHeaders =
        {
            "unbilled revenue days"
        };

        private static readonly string[] LastActiveEtcPDateHeaders =
        {
            "last active etc-p date",
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
            CustomerNameHeaders,
            CustomerIdHeaders,
            OpportunityCurrencyHeaders,
            OriginalBudgetHoursHeaders,
            OriginalBudgetTerHeaders,
            OriginalBudgetMarginPercentHeaders,
            OriginalBudgetExpensesHeaders,
            ChargedHoursMercuryProjectedHeaders,
            TermMercuryProjectedHeaders,
            MarginPercentMercuryProjectedHeaders,
            ExpensesMercuryProjectedHeaders,
            StatusHeaders,
            EtcAgeDaysHeaders,
            UnbilledRevenueDaysHeaders,
            LastActiveEtcPDateHeaders,
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
            if (string.IsNullOrWhiteSpace(metadata.ClosingPeriodName))
            {
                throw new ImportWarningException("Full Management Data workbook is missing the closing period filter. Set the period before exporting and try again.");
            }
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
                        .Include(e => e.Customer)
                        .Where(e => engagementIds.Contains(e.EngagementId))
                        .ToListAsync();

                    var engagementLookup = engagements.ToDictionary(e => e.EngagementId, StringComparer.OrdinalIgnoreCase);

                    var closingPeriods = await context.ClosingPeriods
                        .Include(cp => cp.FiscalYear)
                        .Where(cp => closingPeriodNames.Contains(cp.Name))
                        .ToListAsync();

                    var closingPeriodLookup = closingPeriods.ToDictionary(cp => cp.Name, StringComparer.OrdinalIgnoreCase);

                    var customerNames = new HashSet<string>(
                        parsedRows
                            .Select(r => r.CustomerName)
                            .Where(name => !string.IsNullOrWhiteSpace(name)),
                        StringComparer.OrdinalIgnoreCase);

                    var customerCodes = new HashSet<string>(
                        parsedRows
                            .Select(r => r.CustomerCode)
                            .Where(code => !string.IsNullOrWhiteSpace(code)),
                        StringComparer.OrdinalIgnoreCase);

                    var customers = customerNames.Count > 0 || customerCodes.Count > 0
                        ? await context.Customers
                            .Where(c => customerNames.Contains(c.Name) || customerCodes.Contains(c.CustomerCode))
                            .ToListAsync()
                        : new List<Customer>();

                    var customersByCode = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
                    var customersByName = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);

                    foreach (var customer in customers)
                    {
                        if (!string.IsNullOrWhiteSpace(customer.CustomerCode))
                        {
                            customersByCode[customer.CustomerCode] = customer;
                        }

                        if (!string.IsNullOrWhiteSpace(customer.Name))
                        {
                            customersByName[customer.Name] = customer;
                        }
                    }

                    var updatedEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var manualOnlySkips = new List<string>();
                    var closedEngagementSkips = new List<string>();
                    var lockedFiscalYearSkips = new List<string>();
                    var missingClosingPeriodSkips = new List<string>();
                    var missingEngagementSkips = new List<string>();
                    var errors = new List<string>();
                    var warningMessages = new HashSet<string>(StringComparer.Ordinal);

                    var financialEvolutionUpserts = 0;

                    foreach (var row in parsedRows)
                    {
                        try
                        {
                            var closingPeriodFound = closingPeriodLookup.TryGetValue(row.ClosingPeriodName, out var closingPeriod);

                            if (!closingPeriodFound)
                            {
                                var missingLabel = string.IsNullOrWhiteSpace(row.ClosingPeriodName)
                                    ? "<blank>"
                                    : row.ClosingPeriodName;
                                missingClosingPeriodSkips.Add($"{missingLabel} (row {row.RowNumber})");
                                _logger.LogWarning(
                                    "Closing period '{ClosingPeriod}' not found for Full Management Data row {RowNumber} (Engagement {EngagementId}). Period-specific metrics will be skipped.",
                                    missingLabel,
                                    row.RowNumber,
                                    row.EngagementId);
                            }

                            if (!engagementLookup.TryGetValue(row.EngagementId, out var engagement))
                            {
                                var missingDetail = $"{row.EngagementId} (row {row.RowNumber})";
                                missingEngagementSkips.Add(missingDetail);
                                _logger.LogWarning(
                                    "Engagement not found for Full Management Data row {RowNumber} (Engagement ID {EngagementId}).",
                                    row.RowNumber,
                                    row.EngagementId);
                                continue;
                            }

                            var detail = $"{engagement.EngagementId} (row {row.RowNumber})";

                            if (engagement.Source == EngagementSource.S4Project)
                            {
                                if (!string.IsNullOrWhiteSpace(row.EngagementName))
                                {
                                    engagement.Description = row.EngagementName;
                                }

                                var upsertedCustomer = ResolveCustomer(
                                    context,
                                    row,
                                    customersByCode,
                                    customersByName,
                                    allowCreateOrUpdate: true);

                                if (upsertedCustomer != null)
                                {
                                    engagement.Customer = upsertedCustomer;
                                    engagement.CustomerId = upsertedCustomer.Id;
                                }

                                updatedEngagements.Add(engagement.EngagementId);

                                if (EngagementImportSkipEvaluator.TryCreate(engagement, out var manualMetadata) &&
                                    manualMetadata.ReasonKey == "ManualOnly")
                                {
                                    manualOnlySkips.Add(detail);
                                    warningMessages.Add(manualMetadata.WarningMessage);
                                    _logger.LogWarning(manualMetadata.WarningMessage);
                                }

                                continue;
                            }

                            if (EngagementImportSkipEvaluator.TryCreate(engagement, out var closedMetadata) &&
                                closedMetadata.ReasonKey == "ClosedEngagement")
                            {
                                closedEngagementSkips.Add(detail);
                                warningMessages.Add(closedMetadata.WarningMessage);
                                _logger.LogWarning(closedMetadata.WarningMessage);
                                continue;
                            }

                            if (closingPeriodFound && (closingPeriod!.FiscalYear?.IsLocked ?? false))
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

                            updatedEngagements.Add(engagement.EngagementId);

                            if (!string.IsNullOrWhiteSpace(row.EngagementName))
                            {
                                engagement.Description = row.EngagementName;
                            }

                            var customer = ResolveCustomer(
                                context,
                                row,
                                customersByCode,
                                customersByName,
                                allowCreateOrUpdate: false);

                            if (customer != null)
                            {
                                engagement.Customer = customer;
                                engagement.CustomerId = customer.Id;
                            }

                            TryUpdateCustomerMetadataForPlaceholder(
                                engagement,
                                row,
                                customersByCode,
                                customersByName);

                            if (!string.IsNullOrWhiteSpace(row.OpportunityCurrency))
                            {
                                engagement.Currency = row.OpportunityCurrency;
                            }

                            if (!string.IsNullOrWhiteSpace(row.StatusText))
                            {
                                engagement.StatusText = row.StatusText;
                                engagement.Status = ParseStatus(row.StatusText);
                            }

                            if (row.OriginalBudgetHours.HasValue)
                            {
                                engagement.InitialHoursBudget = row.OriginalBudgetHours.Value;
                            }

                            if (row.OriginalBudgetTer.HasValue)
                            {
                                engagement.OpeningValue = row.OriginalBudgetTer.Value;
                            }

                            if (row.OriginalBudgetMarginPercent.HasValue)
                            {
                                engagement.MarginPctBudget = row.OriginalBudgetMarginPercent;
                            }

                            if (row.OriginalBudgetExpenses.HasValue)
                            {
                                engagement.OpeningExpenses = row.OriginalBudgetExpenses.Value;
                            }

                            if (closingPeriodFound)
                            {
                                if (row.ChargedHoursMercuryProjected.HasValue)
                                {
                                    engagement.EstimatedToCompleteHours = row.ChargedHoursMercuryProjected.Value;
                                }

                                if (row.TERMercuryProjectedOppCurrency.HasValue)
                                {
                                    engagement.ValueEtcp = row.TERMercuryProjectedOppCurrency.Value;
                                }

                                if (row.MarginPercentMercuryProjected.HasValue)
                                {
                                    engagement.MarginPctEtcp = row.MarginPercentMercuryProjected;
                                }

                                if (row.ExpensesMercuryProjected.HasValue)
                                {
                                    engagement.ExpensesEtcp = row.ExpensesMercuryProjected.Value;
                                }

                                if (row.UnbilledRevenueDays.HasValue)
                                {
                                    engagement.UnbilledRevenueDays = row.UnbilledRevenueDays.Value;
                                }

                                var lastEtcDate = ResolveLastEtcDate(row, closingPeriod!);
                                if (lastEtcDate.HasValue)
                                {
                                    engagement.LastEtcDate = lastEtcDate;
                                    engagement.ProposedNextEtcDate = CalculateProposedNextEtcDate(lastEtcDate);
                                }
                                else if (row.NextEtcDate.HasValue)
                                {
                                    engagement.ProposedNextEtcDate = DateTime.SpecifyKind(row.NextEtcDate.Value.Date, DateTimeKind.Unspecified);
                                }

                                engagement.LastClosingPeriodId = closingPeriod!.Id;
                                engagement.LastClosingPeriod = closingPeriod;
                            }
                            else
                            {
                                var lastEtcDate = ResolveLastEtcDate(row, null);
                                if (lastEtcDate.HasValue)
                                {
                                    engagement.LastEtcDate = lastEtcDate;
                                    engagement.ProposedNextEtcDate = CalculateProposedNextEtcDate(lastEtcDate);
                                }
                                else if (row.NextEtcDate.HasValue)
                                {
                                    engagement.ProposedNextEtcDate = DateTime.SpecifyKind(row.NextEtcDate.Value.Date, DateTimeKind.Unspecified);
                                }
                            }

                            financialEvolutionUpserts += UpsertFinancialEvolution(
                                context,
                                engagement,
                                FinancialEvolutionInitialPeriodId,
                                row.OriginalBudgetHours,
                                row.OriginalBudgetTer,
                                row.OriginalBudgetMarginPercent,
                                row.OriginalBudgetExpenses);

                            if (closingPeriodFound)
                            {
                                financialEvolutionUpserts += UpsertFinancialEvolution(
                                    context,
                                    engagement,
                                    closingPeriod!.Name,
                                    row.ChargedHoursMercuryProjected,
                                    row.TERMercuryProjectedOppCurrency,
                                    row.MarginPercentMercuryProjected,
                                    row.ExpensesMercuryProjected);
                            }
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

                    if (closedEngagementSkips.Count > 0)
                    {
                        skipReasons["ClosedEngagement"] = closedEngagementSkips;
                    }

                    if (lockedFiscalYearSkips.Count > 0)
                    {
                        skipReasons["LockedFiscalYear"] = lockedFiscalYearSkips;
                    }

                    if (missingEngagementSkips.Count > 0)
                    {
                        skipReasons["Engagement not found"] = missingEngagementSkips;
                    }

                    if (errors.Count > 0)
                    {
                        skipReasons["Error"] = errors;
                    }

                    var notes = new List<string>
                    {
                        $"Financial evolution entries upserted: {financialEvolutionUpserts}",
                        $"Distinct engagements updated: {updatedEngagements.Count}"
                    };

                    if (!string.IsNullOrWhiteSpace(metadata.ClosingPeriodName))
                    {
                        notes.Add($"Workbook closing period: {metadata.ClosingPeriodName}");
                    }

                    if (metadata.LastUpdateDate.HasValue)
                    {
                        notes.Add($"Workbook last update: {metadata.LastUpdateDate:yyyy-MM-dd}");
                    }

                    if (missingClosingPeriodSkips.Count > 0)
                    {
                        var sample = string.Join(", ", missingClosingPeriodSkips.Take(5));
                        var suffix = missingClosingPeriodSkips.Count > 5 ? ", ..." : string.Empty;
                        notes.Add($"Rows missing closing period: {missingClosingPeriodSkips.Count} (sample: {sample}{suffix}). Period-specific metrics were skipped.");
                    }

                    if (warningMessages.Count > 0)
                    {
                        foreach (var warning in warningMessages)
                        {
                            notes.Insert(0, warning);
                        }
                    }

                    var summary = ImportSummaryFormatter.Build(
                        "Full Management Data import",
                        0,
                        updatedEngagements.Count,
                        skipReasons,
                        notes,
                        parsedRows.Count);
                    _logger.LogInformation(summary);

                    return new FullManagementDataImportResult(
                        summary,
                        parsedRows.Count,
                        0,
                        updatedEngagements.Count,
                        financialEvolutionUpserts,
                        manualOnlySkips.ToArray(),
                        lockedFiscalYearSkips.ToArray(),
                        missingClosingPeriodSkips.ToArray(),
                        missingEngagementSkips.ToArray(),
                        closedEngagementSkips.ToArray(),
                        errors.ToArray(),
                        warningMessages.ToArray());
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
            var customerNameIndex = GetOptionalColumnIndex(headerMap, CustomerNameHeaders);
            var customerIdIndex = GetOptionalColumnIndex(headerMap, CustomerIdHeaders);
            var opportunityCurrencyIndex = GetOptionalColumnIndex(headerMap, OpportunityCurrencyHeaders);
            var originalBudgetHoursIndex = GetOptionalColumnIndex(headerMap, OriginalBudgetHoursHeaders);
            var originalBudgetTerIndex = GetOptionalColumnIndex(headerMap, OriginalBudgetTerHeaders);
            var originalBudgetMarginPercentIndex = GetOptionalColumnIndex(headerMap, OriginalBudgetMarginPercentHeaders);
            var originalBudgetExpensesIndex = GetOptionalColumnIndex(headerMap, OriginalBudgetExpensesHeaders);
            var chargedHoursMercuryProjectedIndex = GetOptionalColumnIndex(headerMap, ChargedHoursMercuryProjectedHeaders);
            var termMercuryProjectedIndex = GetOptionalColumnIndex(headerMap, TermMercuryProjectedHeaders);
            var marginPercentMercuryProjectedIndex = GetOptionalColumnIndex(headerMap, MarginPercentMercuryProjectedHeaders);
            var expensesMercuryProjectedIndex = GetOptionalColumnIndex(headerMap, ExpensesMercuryProjectedHeaders);
            var statusIndex = GetOptionalColumnIndex(headerMap, StatusHeaders);
            var etcAgeDaysIndex = GetOptionalColumnIndex(headerMap, EtcAgeDaysHeaders);
            var unbilledRevenueDaysIndex = GetOptionalColumnIndex(headerMap, UnbilledRevenueDaysHeaders);
            var lastActiveEtcPDateIndex = GetOptionalColumnIndex(headerMap, LastActiveEtcPDateHeaders);
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

                rows.Add(new FullManagementDataRow
                {
                    RowNumber = rowNumber,
                    EngagementId = engagementId,
                    EngagementName = engagementNameIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[engagementNameIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    CustomerName = customerNameIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[customerNameIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    CustomerCode = customerIdIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[customerIdIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    OpportunityCurrency = opportunityCurrencyIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[opportunityCurrencyIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    ClosingPeriodName = closingPeriod ?? string.Empty,
                    OriginalBudgetHours = originalBudgetHoursIndex.HasValue ? ParseDecimal(row[originalBudgetHoursIndex.Value], 2) : null,
                    OriginalBudgetTer = originalBudgetTerIndex.HasValue ? ParseDecimal(row[originalBudgetTerIndex.Value], 2) : null,
                    OriginalBudgetMarginPercent = originalBudgetMarginPercentIndex.HasValue ? ParsePercent(row[originalBudgetMarginPercentIndex.Value]) : null,
                    OriginalBudgetExpenses = originalBudgetExpensesIndex.HasValue ? ParseDecimal(row[originalBudgetExpensesIndex.Value], 2) : null,
                    ChargedHoursMercuryProjected = chargedHoursMercuryProjectedIndex.HasValue ? ParseDecimal(row[chargedHoursMercuryProjectedIndex.Value], 2) : null,
                    TERMercuryProjectedOppCurrency = termMercuryProjectedIndex.HasValue ? ParseDecimal(row[termMercuryProjectedIndex.Value], 2) : null,
                    MarginPercentMercuryProjected = marginPercentMercuryProjectedIndex.HasValue ? ParsePercent(row[marginPercentMercuryProjectedIndex.Value]) : null,
                    ExpensesMercuryProjected = expensesMercuryProjectedIndex.HasValue ? ParseDecimal(row[expensesMercuryProjectedIndex.Value], 2) : null,
                    StatusText = statusIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[statusIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    EtcpAgeDays = etcAgeDaysIndex.HasValue ? ParseInt(row[etcAgeDaysIndex.Value]) : null,
                    UnbilledRevenueDays = unbilledRevenueDaysIndex.HasValue ? ParseInt(row[unbilledRevenueDaysIndex.Value]) : null,
                    LastActiveEtcPDate = lastActiveEtcPDateIndex.HasValue ? ParseDate(row[lastActiveEtcPDateIndex.Value]) : null,
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
                    if (string.Equals(kvp.Value, candidate, StringComparison.Ordinal))
                    {
                        return kvp.Key;
                    }
                }
            }

            foreach (var candidate in candidates)
            {
                foreach (var kvp in headerMap)
                {
                    var header = kvp.Value;
                    if (string.IsNullOrEmpty(header))
                    {
                        continue;
                    }

                    if (!header.Contains(candidate, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (candidate.Contains("engagement", StringComparison.Ordinal) && header.Contains("id", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(candidate, "description", StringComparison.Ordinal) &&
                        (header.Contains("customer", StringComparison.Ordinal) || header.Contains("client", StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    return kvp.Key;
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

        private static DateTime? ResolveLastEtcDate(FullManagementDataRow row, ClosingPeriod? closingPeriod)
        {
            if (row.LastActiveEtcPDate.HasValue)
            {
                return DateTime.SpecifyKind(row.LastActiveEtcPDate.Value.Date, DateTimeKind.Unspecified);
            }

            if (row.EtcpAgeDays.HasValue && closingPeriod != null)
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

        private static Customer? ResolveCustomer(
            ApplicationDbContext context,
            FullManagementDataRow row,
            IDictionary<string, Customer> customersByCode,
            IDictionary<string, Customer> customersByName,
            bool allowCreateOrUpdate)
        {
            Customer? resolved = null;
            string? originalCode = null;
            string? originalName = null;

            if (!string.IsNullOrWhiteSpace(row.CustomerCode) &&
                customersByCode.TryGetValue(row.CustomerCode, out var byCode))
            {
                resolved = byCode;
            }
            else if (!string.IsNullOrWhiteSpace(row.CustomerName) &&
                     customersByName.TryGetValue(row.CustomerName, out var byName))
            {
                resolved = byName;
            }

            if (resolved == null)
            {
                if (!allowCreateOrUpdate)
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(row.CustomerCode) && string.IsNullOrWhiteSpace(row.CustomerName))
                {
                    return null;
                }

                resolved = new Customer
                {
                    CustomerCode = DetermineCustomerCodeForInsert(row, customersByCode),
                    Name = row.CustomerName ?? string.Empty
                };

                context.Customers.Add(resolved);
            }
            else if (allowCreateOrUpdate)
            {
                originalCode = resolved.CustomerCode;
                originalName = resolved.Name;

                if (!string.IsNullOrWhiteSpace(row.CustomerCode) &&
                    !string.Equals(resolved.CustomerCode, row.CustomerCode, StringComparison.OrdinalIgnoreCase))
                {
                    resolved.CustomerCode = row.CustomerCode;
                }

                if (!string.IsNullOrWhiteSpace(row.CustomerName) &&
                    !string.Equals(resolved.Name, row.CustomerName, StringComparison.OrdinalIgnoreCase))
                {
                    resolved.Name = row.CustomerName;
                }
            }

            if (resolved != null)
            {
                if (!string.IsNullOrWhiteSpace(originalCode) &&
                    !string.Equals(originalCode, resolved.CustomerCode, StringComparison.OrdinalIgnoreCase))
                {
                    customersByCode.Remove(originalCode);
                }

                if (!string.IsNullOrWhiteSpace(originalName) &&
                    !string.Equals(originalName, resolved.Name, StringComparison.OrdinalIgnoreCase))
                {
                    customersByName.Remove(originalName);
                }

                if (!string.IsNullOrWhiteSpace(row.CustomerName))
                {
                    customersByName[row.CustomerName] = resolved;
                }

                if (!string.IsNullOrWhiteSpace(resolved.CustomerCode))
                {
                    customersByCode[resolved.CustomerCode] = resolved;
                }
            }

            return resolved;
        }

        private static void TryUpdateCustomerMetadataForPlaceholder(
            Engagement engagement,
            FullManagementDataRow row,
            IDictionary<string, Customer> customersByCode,
            IDictionary<string, Customer> customersByName)
        {
            if (engagement.Customer is null)
            {
                return;
            }

            var customer = engagement.Customer;
            var originalCode = customer.CustomerCode;
            var originalName = customer.Name;
            var updated = false;

            if (!string.IsNullOrWhiteSpace(row.CustomerCode) &&
                (string.IsNullOrWhiteSpace(customer.CustomerCode) ||
                 customer.CustomerCode.StartsWith("AUTO-", StringComparison.OrdinalIgnoreCase)))
            {
                customer.CustomerCode = row.CustomerCode;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(row.CustomerName) &&
                !string.Equals(customer.Name, row.CustomerName, StringComparison.OrdinalIgnoreCase))
            {
                customer.Name = row.CustomerName;
                updated = true;
            }

            if (!updated)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(originalCode) &&
                !string.Equals(originalCode, customer.CustomerCode, StringComparison.OrdinalIgnoreCase))
            {
                customersByCode.Remove(originalCode);
            }

            if (!string.IsNullOrWhiteSpace(originalName) &&
                !string.Equals(originalName, customer.Name, StringComparison.OrdinalIgnoreCase))
            {
                customersByName.Remove(originalName);
            }

            if (!string.IsNullOrWhiteSpace(customer.CustomerCode))
            {
                customersByCode[customer.CustomerCode] = customer;
            }

            if (!string.IsNullOrWhiteSpace(customer.Name))
            {
                customersByName[customer.Name] = customer;
            }
        }

        private static string DetermineCustomerCodeForInsert(
            FullManagementDataRow row,
            IDictionary<string, Customer> customersByCode)
        {
            if (!string.IsNullOrWhiteSpace(row.CustomerCode))
            {
                return row.CustomerCode;
            }

            return GeneratePlaceholderCustomerCode(customersByCode);
        }

        private static string GeneratePlaceholderCustomerCode(IDictionary<string, Customer> customersByCode)
        {
            const int randomSegmentLength = 12;

            while (true)
            {
                var randomSegment = Guid.NewGuid().ToString("N").Substring(0, randomSegmentLength).ToUpperInvariant();
                var placeholder = $"AUTO-{randomSegment}";

                if (!customersByCode.ContainsKey(placeholder))
                {
                    return placeholder;
                }
            }
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
            public string CustomerName { get; init; } = string.Empty;
            public string CustomerCode { get; init; } = string.Empty;
            public string OpportunityCurrency { get; init; } = string.Empty;
            public string ClosingPeriodName { get; init; } = string.Empty;
            public decimal? OriginalBudgetHours { get; init; }
            public decimal? OriginalBudgetTer { get; init; }
            public decimal? OriginalBudgetMarginPercent { get; init; }
            public decimal? OriginalBudgetExpenses { get; init; }
            public decimal? ChargedHoursMercuryProjected { get; init; }
            public decimal? TERMercuryProjectedOppCurrency { get; init; }
            public decimal? MarginPercentMercuryProjected { get; init; }
            public decimal? ExpensesMercuryProjected { get; init; }
            public string StatusText { get; init; } = string.Empty;
            public int? EtcpAgeDays { get; init; }
            public int? UnbilledRevenueDays { get; init; }
            public DateTime? LastActiveEtcPDate { get; init; }
            public DateTime? NextEtcDate { get; init; }
        }
    }
}
