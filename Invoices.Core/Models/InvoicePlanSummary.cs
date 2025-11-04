using System;
using Invoices.Core.Enums;

namespace Invoices.Core.Models;

public sealed class InvoicePlanSummary
{
    public int Id { get; init; }

    public string EngagementId { get; init; } = string.Empty;

    public string EngagementDescription { get; init; } = string.Empty;

    public string? CustomerName { get; init; }

    public InvoicePlanType Type { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? FirstEmissionDate { get; init; }

    public int PlannedItemCount { get; init; }

    public int RequestedItemCount { get; init; }

    public int EmittedItemCount { get; init; }

    public int ClosedItemCount { get; init; }

    public int CanceledItemCount { get; init; }
}
