using System.Collections.Generic;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Persistence.Services.People;

/// <summary>
/// No-op implementation of IPersonDirectory for scenarios without person resolution.
/// </summary>
public sealed class NullPersonDirectory : IPersonDirectory
{
    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    public string? TryGetDisplayName(string identifier)
    {
        return null;
    }

    public IReadOnlyDictionary<string, string> TryResolveDisplayNames(IEnumerable<string> identifiers)
    {
        return Empty;
    }
}
