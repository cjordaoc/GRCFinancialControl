using System.Collections.Generic;

namespace GRCFinancialControl.Persistence.Services.Interfaces;

public interface IPersonDirectory
{
    string? TryGetDisplayName(string identifier);

    IReadOnlyDictionary<string, string> TryResolveDisplayNames(IEnumerable<string> identifiers);
}
