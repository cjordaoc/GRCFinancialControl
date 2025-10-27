namespace Invoices.Core.Models;

public sealed class EngagementLookup
{
    public int Id { get; init; }

    public string EngagementId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? CustomerName { get; init; }

    public string Currency { get; init; } = string.Empty;
}
