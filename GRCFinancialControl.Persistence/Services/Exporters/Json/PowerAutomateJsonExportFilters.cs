using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Persistence.Services.Exporters.Json;

public sealed class PowerAutomateJsonExportFilters
{
    public static PowerAutomateJsonExportFilters Empty => new();

    public DateTime? InvoiceStartDate { get; init; }

    public DateTime? InvoiceEndDate { get; init; }

    public string? FiscalYearName { get; init; }

    public IReadOnlyCollection<string>? ManagerEmails { get; init; }

    public bool HasManagerEmailFilter => ManagerEmails is { Count: > 0 };

    public bool HasInvoiceDateFilter => InvoiceStartDate.HasValue || InvoiceEndDate.HasValue;

    public bool HasFiscalYearFilter => !string.IsNullOrWhiteSpace(FiscalYearName);
}
