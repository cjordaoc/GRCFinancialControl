using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Lookups;
using GRC.Shared.Core.Enums;

namespace GRC.Shared.Core.Models.Lookups;

public sealed class EngagementLookup
{
    public int Id { get; init; }

    public string EngagementId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? CustomerName { get; init; }

    public string Currency { get; init; } = string.Empty;
}
