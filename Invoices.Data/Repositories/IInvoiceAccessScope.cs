using System.Collections.Generic;

namespace Invoices.Data.Repositories;

public interface IInvoiceAccessScope
{
    string? Login { get; }

    IReadOnlySet<string> EngagementIds { get; }

    bool HasAssignments { get; }

    bool IsInitialized { get; }

    string? InitializationError { get; }

    void EnsureInitialized();

    bool IsEngagementAllowed(string? engagementId);
}
