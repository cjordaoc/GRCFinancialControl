using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Persistence.Services.Exporters.Json;

public sealed class ManagerEmailData
{
    public string ManagerName { get; init; } = string.Empty;
    public string ManagerEmail { get; init; } = string.Empty;
    public IReadOnlyList<InvoiceEmailData> Invoices { get; init; } = Array.Empty<InvoiceEmailData>();
    public IReadOnlyList<EtcEmailData> Etcs { get; init; } = Array.Empty<EtcEmailData>();
    public string? WarningBodyHtml { get; set; }

    public bool HasInvoices => Invoices.Count > 0;

    public bool HasEtcs => Etcs.Count > 0;
}

public sealed class InvoiceEmailData
{
    public string EngagementCode { get; init; } = string.Empty;
    public string EngagementName { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public int ParcelNumber { get; init; }
    public int TotalParcels { get; init; }
    public DateTime? IssueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string PoNumber { get; init; } = string.Empty;
    public string FrsNumber { get; init; } = string.Empty;
    public string RitmNumber { get; init; } = string.Empty;
    public string CustomerFocalPointName { get; init; } = string.Empty;
    public string CustomerFocalPointEmail { get; init; } = string.Empty;
}

public sealed class EtcEmailData
{
    public string EngagementCode { get; init; } = string.Empty;
    public string EngagementName { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string RankName { get; init; } = string.Empty;
    public decimal BudgetHours { get; init; }
    public decimal ConsumedHours { get; init; }
    public decimal AdditionalHours { get; init; }
    public decimal RemainingHours { get; init; }
    public string Status { get; init; } = string.Empty;
    public string FiscalYearName { get; init; } = string.Empty;
    public DateTime? LastEtcDate { get; init; }
    public DateTime? ProposedCompletionDate { get; init; }
}
