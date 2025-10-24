using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Exporters.Json;

public sealed class JsonExportService : IJsonExportService
{
    private const string WarningBodyTemplateHtml = """
<p>Olá,</p>
<p>Não foram encontrados dados de faturas ou ETCs para o seu portfólio nesta semana.</p>
<p>Por favor, verifique se as informações foram corretamente atualizadas no sistema.</p>
""";

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<JsonExportService> _logger;

    public JsonExportService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<JsonExportService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyCollection<ManagerEmailData>> LoadManagerEmailDataAsync(
        PowerAutomateJsonExportFilters filters,
        CancellationToken cancellationToken = default)
    {
        filters ??= PowerAutomateJsonExportFilters.Empty;

        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        ArgumentNullException.ThrowIfNull(context);

        var assignmentRows = await (
            from assignment in context.EngagementManagerAssignments.AsNoTracking()
            join manager in context.Managers.AsNoTracking() on assignment.ManagerId equals manager.Id
            join engagement in context.Engagements.AsNoTracking() on assignment.EngagementId equals engagement.Id
            join customer in context.Customers.AsNoTracking() on engagement.CustomerId equals customer.Id into customerJoin
            from customer in customerJoin.DefaultIfEmpty()
            select new AssignmentRow(
                assignment.ManagerId,
                manager.Name ?? string.Empty,
                manager.Email ?? string.Empty,
                engagement.Id,
                engagement.EngagementId ?? string.Empty,
                engagement.Description ?? string.Empty,
                engagement.Currency ?? string.Empty,
                customer != null ? customer.Name ?? string.Empty : string.Empty,
                engagement.LastEtcDate,
                engagement.ProposedNextEtcDate)
        ).ToListAsync(cancellationToken);

        if (filters.HasManagerEmailFilter)
        {
            var allowedManagerEmails = new HashSet<string>(filters.ManagerEmails!, StringComparer.OrdinalIgnoreCase);
            assignmentRows = assignmentRows
                .Where(row => !string.IsNullOrWhiteSpace(row.ManagerEmail) && allowedManagerEmails.Contains(row.ManagerEmail))
                .ToList();
        }

        if (assignmentRows.Count == 0)
        {
            _logger.LogInformation("No manager assignments were found while extracting invoice and ETC data.");
            return Array.Empty<ManagerEmailData>();
        }

        if (filters.HasInvoiceDateFilter || filters.HasFiscalYearFilter || filters.HasManagerEmailFilter)
        {
            _logger.LogInformation(
                "Applying export filters — invoiceStart: {InvoiceStart}, invoiceEnd: {InvoiceEnd}, fiscalYear: {FiscalYear}, managers: {ManagerCount}.",
                filters.InvoiceStartDate,
                filters.InvoiceEndDate,
                filters.FiscalYearName,
                filters.ManagerEmails?.Count ?? 0);
        }

        var managerBuilders = assignmentRows
            .GroupBy(row => row.ManagerId)
            .ToDictionary(
                group => group.Key,
                group => new ManagerEmailDataBuilder(
                    group.First().ManagerName,
                    group.First().ManagerEmail),
                EqualityComparer<int>.Default);

        var assignmentsByEngagementCode = assignmentRows
            .Where(row => !string.IsNullOrWhiteSpace(row.EngagementCode))
            .GroupBy(row => row.EngagementCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var assignmentsByEngagementId = assignmentRows
            .GroupBy(row => row.EngagementDbId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        await LoadInvoiceDataAsync(
            context,
            managerBuilders,
            assignmentsByEngagementCode,
            filters,
            cancellationToken);

        await LoadEtcDataAsync(
            context,
            managerBuilders,
            assignmentsByEngagementId,
            filters,
            cancellationToken);

        var result = managerBuilders
            .Select(pair => pair.Value.Build(_logger))
            .OrderBy(data => data.ManagerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "Prepared export data for {ManagerCount} managers ({InvoiceCount} invoices, {EtcCount} ETC records).",
            result.Count,
            result.Sum(data => data.Invoices.Count),
            result.Sum(data => data.Etcs.Count));

        return result;
    }

    private static async Task LoadInvoiceDataAsync(
        ApplicationDbContext context,
        IReadOnlyDictionary<int, ManagerEmailDataBuilder> managerBuilders,
        IReadOnlyDictionary<string, List<AssignmentRow>> assignmentsByEngagementCode,
        PowerAutomateJsonExportFilters filters,
        CancellationToken cancellationToken)
    {
        if (assignmentsByEngagementCode.Count == 0)
        {
            return;
        }

        var engagementCodes = assignmentsByEngagementCode.Keys.ToArray();

        var invoiceStartDate = filters.InvoiceStartDate;
        var invoiceEndDate = filters.InvoiceEndDate;

        var invoiceQuery = context.InvoiceItems.AsNoTracking()
            .Join(
                context.InvoicePlans.AsNoTracking(),
                item => item.PlanId,
                plan => plan.Id,
                (item, plan) => new { item, plan })
            .Where(pair => engagementCodes.Contains(pair.plan.EngagementId));

        if (invoiceStartDate.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(pair =>
                pair.item.EmissionDate.HasValue && pair.item.EmissionDate.Value >= invoiceStartDate.Value);
        }

        if (invoiceEndDate.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(pair =>
                pair.item.EmissionDate.HasValue && pair.item.EmissionDate.Value <= invoiceEndDate.Value);
        }

        var invoiceRows = await invoiceQuery
            .Select(pair => new InvoiceRow(
                pair.plan.EngagementId ?? string.Empty,
                pair.item.SeqNo,
                pair.plan.NumInvoices,
                pair.item.EmissionDate,
                pair.item.DueDate,
                pair.item.Amount,
                pair.plan.PaymentTermDays,
                pair.item.PoNumber ?? string.Empty,
                pair.item.FrsNumber ?? string.Empty,
                pair.item.RitmNumber ?? string.Empty,
                pair.plan.CustomerFocalPointName ?? string.Empty,
                pair.plan.CustomerFocalPointEmail ?? string.Empty))
            .ToListAsync(cancellationToken);

        foreach (var row in invoiceRows)
        {
            if (!assignmentsByEngagementCode.TryGetValue(row.EngagementCode, out var assignments))
            {
                continue;
            }

            foreach (var assignment in assignments)
            {
                if (!managerBuilders.TryGetValue(assignment.ManagerId, out var builder))
                {
                    continue;
                }

                var dueDate = row.DueDate ?? (row.IssueDate.HasValue
                    ? row.IssueDate.Value.AddDays(row.PaymentTermDays)
                    : null);

                builder.Invoices.Add(new InvoiceEmailData
                {
                    EngagementCode = assignment.EngagementCode,
                    EngagementName = assignment.EngagementName,
                    CustomerName = assignment.CustomerName,
                    ParcelNumber = row.ParcelNumber,
                    TotalParcels = row.TotalParcels,
                    IssueDate = row.IssueDate,
                    DueDate = dueDate,
                    Amount = row.Amount,
                    Currency = assignment.EngagementCurrency,
                    PoNumber = row.PoNumber,
                    FrsNumber = row.FrsNumber,
                    RitmNumber = row.RitmNumber,
                    CustomerFocalPointName = row.CustomerFocalPointName,
                    CustomerFocalPointEmail = row.CustomerFocalPointEmail
                });
            }
        }
    }

    private static async Task LoadEtcDataAsync(
        ApplicationDbContext context,
        IReadOnlyDictionary<int, ManagerEmailDataBuilder> managerBuilders,
        IReadOnlyDictionary<int, List<AssignmentRow>> assignmentsByEngagementId,
        PowerAutomateJsonExportFilters filters,
        CancellationToken cancellationToken)
    {
        if (assignmentsByEngagementId.Count == 0)
        {
            return;
        }

        var engagementIds = assignmentsByEngagementId.Keys.ToArray();

        var etcRows = await (
            from budget in context.EngagementRankBudgets.AsNoTracking()
            join fiscalYear in context.FiscalYears.AsNoTracking() on budget.FiscalYearId equals fiscalYear.Id into fiscalYearJoin
            from fiscalYear in fiscalYearJoin.DefaultIfEmpty()
            where engagementIds.Contains(budget.EngagementId)
            select new EtcRow(
                budget.EngagementId,
                budget.RankName ?? string.Empty,
                budget.BudgetHours,
                budget.ConsumedHours,
                budget.AdditionalHours,
                budget.RemainingHours,
                budget.Status ?? string.Empty,
                fiscalYear != null ? fiscalYear.Name ?? string.Empty : string.Empty)
        ).ToListAsync(cancellationToken);

        if (filters.HasFiscalYearFilter)
        {
            var fiscalYearName = filters.FiscalYearName!;
            etcRows = etcRows
                .Where(row => string.Equals(row.FiscalYearName, fiscalYearName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        foreach (var row in etcRows)
        {
            if (!assignmentsByEngagementId.TryGetValue(row.EngagementId, out var assignments))
            {
                continue;
            }

            foreach (var assignment in assignments)
            {
                if (!managerBuilders.TryGetValue(assignment.ManagerId, out var builder))
                {
                    continue;
                }

                builder.Etcs.Add(new EtcEmailData
                {
                    EngagementCode = assignment.EngagementCode,
                    EngagementName = assignment.EngagementName,
                    CustomerName = assignment.CustomerName,
                    RankName = row.RankName,
                    BudgetHours = row.BudgetHours,
                    ConsumedHours = row.ConsumedHours,
                    AdditionalHours = row.AdditionalHours,
                    RemainingHours = row.RemainingHours,
                    Status = row.Status,
                    FiscalYearName = row.FiscalYearName,
                    LastEtcDate = assignment.LastEtcDate,
                    ProposedCompletionDate = assignment.ProposedNextEtcDate
                });
            }
        }
    }

    private sealed record AssignmentRow(
        int ManagerId,
        string ManagerName,
        string ManagerEmail,
        int EngagementDbId,
        string EngagementCode,
        string EngagementName,
        string EngagementCurrency,
        string CustomerName,
        DateTime? LastEtcDate,
        DateTime? ProposedNextEtcDate);

    private sealed record InvoiceRow(
        string EngagementCode,
        int ParcelNumber,
        int TotalParcels,
        DateTime? IssueDate,
        DateTime? DueDate,
        decimal Amount,
        int PaymentTermDays,
        string PoNumber,
        string FrsNumber,
        string RitmNumber,
        string CustomerFocalPointName,
        string CustomerFocalPointEmail);

    private sealed record EtcRow(
        int EngagementId,
        string RankName,
        decimal BudgetHours,
        decimal ConsumedHours,
        decimal AdditionalHours,
        decimal RemainingHours,
        string Status,
        string FiscalYearName);

    private sealed class ManagerEmailDataBuilder
    {
        private readonly List<InvoiceEmailData> _invoices = new();
        private readonly List<EtcEmailData> _etcs = new();

        public ManagerEmailDataBuilder(string managerName, string managerEmail)
        {
            ManagerName = managerName ?? string.Empty;
            ManagerEmail = managerEmail ?? string.Empty;
        }

        public string ManagerName { get; }

        public string ManagerEmail { get; }

        public List<InvoiceEmailData> Invoices => _invoices;

        public List<EtcEmailData> Etcs => _etcs;

        public ManagerEmailData Build(ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            var managerData = new ManagerEmailData
            {
                ManagerName = ManagerName,
                ManagerEmail = ManagerEmail,
                Invoices = _invoices.AsReadOnly(),
                Etcs = _etcs.AsReadOnly()
            };

            if (_invoices.Count == 0 && _etcs.Count == 0)
            {
                managerData.WarningBodyHtml = WarningBodyTemplateHtml;
                logger.LogWarning(
                    "No invoices or ETCs were found for manager {ManagerName} ({ManagerEmail}).",
                    ManagerName,
                    ManagerEmail);
            }

            return managerData;
        }
    }
}
