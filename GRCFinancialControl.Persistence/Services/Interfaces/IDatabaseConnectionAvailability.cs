using System.Threading;

namespace GRCFinancialControl.Persistence.Services.Interfaces;

public interface IDatabaseConnectionAvailability
{
    bool IsConfigured { get; }

    string? ErrorMessage { get; }

    void Update(bool isConfigured, string? errorMessage = null);
}
