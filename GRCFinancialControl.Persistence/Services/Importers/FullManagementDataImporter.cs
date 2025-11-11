using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static GRCFinancialControl.Persistence.Services.Utilities.DataNormalizationService;

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
            "ter etd",
            "value data",
            "valuedata",
            "etd value"
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

        private const int DefaultHeaderRowIndex = 10; // Row 11 in Excel (0-based index)

        private static class FieldNames
        {
            public const string EngagementId = nameof(FullManagementDataRow.EngagementId);
            public const string EngagementDescription = nameof(FullManagementDataRow.EngagementName);
            public const string CustomerName = nameof(FullManagementDataRow.CustomerName);
            public const string CustomerCode = nameof(FullManagementDataRow.CustomerCode);
            public const string OpportunityCurrency = nameof(FullManagementDataRow.OpportunityCurrency);
            public const string OriginalBudgetHours = nameof(FullManagementDataRow.OriginalBudgetHours);
            public const string OriginalBudgetTer = nameof(FullManagementDataRow.OriginalBudgetTer);
            public const string OriginalBudgetMarginPercent = nameof(FullManagementDataRow.OriginalBudgetMarginPercent);
            public const string OriginalBudgetExpenses = nameof(FullManagementDataRow.OriginalBudgetExpenses);
            public const string ChargedHours = nameof(FullManagementDataRow.ChargedHours);
            public const string ChargedHoursFytd = nameof(FullManagementDataRow.FYTDHours);
            public const string TermMercuryProjectedOppCurrency = nameof(FullManagementDataRow.TERMercuryProjectedOppCurrency);
            public const string ValueData = nameof(FullManagementDataRow.ValueData);
            public const string TerFiscalYearToDate = nameof(FullManagementDataRow.CurrentFiscalYearToDate);
            public const string MarginPercentEtd = nameof(FullManagementDataRow.ToDateMargin);
            public const string MarginPercentFytd = nameof(FullManagementDataRow.FYTDMargin);
            public const string ExpensesEtd = nameof(FullManagementDataRow.ExpensesToDate);
            public const string ExpensesFytd = nameof(FullManagementDataRow.FYTDExpenses);
            public const string Status = nameof(FullManagementDataRow.StatusText);
            public const string EngagementPartnerGui = nameof(FullManagementDataRow.PartnerGuiIds);
            public const string EngagementManagerGui = nameof(FullManagementDataRow.ManagerGuiIds);
            public const string EtcAgeDays = nameof(FullManagementDataRow.EtcpAgeDays);
            public const string UnbilledRevenueDays = nameof(FullManagementDataRow.UnbilledRevenueDays);
            public const string LastActiveEtcPDate = nameof(FullManagementDataRow.LastActiveEtcPDate);
            public const string NextEtcDate = nameof(FullManagementDataRow.NextEtcDate);
            public const string CurrentFiscalYearBacklog = nameof(FullManagementDataRow.CurrentFiscalYearBacklog);
            public const string FutureFiscalYearBacklog = nameof(FullManagementDataRow.FutureFiscalYearBacklog);
        }

        private sealed record ColumnDefinition(
            string FieldName,
            string HeaderLabel,
            string ColumnLetter,
            int ColumnIndex,
            string[] Aliases,
            bool IsRequired);

        private sealed record ColumnIndexes(
            int HeaderRowIndex,
            int EngagementId,
            int? EngagementDescription,
            int? CustomerName,
            int? CustomerCode,
            int? OpportunityCurrency,
            int? OriginalBudgetHours,
            int? OriginalBudgetTer,
            int? OriginalBudgetMarginPercent,
            int? OriginalBudgetExpenses,
            int? ChargedHours,
            int? ChargedHoursFytd,
            int? TermMercuryProjectedOppCurrency,
            int? ValueData,
            int? TerFiscalYearToDate,
            int? MarginPercentEtd,
            int? MarginPercentFytd,
            int? ExpensesEtd,
            int? ExpensesFytd,
            int? Status,
            int? EngagementPartnerGui,
            int? EngagementManagerGui,
            int? EtcAgeDays,
            int? UnbilledRevenueDays,
            int? LastActiveEtcPDate,
            int? NextEtcDate,
            int? CurrentFiscalYearBacklog,
            int? FutureFiscalYearBacklog);

        private static readonly ColumnDefinition[] ColumnDefinitions =
        {
            new(FieldNames.EngagementId, "Engagement ID", "A", ColumnLetterToIndex("A"), EngagementIdHeaders, true),
            new(FieldNames.EngagementDescription, "Engagement", "B", ColumnLetterToIndex("B"), EngagementNameHeaders, false),
            new(FieldNames.CustomerName, "Client", "BK", ColumnLetterToIndex("BK"), CustomerNameHeaders, false),
            new(FieldNames.CustomerCode, "Client ID", "BI", ColumnLetterToIndex("BI"), CustomerIdHeaders, false),
            new(FieldNames.OpportunityCurrency, "Opportunity Currency", "FX", ColumnLetterToIndex("FX"), OpportunityCurrencyHeaders, false),
            new(FieldNames.OriginalBudgetHours, "Original Budget Hours", "HL", ColumnLetterToIndex("HL"), OriginalBudgetHoursHeaders, false),
            new(FieldNames.OriginalBudgetTer, "Original Budget TER", "HV", ColumnLetterToIndex("HV"), OriginalBudgetTerHeaders, false),
            new(FieldNames.OriginalBudgetMarginPercent, "Original Budget Margin %", "HW", ColumnLetterToIndex("HW"), OriginalBudgetMarginPercentHeaders, false),
            new(FieldNames.OriginalBudgetExpenses, "Original Budget Expenses", "HQ", ColumnLetterToIndex("HQ"), OriginalBudgetExpensesHeaders, false),
            new(FieldNames.ChargedHours, "Charged Hours ETD", "CI", ColumnLetterToIndex("CI"), ChargedHoursETDHeaders, false),
            new(FieldNames.ChargedHoursFytd, "Charged Hours FYTD", "CJ", ColumnLetterToIndex("CJ"), ChargedHoursFYTDHeaders, false),
            new(FieldNames.TermMercuryProjectedOppCurrency, "TER Mercury Projected Opp Currency", "FP", ColumnLetterToIndex("FP"), TermMercuryProjectedHeaders, false),
            new(FieldNames.ValueData, "TER ETD", "CU", ColumnLetterToIndex("CU"), ValueDataHeaders, false),
            new(FieldNames.TerFiscalYearToDate, "TER FYTD", "CV", ColumnLetterToIndex("CV"), TerFiscalYearToDateHeaders, false),
            new(FieldNames.MarginPercentEtd, "Margin % ETD", "CG", ColumnLetterToIndex("CG"), MarginPercentETDHeaders, false),
            new(FieldNames.MarginPercentFytd, "Margin % FYTD", "CH", ColumnLetterToIndex("CH"), MarginPercentFYTDHeaders, false),
            new(FieldNames.ExpensesEtd, "Expenses ETD", "DH", ColumnLetterToIndex("DH"), ExpensesETDHeaders, false),
            new(FieldNames.ExpensesFytd, "Expenses FYTD", "DI", ColumnLetterToIndex("DI"), ExpensesFYTDHeaders, false),
            new(FieldNames.Status, "Engagement Status", "AH", ColumnLetterToIndex("AH"), StatusHeaders, false),
            new(FieldNames.EngagementPartnerGui, "Engagement Partner GUI", "AO", ColumnLetterToIndex("AO"), EngagementPartnerGuiHeaders, false),
            new(FieldNames.EngagementManagerGui, "Engagement Manager GUI", "AZ", ColumnLetterToIndex("AZ"), EngagementManagerGuiHeaders, false),
            new(FieldNames.EtcAgeDays, "ETC-P Age", "FB", ColumnLetterToIndex("FB"), EtcAgeDaysHeaders, false),
            new(FieldNames.UnbilledRevenueDays, "Unbilled Revenue Days", "GA", ColumnLetterToIndex("GA"), UnbilledRevenueDaysHeaders, false),
            new(FieldNames.LastActiveEtcPDate, "Last Active ETC-P Date", "EZ", ColumnLetterToIndex("EZ"), LastActiveEtcPDateHeaders, false),
            new(FieldNames.NextEtcDate, "Next ETC Date", "FD", ColumnLetterToIndex("FD"), NextEtcDateHeaders, false),
            new(FieldNames.CurrentFiscalYearBacklog, "FYTG Backlog", "GR", ColumnLetterToIndex("GR"), CurrentFiscalYearBacklogHeaders, false),
            new(FieldNames.FutureFiscalYearBacklog, "Future FY Backlog", "GS", ColumnLetterToIndex("GS"), FutureFiscalYearBacklogHeaders, false)
        };

        public async Task<FullManagementDataImportResult> ImportAsync(string filePath, int closingPeriodId)
        {
            if (closingPeriodId <= 0)
            {
                throw new ArgumentException("Valid closing period ID must be provided.", nameof(closingPeriodId));
            }

            return await ExecuteWorkbookOperationAsync(
                filePath,
                "Full Management Data",
                workbook => ImportFromWorkbookAsync(workbook, closingPeriodId)).ConfigureAwait(false);
        }

        private async Task<FullManagementDataImportResult> ImportFromWorkbookAsync(
            WorkbookData workbook,
            int closingPeriodId)
        {
            var worksheet = ResolveWorksheet(workbook);
            if (worksheet == null)
            {
                throw new InvalidDataException("The Full Management Data workbook does not contain any worksheets.");
            }

            var fallbackMessages = new List<string>();
            var missingColumns = new List<string>();
            var rowValidationIssues = new List<string>();

            var columnIndexes = ResolveColumnIndexes(worksheet, fallbackMessages, missingColumns, out var hasCriticalMissing);

            if (missingColumns.Count > 0)
            {
                foreach (var message in missingColumns)
                {
                    Logger.LogWarning("{Message}", message);
                }
            }

            if (hasCriticalMissing)
            {
                throw new InvalidDataException(string.Join(Environment.NewLine, missingColumns));
            }

            foreach (var message in fallbackMessages)
            {
                Logger.LogWarning("{Message}", message);
            }

            var parsedRows = ParseRows(worksheet, columnIndexes, rowValidationIssues);
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
                            var revenueToDate = row.CurrentFiscalYearToDate;
                            if (revenueToDate == null && (row.CurrentFiscalYearBacklog.HasValue || row.FutureFiscalYearBacklog.HasValue))
                            {
                                revenueToDate = engagement.ValueToAllocate
                                    - (row.CurrentFiscalYearBacklog ?? 0m)
                                    - (row.FutureFiscalYearBacklog ?? 0m);
                            }

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
                                    revenueToDate,
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

                    if (rowValidationIssues.Count > 0)
                    {
                        skipReasons["IncompleteRow"] = rowValidationIssues;
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

        private static IWorksheet? ResolveWorksheet(WorkbookData workbook)
        {
            return workbook.FirstWorksheet;
        }

        private ColumnIndexes ResolveColumnIndexes(
            IWorksheet worksheet,
            ICollection<string> fallbackMessages,
            ICollection<string> missingColumns,
            out bool hasCriticalMissing)
        {
            if (worksheet == null)
            {
                throw new ArgumentNullException(nameof(worksheet));
            }

            hasCriticalMissing = false;

            var (headerMap, detectedHeaderRowIndex) = BuildBestHeaderMap(worksheet, DefaultHeaderRowIndex);
            var headerRowIndex = detectedHeaderRowIndex >= 0 ? detectedHeaderRowIndex : DefaultHeaderRowIndex;

            var resolvedIndices = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in ColumnDefinitions)
            {
                var expectedNormalized = NormalizeHeader(definition.HeaderLabel);
                var directHeader = NormalizeHeader(GetCellString(worksheet, DefaultHeaderRowIndex, definition.ColumnIndex));

                if (!string.IsNullOrEmpty(directHeader) &&
                    string.Equals(directHeader, expectedNormalized, StringComparison.Ordinal))
                {
                    resolvedIndices[definition.FieldName] = definition.ColumnIndex;
                    continue;
                }

                var aliasIndex = TryResolveAliasColumn(
                    worksheet,
                    headerMap,
                    headerRowIndex,
                    definition,
                    fallbackMessages);

                if (aliasIndex.HasValue)
                {
                    resolvedIndices[definition.FieldName] = aliasIndex.Value;
                }
                else
                {
                    resolvedIndices[definition.FieldName] = null;
                    var message = $"Full Management Data import: Column '{definition.HeaderLabel}' not found at expected column {definition.ColumnLetter} in row 11.";
                    missingColumns.Add(message);
                    if (definition.IsRequired)
                    {
                        hasCriticalMissing = true;
                    }
                }
            }

            return new ColumnIndexes(
                headerRowIndex,
                ResolveRequired(FieldNames.EngagementId),
                ResolveOptional(FieldNames.EngagementDescription),
                ResolveOptional(FieldNames.CustomerName),
                ResolveOptional(FieldNames.CustomerCode),
                ResolveOptional(FieldNames.OpportunityCurrency),
                ResolveOptional(FieldNames.OriginalBudgetHours),
                ResolveOptional(FieldNames.OriginalBudgetTer),
                ResolveOptional(FieldNames.OriginalBudgetMarginPercent),
                ResolveOptional(FieldNames.OriginalBudgetExpenses),
                ResolveOptional(FieldNames.ChargedHours),
                ResolveOptional(FieldNames.ChargedHoursFytd),
                ResolveOptional(FieldNames.TermMercuryProjectedOppCurrency),
                ResolveOptional(FieldNames.ValueData),
                ResolveOptional(FieldNames.TerFiscalYearToDate),
                ResolveOptional(FieldNames.MarginPercentEtd),
                ResolveOptional(FieldNames.MarginPercentFytd),
                ResolveOptional(FieldNames.ExpensesEtd),
                ResolveOptional(FieldNames.ExpensesFytd),
                ResolveOptional(FieldNames.Status),
                ResolveOptional(FieldNames.EngagementPartnerGui),
                ResolveOptional(FieldNames.EngagementManagerGui),
                ResolveOptional(FieldNames.EtcAgeDays),
                ResolveOptional(FieldNames.UnbilledRevenueDays),
                ResolveOptional(FieldNames.LastActiveEtcPDate),
                ResolveOptional(FieldNames.NextEtcDate),
                ResolveOptional(FieldNames.CurrentFiscalYearBacklog),
                ResolveOptional(FieldNames.FutureFiscalYearBacklog));

            int ResolveRequired(string fieldName)
            {
                if (resolvedIndices.TryGetValue(fieldName, out var index) && index.HasValue)
                {
                    return index.Value;
                }

                throw new InvalidOperationException($"Required column '{fieldName}' could not be resolved.");
            }

            int? ResolveOptional(string fieldName)
            {
                return resolvedIndices.TryGetValue(fieldName, out var index) ? index : null;
            }
        }

        private static (Dictionary<string, int> Map, int HeaderRowIndex) BuildBestHeaderMap(
            IWorksheet worksheet,
            int preferredHeaderRowIndex)
        {
            var bestMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var bestIndex = -1;
            var bestScore = -1;

            if (preferredHeaderRowIndex >= 0 && preferredHeaderRowIndex < worksheet.RowCount)
            {
                bestMap = BuildHeaderMap(worksheet, preferredHeaderRowIndex);
                bestIndex = preferredHeaderRowIndex;
                bestScore = CountHeaderMatches(bestMap);
            }

            var searchLimit = Math.Min(worksheet.RowCount, preferredHeaderRowIndex + 15);
            for (var rowIndex = 0; rowIndex < searchLimit; rowIndex++)
            {
                if (rowIndex == preferredHeaderRowIndex)
                {
                    continue;
                }

                var map = BuildHeaderMap(worksheet, rowIndex);
                if (map.Count == 0)
                {
                    continue;
                }

                var score = CountHeaderMatches(map);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMap = map;
                    bestIndex = rowIndex;
                }
            }

            return (bestMap, bestIndex);
        }

        private int? TryResolveAliasColumn(
            IWorksheet worksheet,
            Dictionary<string, int> headerMap,
            int headerRowIndex,
            ColumnDefinition definition,
            ICollection<string> fallbackMessages)
        {
            if (headerMap.Count == 0)
            {
                return null;
            }

            var normalizedCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var alias in definition.Aliases)
            {
                var normalized = NormalizeHeader(alias);
                if (!string.IsNullOrEmpty(normalized))
                {
                    normalizedCandidates.Add(normalized);
                }
            }

            normalizedCandidates.Add(NormalizeHeader(definition.HeaderLabel));

            foreach (var candidate in normalizedCandidates)
            {
                if (headerMap.TryGetValue(candidate, out var columnIndex))
                {
                    LogFallback(columnIndex, candidate, false);
                    return columnIndex;
                }
            }

            foreach (var candidate in normalizedCandidates)
            {
                foreach (var kvp in headerMap)
                {
                    if (kvp.Key.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        LogFallback(kvp.Value, kvp.Key, true);
                        return kvp.Value;
                    }
                }
            }

            return null;

            void LogFallback(int columnIndex, string matchedHeader, bool isPartial)
            {
                var headerText = GetCellString(worksheet, headerRowIndex, columnIndex);
                var descriptor = isPartial ? "partial" : "alias";
                fallbackMessages.Add(
                    $"Full Management Data import: Using {descriptor} header '{headerText}' (column {ColumnIndexToLetter(columnIndex)}) for field '{definition.FieldName}' (expected '{definition.HeaderLabel}' at column {definition.ColumnLetter} in row 11).");
            }
        }

        private List<FullManagementDataRow> ParseRows(
            IWorksheet worksheet,
            ColumnIndexes columnIndexes,
            ICollection<string> rowValidationIssues)
        {
            var headerRowIndex = columnIndexes.HeaderRowIndex >= 0
                ? columnIndexes.HeaderRowIndex
                : DefaultHeaderRowIndex;

            var startRow = Math.Min(headerRowIndex + 1, worksheet.RowCount);

            var rows = new List<FullManagementDataRow>();

            for (var rowIndex = startRow; rowIndex < worksheet.RowCount; rowIndex++)
            {
                if (IsRowEmpty(worksheet, rowIndex))
                {
                    continue;
                }

                var rowNumber = rowIndex + 1;

                var engagementId = NormalizeWhitespace(GetCellString(worksheet, rowIndex, columnIndexes.EngagementId));
                if (string.IsNullOrWhiteSpace(engagementId))
                {
                    rowValidationIssues.Add($"Row {rowNumber}: Missing Engagement ID. Row skipped.");
                    Logger.LogWarning("Skipping Full Management Data row {RowNumber} because the Engagement ID is blank.", rowNumber);
                    continue;
                }

                var engagementName = columnIndexes.EngagementDescription.HasValue
                    ? NormalizeWhitespace(GetCellString(worksheet, rowIndex, columnIndexes.EngagementDescription.Value))
                    : string.Empty;

                var customerName = columnIndexes.CustomerName.HasValue
                    ? NormalizeWhitespace(GetCellString(worksheet, rowIndex, columnIndexes.CustomerName.Value))
                    : string.Empty;

                var customerCode = columnIndexes.CustomerCode.HasValue
                    ? NormalizeWhitespace(GetCellString(worksheet, rowIndex, columnIndexes.CustomerCode.Value))
                    : string.Empty;

                var opportunityCurrency = columnIndexes.OpportunityCurrency.HasValue
                    ? NormalizeWhitespace(GetCellString(worksheet, rowIndex, columnIndexes.OpportunityCurrency.Value))
                    : string.Empty;

                var missingCriticalFields = new List<string>();
                if (columnIndexes.EngagementDescription.HasValue && string.IsNullOrWhiteSpace(engagementName))
                {
                    missingCriticalFields.Add("Engagement Description");
                }

                if (columnIndexes.CustomerName.HasValue && string.IsNullOrWhiteSpace(customerName))
                {
                    missingCriticalFields.Add("Customer Name");
                }

                if (columnIndexes.CustomerCode.HasValue && string.IsNullOrWhiteSpace(customerCode))
                {
                    missingCriticalFields.Add("Customer Code");
                }

                if (columnIndexes.OpportunityCurrency.HasValue && string.IsNullOrWhiteSpace(opportunityCurrency))
                {
                    missingCriticalFields.Add("Opportunity Currency");
                }

                if (missingCriticalFields.Count > 0)
                {
                    var issue = $"Row {rowNumber}: Missing critical fields ({string.Join(", ", missingCriticalFields)}). Row skipped.";
                    rowValidationIssues.Add(issue);
                    Logger.LogWarning(issue);
                    continue;
                }

                rows.Add(new FullManagementDataRow
                {
                    RowNumber = rowNumber,
                    EngagementId = engagementId,
                    EngagementName = engagementName,
                    CustomerName = customerName,
                    CustomerCode = customerCode,
                    OpportunityCurrency = opportunityCurrency,
                    OriginalBudgetHours = columnIndexes.OriginalBudgetHours.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.OriginalBudgetHours.Value), 2) : null,
                    OriginalBudgetTer = columnIndexes.OriginalBudgetTer.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.OriginalBudgetTer.Value), 2) : null,
                    OriginalBudgetMarginPercent = columnIndexes.OriginalBudgetMarginPercent.HasValue ? ParsePercent(GetCellValue(worksheet, rowIndex, columnIndexes.OriginalBudgetMarginPercent.Value)) : null,
                    OriginalBudgetExpenses = columnIndexes.OriginalBudgetExpenses.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.OriginalBudgetExpenses.Value), 2) : null,
                    ChargedHours = columnIndexes.ChargedHours.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.ChargedHours.Value), 2) : null,
                    FYTDHours = columnIndexes.ChargedHoursFytd.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.ChargedHoursFytd.Value), 2) : null,
                    TERMercuryProjectedOppCurrency = columnIndexes.TermMercuryProjectedOppCurrency.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.TermMercuryProjectedOppCurrency.Value), 2) : null,
                    ValueData = columnIndexes.ValueData.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.ValueData.Value), 2) : null,
                    CurrentFiscalYearToDate = columnIndexes.TerFiscalYearToDate.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.TerFiscalYearToDate.Value), 2) : null,
                    ToDateMargin = columnIndexes.MarginPercentEtd.HasValue ? ParsePercent(GetCellValue(worksheet, rowIndex, columnIndexes.MarginPercentEtd.Value)) : null,
                    FYTDMargin = columnIndexes.MarginPercentFytd.HasValue ? ParsePercent(GetCellValue(worksheet, rowIndex, columnIndexes.MarginPercentFytd.Value)) : null,
                    ExpensesToDate = columnIndexes.ExpensesEtd.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.ExpensesEtd.Value), 2) : null,
                    FYTDExpenses = columnIndexes.ExpensesFytd.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.ExpensesFytd.Value), 2) : null,
                    StatusText = columnIndexes.Status.HasValue ? NormalizeWhitespace(GetCellString(worksheet, rowIndex, columnIndexes.Status.Value)) : string.Empty,
                    PartnerGuiIds = columnIndexes.EngagementPartnerGui.HasValue ? ParseGuiIdentifiers(GetCellValue(worksheet, rowIndex, columnIndexes.EngagementPartnerGui.Value)) : Array.Empty<string>(),
                    ManagerGuiIds = columnIndexes.EngagementManagerGui.HasValue ? ParseGuiIdentifiers(GetCellValue(worksheet, rowIndex, columnIndexes.EngagementManagerGui.Value)) : Array.Empty<string>(),
                    EtcpAgeDays = columnIndexes.EtcAgeDays.HasValue ? ParseInt(GetCellValue(worksheet, rowIndex, columnIndexes.EtcAgeDays.Value)) : null,
                    UnbilledRevenueDays = columnIndexes.UnbilledRevenueDays.HasValue ? ParseInt(GetCellValue(worksheet, rowIndex, columnIndexes.UnbilledRevenueDays.Value)) : null,
                    LastActiveEtcPDate = columnIndexes.LastActiveEtcPDate.HasValue ? ParseDate(GetCellValue(worksheet, rowIndex, columnIndexes.LastActiveEtcPDate.Value)) : null,
                    NextEtcDate = columnIndexes.NextEtcDate.HasValue ? ParseDate(GetCellValue(worksheet, rowIndex, columnIndexes.NextEtcDate.Value)) : null,
                    CurrentFiscalYearBacklog = columnIndexes.CurrentFiscalYearBacklog.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.CurrentFiscalYearBacklog.Value), 2) : null,
                    FutureFiscalYearBacklog = columnIndexes.FutureFiscalYearBacklog.HasValue ? ParseDecimal(GetCellValue(worksheet, rowIndex, columnIndexes.FutureFiscalYearBacklog.Value), 2) : null
                });
            }

            return rows;
        }

        private static int CountHeaderMatches(Dictionary<string, int> headerMap)
        {
            if (headerMap.Count == 0)
            {
                return 0;
            }

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

        private static bool ContainsAnyHeader(Dictionary<string, int> headerMap, IEnumerable<string> candidates)
        {
            foreach (var candidate in candidates)
            {
                var normalized = NormalizeHeader(candidate);
                if (headerMap.ContainsKey(normalized))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ColumnLetterToIndex(string columnLetter)
        {
            if (string.IsNullOrWhiteSpace(columnLetter))
            {
                throw new ArgumentException("Column letter must be provided.", nameof(columnLetter));
            }

            var result = 0;
            foreach (var c in columnLetter.Trim().ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z')
                {
                    continue;
                }

                result = result * 26 + (c - 'A' + 1);
            }

            return result - 1;
        }

        private static string ColumnIndexToLetter(int columnIndex)
        {
            if (columnIndex < 0)
            {
                return "?";
            }

            var builder = new StringBuilder();
            var index = columnIndex + 1;

            while (index > 0)
            {
                index--;
                builder.Insert(0, (char)('A' + (index % 26)));
                index /= 26;
            }

            return builder.ToString();
        }

        private static FullManagementReportMetadata ExtractReportMetadata(IWorksheet worksheet)
        {
            var rawValue = GetCellString(worksheet, 3, 0);

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                var searchLimit = Math.Min(worksheet.RowCount, 12);
                for (var rowIndex = 0; rowIndex < searchLimit; rowIndex++)
                {
                    var candidate = GetCellString(worksheet, rowIndex, 0);
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
            public decimal? CurrentFiscalYearToDate { get; set; }
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
            decimal? revenueToDate,
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
                var toDateCurrent = revenueToDate.HasValue
                    ? RoundMoney(revenueToDate.Value)
                    : RoundMoney(engagement.ValueToAllocate - currentBacklog - futureBacklog);

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
