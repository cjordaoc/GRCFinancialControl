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
    /// <summary>
    /// Imports Full Management Data Excel workbooks and populates engagement financial snapshots.
    /// Inherits from ImportServiceBase for common Excel loading and transaction management.
    /// 
    /// Updates three tables:
    /// - Engagements: Description, Currency, Status, Budget/ETC-P values
    /// - FinancialEvolution: Budget/ETD/FYTD metrics for hours, margin, expenses, revenue
    /// - RevenueAllocations: ToGo/ToDate values for current + future fiscal years
    /// 
    /// Performance: Uses dictionary lookups and applies ConfigureAwait(false) to all async operations.
    /// </summary>
    public sealed class FullManagementDataImporter : ImportServiceBase, IFullManagementDataImporter
    {
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
            "customername",
            "customer-name",
            "client name",
            "clientname",
            "client-name",
            "client name (id)",
            "clientname(id)",
            "client-name(id)",
            "client description",
            "clientdescription",
            "client-description",
            "customer description",
            "customerdescription",
            "customer-description",
            "customer",
            "client"
        };

        private static readonly string[] CustomerIdHeaders =
        {
            "customer id",
            "customerid",
            "customer-id",
            "client id",
            "clientid",
            "client-id",
            "customer code",
            "customercode",
            "customer-code",
            "client code",
            "clientcode",
            "client-code"
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

        private static readonly string[] TerFiscalYearToDateHeaders =
        {
            "ter fytd",
            "ter fytd (r$)",
            "ter fytd (brl)",
            "ter fytd value",
            "ter fytd (local currency)",
            "ter fytd local currency",
            "ter fytd (lc)",
            "ter fytd (opp currency)",
            "ter fytd opp currency",
            "ter fytd (opportunity currency)",
            "ter fytd opportunity currency",
            "ter fytd (moeda da oportunidade)",
            "ter fytd moeda da oportunidade",
            "ter fytd (moeda oportunidade)",
            "ter fytd (moeda local)",
            "ter fytd moeda local",
            "ter fiscal year to date",
            "fytd ter"
        };

        private static readonly string[] ValueDataHeaders =
        {
            "valuedata",
            "value data"
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

        private static readonly string[] EngagementPartnerGuiHeaders =
        {
            "engagement partner gui"
        };

        private static readonly string[] EngagementManagerGuiHeaders =
        {
            "engagement manager gui"
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

        private static readonly string[] CurrentFiscalYearBacklogHeaders =
        {
            "fytg backlog",
            "fiscal year to go backlog",
            "current backlog"
        };

        private static readonly string[] FutureFiscalYearBacklogHeaders =
        {
            "future fy backlog",
            "future fiscal year backlog",
            "future backlog"
        };

        private static readonly string[] ChargedHoursETDHeaders =
        {
            "charged hours etd",
            "etd hours",
            "hours etd"
        };

        private static readonly string[] ChargedHoursFYTDHeaders =
        {
            "charged hours fytd",
            "fytd hours",
            "hours fytd"
        };

        private static readonly string[] MarginPercentETDHeaders =
        {
            "margin % etd",
            "etd margin %",
            "margin etd"
        };

        private static readonly string[] MarginPercentFYTDHeaders =
        {
            "margin % fytd",
            "fytd margin %",
            "margin fytd"
        };

        private static readonly string[] ExpensesETDHeaders =
        {
            "expenses etd",
            "etd expenses"
        };

        private static readonly string[] ExpensesFYTDHeaders =
        {
            "expenses fytd",
            "fytd expenses"
        };

        public FullManagementDataImporter(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FullManagementDataImporter> logger)
            : base(contextFactory, logger)
        {
        }

        private static readonly IReadOnlyList<string[]> HeaderGroups = new[]
        {
            EngagementIdHeaders,
            EngagementNameHeaders,
            // Note: ClosingPeriodHeaders removed - closing period now comes from UI selection
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
            NextEtcDateHeaders,
            CurrentFiscalYearBacklogHeaders,
            FutureFiscalYearBacklogHeaders
        };

        public async Task<FullManagementDataImportResult> ImportAsync(string filePath, int closingPeriodId)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (closingPeriodId <= 0)
            {
                throw new ArgumentException("Valid closing period ID must be provided.", nameof(closingPeriodId));
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

            // Parse rows without requiring closing period from Excel
            var parsedRows = ParseRows(worksheet, null);
            if (parsedRows.Count == 0)
            {
                return new FullManagementDataImportResult(
                    "Full Management Data workbook did not contain any engagement rows to process.",
                    0,
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

            await using var strategyContext = await ContextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var strategy = strategyContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var context = await ContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                await using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);

                try
                {
                    var engagementIds = parsedRows.Select(r => r.EngagementId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                    var engagements = await context.Engagements
                        .Include(e => e.FinancialEvolutions)
                        .Include(e => e.Customer)
                        .Include(e => e.RevenueAllocations)
                        .Include(e => e.ManagerAssignments)
                        .Include(e => e.EngagementPapds)
                        .Where(e => engagementIds.Contains(e.EngagementId))
                        .ToListAsync()
                        .ConfigureAwait(false);

                    var engagementLookup = engagements.ToDictionary(e => e.EngagementId, StringComparer.OrdinalIgnoreCase);

                    // Fetch the user-selected closing period
                    var closingPeriod = await context.ClosingPeriods
                        .Include(cp => cp.FiscalYear)
                        .FirstOrDefaultAsync(cp => cp.Id == closingPeriodId)
                        .ConfigureAwait(false);

                    if (closingPeriod == null)
                    {
                        throw new InvalidDataException($"Closing period with ID {closingPeriodId} not found in database.");
                    }

                    if (closingPeriod.FiscalYear?.IsLocked ?? false)
                    {
                        throw new InvalidOperationException($"Cannot import data for closing period '{closingPeriod.Name}' because its fiscal year '{closingPeriod.FiscalYear.Name}' is locked.");
                    }

                    var closingPeriodRemovalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        closingPeriod.Id.ToString(CultureInfo.InvariantCulture).Trim()
                    };

                    if (!string.IsNullOrWhiteSpace(closingPeriod.Name))
                    {
                        closingPeriodRemovalKeys.Add(closingPeriod.Name.Trim());
                    }

                    var closingPeriodIdLower = closingPeriod.Id
                        .ToString(CultureInfo.InvariantCulture)
                        .Trim()
                        .ToLowerInvariant();
                    var closingPeriodNameLower = string.IsNullOrWhiteSpace(closingPeriod.Name)
                        ? null
                        : closingPeriod.Name.Trim().ToLowerInvariant();

                    var evolutionsToRemove = await context.FinancialEvolutions
                        .Where(fe => fe.ClosingPeriodId != null)
                        .Where(fe => fe.ClosingPeriodId!.Trim().ToLower() == closingPeriodIdLower ||
                                     (closingPeriodNameLower != null &&
                                      fe.ClosingPeriodId!.Trim().ToLower() == closingPeriodNameLower))
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (evolutionsToRemove.Count > 0)
                    {
                        context.FinancialEvolutions.RemoveRange(evolutionsToRemove);
                    }

                    if (closingPeriodRemovalKeys.Count > 0)
                    {
                        foreach (var engagement in engagements)
                        {
                            var snapshotsToDetach = engagement.FinancialEvolutions
                                .Where(fe => !string.IsNullOrWhiteSpace(fe.ClosingPeriodId) &&
                                    closingPeriodRemovalKeys.Contains(fe.ClosingPeriodId!.Trim()))
                                .ToList();

                            if (snapshotsToDetach.Count == 0)
                            {
                                continue;
                            }

                            foreach (var snapshot in snapshotsToDetach)
                            {
                                engagement.FinancialEvolutions.Remove(snapshot);
                            }
                        }
                    }

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
                            .ConfigureAwait(false)
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

                    var managers = await context.Managers
                        .AsNoTracking()
                        .Where(m => m.EngagementManagerGui != null)
                        .ToListAsync()
                        .ConfigureAwait(false);
                    var managersByGui = managers.ToDictionary(m => m.EngagementManagerGui!, StringComparer.OrdinalIgnoreCase);

                    var papds = await context.Papds
                        .AsNoTracking()
                        .Where(p => p.EngagementPapdGui != null)
                        .ToListAsync()
                        .ConfigureAwait(false);
                    var papdsByGui = papds.ToDictionary(p => p.EngagementPapdGui!, StringComparer.OrdinalIgnoreCase);

                    var updatedEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var manualOnlySkips = new List<string>();
                    var closedEngagementSkips = new List<string>();
                    var missingEngagementSkips = new List<string>();
                    var errors = new List<string>();
                    var warningMessages = new HashSet<string>(StringComparer.Ordinal);

                    var missingManagerGuis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var missingPapdGuis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    var financialEvolutionUpserts = 0;
                    var s4MetadataRefreshes = 0;
                    var managerAssignmentChanges = 0;
                    var papdAssignmentChanges = 0;

                    foreach (var row in parsedRows)
                    {
                        try
                        {

                            if (!engagementLookup.TryGetValue(row.EngagementId, out var engagement))
                            {
                                var missingDetail = $"{row.EngagementId} (row {row.RowNumber})";
                                missingEngagementSkips.Add(missingDetail);
                                Logger.LogWarning(
                                    "Engagement not found for Full Management Data row {RowNumber} (Engagement ID {EngagementId}).",
                                    row.RowNumber,
                                    row.EngagementId);
                                continue;
                            }

                            var detail = $"{engagement.EngagementId} (row {row.RowNumber})";

                            if (engagement.Source == EngagementSource.S4Project)
                            {
                                var metadataChanged = false;

                                if (!string.IsNullOrWhiteSpace(row.EngagementName))
                                {
                                    if (!string.Equals(engagement.Description, row.EngagementName, StringComparison.Ordinal))
                                    {
                                        engagement.Description = row.EngagementName;
                                        metadataChanged = true;
                                    }
                                }

                                var upsertedCustomer = ResolveCustomer(
                                    context,
                                    row,
                                    customersByCode,
                                    customersByName,
                                    allowCreateOrUpdate: true);

                                if (upsertedCustomer != null)
                                {
                                    if (!ReferenceEquals(engagement.Customer, upsertedCustomer) || engagement.CustomerId != upsertedCustomer.Id)
                                    {
                                        engagement.Customer = upsertedCustomer;
                                        engagement.CustomerId = upsertedCustomer.Id;
                                        metadataChanged = true;
                                    }
                                }

                                if (TryUpdateCustomerMetadataForPlaceholder(
                                    engagement,
                                    row,
                                    customersByCode,
                                    customersByName))
                                {
                                    metadataChanged = true;
                                }

                                if (!string.IsNullOrWhiteSpace(row.OpportunityCurrency))
                                {
                                    if (!string.Equals(engagement.Currency, row.OpportunityCurrency, StringComparison.OrdinalIgnoreCase))
                                    {
                                        engagement.Currency = row.OpportunityCurrency;
                                        metadataChanged = true;
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(row.StatusText))
                                {
                                    if (!string.Equals(engagement.StatusText, row.StatusText, StringComparison.Ordinal))
                                    {
                                        engagement.StatusText = row.StatusText;
                                        engagement.Status = ParseStatus(row.StatusText);
                                        metadataChanged = true;
                                    }
                                }

                                updatedEngagements.Add(engagement.EngagementId);
                                if (metadataChanged)
                                {
                                    s4MetadataRefreshes++;
                                }

                                if (EngagementImportSkipEvaluator.TryCreate(engagement, out var manualMetadata) &&
                                    manualMetadata.ReasonKey == "ManualOnly")
                                {
                                    manualOnlySkips.Add(detail);
                                    warningMessages.Add(manualMetadata.WarningMessage);
                                    Logger.LogWarning(manualMetadata.WarningMessage);
                                }

                                continue;
                            }

                            if (EngagementImportSkipEvaluator.TryCreate(engagement, out var closedMetadata) &&
                                closedMetadata.ReasonKey == "ClosedEngagement")
                            {
                                closedEngagementSkips.Add(detail);
                                warningMessages.Add(closedMetadata.WarningMessage);
                                Logger.LogWarning(closedMetadata.WarningMessage);
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
                                allowCreateOrUpdate: true);

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

                            if (row.ManagerGuiIds.Count > 0)
                            {
                                managerAssignmentChanges += SyncManagerAssignments(
                                    context,
                                    engagement,
                                    row.ManagerGuiIds,
                                    managersByGui,
                                    missingManagerGuis);
                            }

                            if (row.PartnerGuiIds.Count > 0)
                            {
                                papdAssignmentChanges += SyncPapdAssignments(
                                    context,
                                    engagement,
                                    row.PartnerGuiIds,
                                    papdsByGui,
                                    missingPapdGuis);
                            }

                            if (row.OriginalBudgetHours.HasValue)
                            {
                                engagement.InitialHoursBudget = row.OriginalBudgetHours.Value;
                            }

                            if (row.OriginalBudgetMarginPercent.HasValue)
                            {
                                engagement.MarginPctBudget = row.OriginalBudgetMarginPercent;
                            }

                            if (row.OriginalBudgetExpenses.HasValue)
                            {
                                engagement.OpeningExpenses = row.OriginalBudgetExpenses.Value;
                            }

                            // Always process period-specific data with user-selected closing period
                            if (row.ChargedHours.HasValue)
                            {
                                engagement.EstimatedToCompleteHours = row.ChargedHours.Value;
                            }

                            var valueData = row.ValueData ?? row.TERMercuryProjectedOppCurrency;

                            if (valueData.HasValue)
                            {
                                engagement.ValueEtcp = valueData.Value;
                            }

                            if (row.ToDateMargin.HasValue)
                            {
                                engagement.MarginPctEtcp = row.ToDateMargin;
                            }

                            if (row.ExpensesToDate.HasValue)
                            {
                                engagement.ExpensesEtcp = row.ExpensesToDate.Value;
                            }

                            if (row.UnbilledRevenueDays.HasValue)
                            {
                                engagement.UnbilledRevenueDays = row.UnbilledRevenueDays.Value;
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

                            // Always create FinancialEvolution and process RevenueAllocations
                            // Calculate revenue to-go and to-date values from backlog data
                            var revenueToGo = row.CurrentFiscalYearBacklog;
                            var revenueToDate = row.CurrentFiscalYearToDate ??
                                ((row.CurrentFiscalYearBacklog.HasValue || row.FutureFiscalYearBacklog.HasValue)
                                    ? engagement.ValueToAllocate - (row.CurrentFiscalYearBacklog ?? 0m) - (row.FutureFiscalYearBacklog ?? 0m)
                                    : (decimal?)null);

                            financialEvolutionUpserts += UpsertFinancialEvolution(
                                context,
                                engagement,
                                closingPeriod.Id.ToString(CultureInfo.InvariantCulture),
                                row.OriginalBudgetHours,
                                row.ChargedHours,
                                row.FYTDHours,
                                valueData,
                                row.OriginalBudgetMarginPercent,
                                row.ToDateMargin,
                                row.FYTDMargin,
                                row.OriginalBudgetExpenses,
                                row.ExpensesToDate,
                                row.FYTDExpenses,
                                closingPeriod.FiscalYearId,
                                revenueToGo,
                                revenueToDate);

                            // Process backlog data if available
                            if (row.CurrentFiscalYearBacklog.HasValue || row.FutureFiscalYearBacklog.HasValue)
                            {
                                ProcessBacklogData(
                                    context,
                                    engagement,
                                    closingPeriod,
                                    row.CurrentFiscalYearBacklog ?? 0m,
                                    row.FutureFiscalYearBacklog ?? 0m,
                                    Logger);
                            }
                        }
                        catch (Exception ex)
                        {
                            var errorMessage = $"Row {row.RowNumber}: {ex.Message}";
                            errors.Add(errorMessage);
                            Logger.LogError(ex, "Error processing Full Management Data row {RowNumber} for engagement {EngagementId}.", row.RowNumber, row.EngagementId);
                        }
                    }

                    await context.SaveChangesAsync().ConfigureAwait(false);
                    await transaction.CommitAsync().ConfigureAwait(false);

                    var skipReasons = new Dictionary<string, IReadOnlyCollection<string>>();

                    if (manualOnlySkips.Count > 0)
                    {
                        skipReasons["ManualOnly"] = manualOnlySkips;
                    }

                    if (closedEngagementSkips.Count > 0)
                    {
                        skipReasons["ClosedEngagement"] = closedEngagementSkips;
                    }

                    if (missingEngagementSkips.Count > 0)
                    {
                        skipReasons["Engagement not found"] = missingEngagementSkips;
                    }

                    if (errors.Count > 0)
                    {
                        skipReasons["Error"] = errors;
                    }

                    if (missingManagerGuis.Count > 0)
                    {
                        var sortedManagers = missingManagerGuis
                            .OrderBy(gui => gui, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        warningMessages.Add($"Manager GUI identifiers not found: {string.Join(", ", sortedManagers)}.");
                    }

                    if (missingPapdGuis.Count > 0)
                    {
                        var sortedPapds = missingPapdGuis
                            .OrderBy(gui => gui, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        warningMessages.Add($"PAPD GUI identifiers not found: {string.Join(", ", sortedPapds)}.");
                    }

                    var notes = new List<string>
                    {
                        $"Financial evolution entries upserted: {financialEvolutionUpserts}",
                        $"Distinct engagements updated: {updatedEngagements.Count}",
                        $"Closing period used: {closingPeriod.Name} (ID: {closingPeriod.Id})"
                    };

                    if (managerAssignmentChanges > 0)
                    {
                        notes.Add($"Manager assignments synchronized: {managerAssignmentChanges}");
                    }

                    if (papdAssignmentChanges > 0)
                    {
                        notes.Add($"PAPD assignments synchronized: {papdAssignmentChanges}");
                    }

                    if (warningMessages.Count > 0)
                    {
                        foreach (var warning in warningMessages)
                        {
                            notes.Insert(0, warning);
                        }
                    }

                    if (s4MetadataRefreshes > 0)
                    {
                        notes.Insert(0, $"S/4 metadata rows refreshed: {s4MetadataRefreshes}");
                    }

                    var summary = ImportSummaryFormatter.Build(
                        "Full Management Data import",
                        0,
                        updatedEngagements.Count,
                        skipReasons,
                        notes,
                        parsedRows.Count);
                    Logger.LogInformation(summary);

                    return new FullManagementDataImportResult(
                        summary,
                        parsedRows.Count,
                        0,
                        updatedEngagements.Count,
                        financialEvolutionUpserts,
                        s4MetadataRefreshes,
                        manualOnlySkips.ToArray(),
                        Array.Empty<string>(), // lockedFiscalYearSkips (now checked at import start)
                        Array.Empty<string>(), // missingClosingPeriodSkips (now from UI selection)
                        missingEngagementSkips.ToArray(),
                        closedEngagementSkips.ToArray(),
                        errors.ToArray(),
                        warningMessages.ToArray());
                }
                catch
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
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
            var valueDataIndex = GetOptionalColumnIndex(headerMap, ValueDataHeaders);
            var terFiscalYearToDateIndex = GetOptionalColumnIndex(headerMap, TerFiscalYearToDateHeaders);
            var marginPercentMercuryProjectedIndex = GetOptionalColumnIndex(headerMap, MarginPercentMercuryProjectedHeaders);
            var expensesMercuryProjectedIndex = GetOptionalColumnIndex(headerMap, ExpensesMercuryProjectedHeaders);
            var statusIndex = GetOptionalColumnIndex(headerMap, StatusHeaders);
            var engagementPartnerGuiIndex = GetOptionalColumnIndex(headerMap, EngagementPartnerGuiHeaders);
            var engagementManagerGuiIndex = GetOptionalColumnIndex(headerMap, EngagementManagerGuiHeaders);
            var etcAgeDaysIndex = GetOptionalColumnIndex(headerMap, EtcAgeDaysHeaders);
            var unbilledRevenueDaysIndex = GetOptionalColumnIndex(headerMap, UnbilledRevenueDaysHeaders);
            var lastActiveEtcPDateIndex = GetOptionalColumnIndex(headerMap, LastActiveEtcPDateHeaders);
            var nextEtcDateIndex = GetOptionalColumnIndex(headerMap, NextEtcDateHeaders);
            var currentFiscalYearBacklogIndex = GetOptionalColumnIndex(headerMap, CurrentFiscalYearBacklogHeaders);
            var futureFiscalYearBacklogIndex = GetOptionalColumnIndex(headerMap, FutureFiscalYearBacklogHeaders);
            var chargedHoursETDIndex = GetOptionalColumnIndex(headerMap, ChargedHoursETDHeaders);
            var chargedHoursFYTDIndex = GetOptionalColumnIndex(headerMap, ChargedHoursFYTDHeaders);
            var marginPercentETDIndex = GetOptionalColumnIndex(headerMap, MarginPercentETDHeaders);
            var marginPercentFYTDIndex = GetOptionalColumnIndex(headerMap, MarginPercentFYTDHeaders);
            var expensesETDIndex = GetOptionalColumnIndex(headerMap, ExpensesETDHeaders);
            var expensesFYTDIndex = GetOptionalColumnIndex(headerMap, ExpensesFYTDHeaders);

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

                rows.Add(new FullManagementDataRow
                {
                    RowNumber = rowNumber,
                    EngagementId = engagementId,
                    EngagementName = engagementNameIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[engagementNameIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    CustomerName = customerNameIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[customerNameIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    CustomerCode = customerIdIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[customerIdIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    OpportunityCurrency = opportunityCurrencyIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[opportunityCurrencyIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    OriginalBudgetHours = originalBudgetHoursIndex.HasValue ? ParseDecimal(row[originalBudgetHoursIndex.Value], 2) : null,
                    OriginalBudgetTer = originalBudgetTerIndex.HasValue ? ParseDecimal(row[originalBudgetTerIndex.Value], 2) : null,
                    OriginalBudgetMarginPercent = originalBudgetMarginPercentIndex.HasValue ? ParsePercent(row[originalBudgetMarginPercentIndex.Value]) : null,
                    OriginalBudgetExpenses = originalBudgetExpensesIndex.HasValue ? ParseDecimal(row[originalBudgetExpensesIndex.Value], 2) : null,
                    ChargedHours = chargedHoursETDIndex.HasValue ? ParseDecimal(row[chargedHoursETDIndex.Value], 2) : null,
                    FYTDHours = chargedHoursFYTDIndex.HasValue ? ParseDecimal(row[chargedHoursFYTDIndex.Value], 2) : null,
                    TERMercuryProjectedOppCurrency = termMercuryProjectedIndex.HasValue ? ParseDecimal(row[termMercuryProjectedIndex.Value], 2) : null,
                    ValueData = valueDataIndex.HasValue ? ParseDecimal(row[valueDataIndex.Value], 2) : null,
                    CurrentFiscalYearToDate = terFiscalYearToDateIndex.HasValue ? ParseDecimal(row[terFiscalYearToDateIndex.Value], 2) : null,
                    ToDateMargin = marginPercentETDIndex.HasValue ? ParsePercent(row[marginPercentETDIndex.Value]) : null,
                    FYTDMargin = marginPercentFYTDIndex.HasValue ? ParsePercent(row[marginPercentFYTDIndex.Value]) : null,
                    ExpensesToDate = expensesETDIndex.HasValue ? ParseDecimal(row[expensesETDIndex.Value], 2) : null,
                    FYTDExpenses = expensesFYTDIndex.HasValue ? ParseDecimal(row[expensesFYTDIndex.Value], 2) : null,
                    StatusText = statusIndex.HasValue ? NormalizeWhitespace(Convert.ToString(row[statusIndex.Value], CultureInfo.InvariantCulture)) : string.Empty,
                    PartnerGuiIds = engagementPartnerGuiIndex.HasValue ? ParseGuiIdentifiers(row[engagementPartnerGuiIndex.Value]) : Array.Empty<string>(),
                    ManagerGuiIds = engagementManagerGuiIndex.HasValue ? ParseGuiIdentifiers(row[engagementManagerGuiIndex.Value]) : Array.Empty<string>(),
                    EtcpAgeDays = etcAgeDaysIndex.HasValue ? ParseInt(row[etcAgeDaysIndex.Value]) : null,
                    UnbilledRevenueDays = unbilledRevenueDaysIndex.HasValue ? ParseInt(row[unbilledRevenueDaysIndex.Value]) : null,
                    LastActiveEtcPDate = lastActiveEtcPDateIndex.HasValue ? ParseDate(row[lastActiveEtcPDateIndex.Value]) : null,
                    NextEtcDate = nextEtcDateIndex.HasValue ? ParseDate(row[nextEtcDateIndex.Value]) : null,
                    CurrentFiscalYearBacklog = currentFiscalYearBacklogIndex.HasValue ? ParseDecimal(row[currentFiscalYearBacklogIndex.Value], 2) : null,
                    FutureFiscalYearBacklog = futureFiscalYearBacklogIndex.HasValue ? ParseDecimal(row[futureFiscalYearBacklogIndex.Value], 2) : null
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
            // First pass: exact match (optimized with direct lookup via values)
            var headerValues = headerMap.Values.ToList();
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                for (var j = 0; j < headerValues.Count; j++)
                {
                    if (string.Equals(headerValues[j], candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        // Find the key for this value
                        foreach (var kvp in headerMap)
                        {
                            if (string.Equals(kvp.Value, candidate, StringComparison.OrdinalIgnoreCase))
                            {
                                return kvp.Key;
                            }
                        }
                    }
                }
            }

            // Second pass: partial match with exclusions
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                var candidateLower = candidate.ToLowerInvariant();
                var isEngagement = candidateLower.Contains("engagement");
                var isDescription = string.Equals(candidate, "description", StringComparison.OrdinalIgnoreCase);

                foreach (var kvp in headerMap)
                {
                    var header = kvp.Value;
                    if (string.IsNullOrEmpty(header))
                    {
                        continue;
                    }

                    if (!header.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (isEngagement && header.Contains("id", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (isDescription &&
                        (header.Contains("customer", StringComparison.OrdinalIgnoreCase) || header.Contains("client", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    return kvp.Key;
                }
            }

            return null;
        }

        private static bool IsS4MetadataWorkbook(Dictionary<int, string> headerMap)
        {
            var hasEngagement = ContainsAnyHeader(headerMap, EngagementIdHeaders);
            var hasClientDetails = ContainsAnyHeader(headerMap, CustomerIdHeaders) || ContainsAnyHeader(headerMap, CustomerNameHeaders);
            var hasStatus = ContainsAnyHeader(headerMap, StatusHeaders);
            var lacksFinancialColumns = !ContainsAnyHeader(headerMap, OriginalBudgetHoursHeaders)
                                        && !ContainsAnyHeader(headerMap, OriginalBudgetTerHeaders)
                                        && !ContainsAnyHeader(headerMap, OriginalBudgetMarginPercentHeaders)
                                        && !ContainsAnyHeader(headerMap, OriginalBudgetExpensesHeaders)
                                        && !ContainsAnyHeader(headerMap, ChargedHoursMercuryProjectedHeaders)
                                        && !ContainsAnyHeader(headerMap, TermMercuryProjectedHeaders)
                                        && !ContainsAnyHeader(headerMap, MarginPercentMercuryProjectedHeaders)
                                        && !ContainsAnyHeader(headerMap, ExpensesMercuryProjectedHeaders)
                                        && !ContainsAnyHeader(headerMap, EtcAgeDaysHeaders)
                                        && !ContainsAnyHeader(headerMap, UnbilledRevenueDaysHeaders)
                                        && !ContainsAnyHeader(headerMap, LastActiveEtcPDateHeaders)
                                        && !ContainsAnyHeader(headerMap, NextEtcDateHeaders);
            var missingClosingPeriod = !ContainsAnyHeader(headerMap, ClosingPeriodHeaders);

            return hasEngagement && hasClientDetails && hasStatus && lacksFinancialColumns && missingClosingPeriod;
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

        private new static decimal? ParseDecimal(object? value, int? decimals)
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

        private static IReadOnlyList<string> ParseGuiIdentifiers(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return Array.Empty<string>();
            }

            var rawValue = NormalizeWhitespace(Convert.ToString(value, CultureInfo.InvariantCulture));
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return Array.Empty<string>();
            }

            var separators = new[] { ';', ',', '|', '\n', '\r' };
            var segments = rawValue.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return Array.Empty<string>();
            }

            var identifiers = new List<string>();
            foreach (var segment in segments)
            {
                var normalized = NormalizeWhitespace(segment);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (!identifiers.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    identifiers.Add(normalized);
                }
            }

            return identifiers;
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

        private static bool TryUpdateCustomerMetadataForPlaceholder(
            Engagement engagement,
            FullManagementDataRow row,
            IDictionary<string, Customer> customersByCode,
            IDictionary<string, Customer> customersByName)
        {
            if (engagement.Customer is null)
            {
                return false;
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
                return false;
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

            return true;
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

        /// <summary>
        /// Creates or updates a FinancialEvolution snapshot for the given engagement and closing period.
        /// Uses composite key (EngagementId, ClosingPeriodId) to upsert: existing snapshots are updated,
        /// new ones are inserted.
        /// 
        /// Design rationale: Budget values (budgetHours, budgetMargin, expenseBudget) are constant
        /// across snapshots. ETD and FYTD values reflect period-specific actuals. This allows
        /// time-series analysis while keeping baseline budget data consistent.
        /// </summary>
        private static int UpsertFinancialEvolution(
            ApplicationDbContext context,
            Engagement engagement,
            string closingPeriodId,
            decimal? budgetHours,
            decimal? chargedHours,
            decimal? fytdHours,
            decimal? value,
            decimal? budgetMargin,
            decimal? toDateMargin,
            decimal? fytdMargin,
            decimal? expenseBudget,
            decimal? expensesToDate,
            decimal? fytdExpenses,
            int? fiscalYearId = null,
            decimal? revenueToGoValue = null,
            decimal? revenueToDateValue = null)
        {
            if (!budgetHours.HasValue && !chargedHours.HasValue && !fytdHours.HasValue &&
                !value.HasValue && !budgetMargin.HasValue && !toDateMargin.HasValue && !fytdMargin.HasValue &&
                !expenseBudget.HasValue && !expensesToDate.HasValue && !fytdExpenses.HasValue &&
                !revenueToGoValue.HasValue && !revenueToDateValue.HasValue)
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
            evolution.BudgetHours = budgetHours;
            evolution.ChargedHours = chargedHours;
            evolution.FYTDHours = fytdHours;
            evolution.AdditionalHours = null;
            evolution.ValueData = value;
            evolution.BudgetMargin = budgetMargin;
            evolution.ToDateMargin = toDateMargin;
            evolution.FYTDMargin = fytdMargin;
            evolution.ExpenseBudget = expenseBudget;
            evolution.ExpensesToDate = expensesToDate;
            evolution.FYTDExpenses = fytdExpenses;
            evolution.FiscalYearId = fiscalYearId;
            evolution.RevenueToGoValue = revenueToGoValue;
            evolution.RevenueToDateValue = revenueToDateValue;

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
            public decimal? OriginalBudgetHours { get; init; }
            public decimal? OriginalBudgetTer { get; init; }
            public decimal? OriginalBudgetMarginPercent { get; init; }
            public decimal? OriginalBudgetExpenses { get; init; }
            public decimal? ChargedHours { get; init; }
            public decimal? FYTDHours { get; init; }
            public decimal? TERMercuryProjectedOppCurrency { get; init; }
            public decimal? ValueData { get; init; }
            public decimal? CurrentFiscalYearToDate { get; init; }
            public decimal? ToDateMargin { get; init; }
            public decimal? FYTDMargin { get; init; }
            public decimal? ExpensesToDate { get; init; }
            public decimal? FYTDExpenses { get; init; }
            public string StatusText { get; init; } = string.Empty;
            public IReadOnlyList<string> PartnerGuiIds { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> ManagerGuiIds { get; init; } = Array.Empty<string>();
            public int? EtcpAgeDays { get; init; }
            public int? UnbilledRevenueDays { get; init; }
            public DateTime? LastActiveEtcPDate { get; init; }
            public DateTime? NextEtcDate { get; init; }
            public decimal? CurrentFiscalYearBacklog { get; init; }
            public decimal? FutureFiscalYearBacklog { get; init; }
        }

        private static int SyncManagerAssignments(
            ApplicationDbContext context,
            Engagement engagement,
            IReadOnlyCollection<string> managerGuiIds,
            IReadOnlyDictionary<string, Manager> managersByGui,
            ISet<string> missingManagerGuis)
        {
            if (managerGuiIds.Count == 0)
            {
                return 0;
            }

            var desiredManagerIds = new HashSet<int>();
            foreach (var gui in managerGuiIds)
            {
                if (managersByGui.TryGetValue(gui, out var manager))
                {
                    desiredManagerIds.Add(manager.Id);
                }
                else
                {
                    missingManagerGuis.Add(gui);
                }
            }

            if (desiredManagerIds.Count == 0)
            {
                return 0;
            }

            var changes = 0;
            var existingAssignments = engagement.ManagerAssignments.ToList();

            foreach (var assignment in existingAssignments)
            {
                if (!desiredManagerIds.Contains(assignment.ManagerId))
                {
                    context.EngagementManagerAssignments.Remove(assignment);
                    engagement.ManagerAssignments.Remove(assignment);
                    changes++;
                }
            }

            var currentManagerIds = engagement.ManagerAssignments.Select(a => a.ManagerId).ToHashSet();
            foreach (var managerId in desiredManagerIds)
            {
                if (currentManagerIds.Contains(managerId))
                {
                    continue;
                }

                var assignment = new EngagementManagerAssignment
                {
                    EngagementId = engagement.Id,
                    ManagerId = managerId
                };

                engagement.ManagerAssignments.Add(assignment);
                context.EngagementManagerAssignments.Add(assignment);
                changes++;
            }

            return changes;
        }

        private static int SyncPapdAssignments(
            ApplicationDbContext context,
            Engagement engagement,
            IReadOnlyCollection<string> papdGuiIds,
            IReadOnlyDictionary<string, Papd> papdsByGui,
            ISet<string> missingPapdGuis)
        {
            if (papdGuiIds.Count == 0)
            {
                return 0;
            }

            var desiredPapdIds = new HashSet<int>();
            foreach (var gui in papdGuiIds)
            {
                if (papdsByGui.TryGetValue(gui, out var papd))
                {
                    desiredPapdIds.Add(papd.Id);
                }
                else
                {
                    missingPapdGuis.Add(gui);
                }
            }

            if (desiredPapdIds.Count == 0)
            {
                return 0;
            }

            var changes = 0;
            var existingAssignments = engagement.EngagementPapds.ToList();

            foreach (var assignment in existingAssignments)
            {
                if (!desiredPapdIds.Contains(assignment.PapdId))
                {
                    context.EngagementPapds.Remove(assignment);
                    engagement.EngagementPapds.Remove(assignment);
                    changes++;
                }
            }

            var currentPapdIds = engagement.EngagementPapds.Select(a => a.PapdId).ToHashSet();
            foreach (var papdId in desiredPapdIds)
            {
                if (currentPapdIds.Contains(papdId))
                {
                    continue;
                }

                var assignment = new EngagementPapd
                {
                    EngagementId = engagement.Id,
                    PapdId = papdId
                };

                engagement.EngagementPapds.Add(assignment);
                context.EngagementPapds.Add(assignment);
                changes++;
            }

            return changes;
        }

        private static void ProcessBacklogData(
            ApplicationDbContext context,
            Engagement engagement,
            ClosingPeriod closingPeriod,
            decimal currentBacklog,
            decimal futureBacklog,
            ILogger logger)
        {
            if (closingPeriod.FiscalYear == null)
            {
                logger.LogWarning(
                    "Skipping backlog processing for engagement {EngagementId} because closing period {ClosingPeriod} has no fiscal year.",
                    engagement.EngagementId,
                    closingPeriod.Name);
                return;
            }

            var currentFiscalYear = closingPeriod.FiscalYear;
            var currentFiscalYearName = currentFiscalYear.Name;

            if (string.IsNullOrWhiteSpace(currentFiscalYearName))
            {
                logger.LogWarning(
                    "Skipping backlog processing for engagement {EngagementId} because fiscal year has no name.",
                    engagement.EngagementId);
                return;
            }

            // Calculate next fiscal year name
            string? nextFiscalYearName = null;
            try
            {
                nextFiscalYearName = IncrementFiscalYearName(currentFiscalYearName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Unable to determine next fiscal year from '{CurrentFiscalYear}' for engagement {EngagementId}. Future backlog will be skipped.",
                    currentFiscalYearName,
                    engagement.EngagementId);
            }

            // Load fiscal years
            var fiscalYears = context.FiscalYears
                .AsNoTracking()
                .Where(fy => fy.Name == currentFiscalYearName || (nextFiscalYearName != null && fy.Name == nextFiscalYearName))
                .ToList();

            var fiscalYearLookup = fiscalYears.ToDictionary(fy => fy.Name, StringComparer.OrdinalIgnoreCase);

            if (!fiscalYearLookup.TryGetValue(currentFiscalYearName, out var currentFy))
            {
                logger.LogWarning(
                    "Fiscal year '{FiscalYear}' not found for engagement {EngagementId}. Current backlog will be skipped.",
                    currentFiscalYearName,
                    engagement.EngagementId);
            }
            else if (!currentFy.IsLocked)
            {
                var toGoCurrent = RoundMoney(currentBacklog);
                var toDateCurrent = RoundMoney(engagement.ValueToAllocate - currentBacklog - futureBacklog);

                // Snapshot-based: Look for allocation for this specific closing period
                var allocation = engagement.RevenueAllocations
                    .FirstOrDefault(a => a.FiscalYearId == currentFy.Id && a.ClosingPeriodId == closingPeriod.Id);

                if (allocation == null)
                {
                    allocation = new EngagementFiscalYearRevenueAllocation
                    {
                        EngagementId = engagement.Id,
                        FiscalYearId = currentFy.Id,
                        ClosingPeriodId = closingPeriod.Id,
                        ToGoValue = toGoCurrent,
                        ToDateValue = toDateCurrent,
                        UpdatedAt = DateTime.UtcNow
                    };
                    engagement.RevenueAllocations.Add(allocation);
                    context.EngagementFiscalYearRevenueAllocations.Add(allocation);
                }
                else
                {
                    // Update existing snapshot for this closing period
                    allocation.ToGoValue = toGoCurrent;
                    allocation.ToDateValue = toDateCurrent;
                    allocation.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Process future backlog
            if (nextFiscalYearName != null && fiscalYearLookup.TryGetValue(nextFiscalYearName, out var nextFy))
            {
                if (!nextFy.IsLocked)
                {
                    var toGoNext = RoundMoney(futureBacklog);

                    // Snapshot-based: Look for allocation for this specific closing period
                    var nextAllocation = engagement.RevenueAllocations
                        .FirstOrDefault(a => a.FiscalYearId == nextFy.Id && a.ClosingPeriodId == closingPeriod.Id);

                    if (nextAllocation == null)
                    {
                        nextAllocation = new EngagementFiscalYearRevenueAllocation
                        {
                            EngagementId = engagement.Id,
                            FiscalYearId = nextFy.Id,
                            ClosingPeriodId = closingPeriod.Id,
                            ToGoValue = toGoNext,
                            ToDateValue = 0m,
                            UpdatedAt = DateTime.UtcNow
                        };
                        engagement.RevenueAllocations.Add(nextAllocation);
                        context.EngagementFiscalYearRevenueAllocations.Add(nextAllocation);
                    }
                    else
                    {
                        // Update existing snapshot for this closing period
                        nextAllocation.ToGoValue = toGoNext;
                        nextAllocation.ToDateValue = 0m;
                        nextAllocation.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
        }


        private static string IncrementFiscalYearName(string fiscalYearName)
        {
            if (string.IsNullOrWhiteSpace(fiscalYearName))
            {
                throw new ArgumentException("Fiscal year name must be provided.", nameof(fiscalYearName));
            }

            var match = Regex.Match(fiscalYearName, @"\d+");
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
    }
}
