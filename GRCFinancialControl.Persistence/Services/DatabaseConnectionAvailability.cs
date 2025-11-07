using System.Threading;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Persistence.Services;

/// <summary>
/// Tracks database connection configuration status with thread-safe updates.
/// </summary>
public sealed class DatabaseConnectionAvailability : IDatabaseConnectionAvailability
{
    private const string DefaultErrorMessage = "Connection settings are missing or incomplete.";

    private volatile bool _isConfigured;
    private string? _errorMessage;

    public DatabaseConnectionAvailability(bool isConfigured, string? errorMessage = null)
    {
        Update(isConfigured, errorMessage);
    }

    public bool IsConfigured => _isConfigured;

    public string? ErrorMessage => Volatile.Read(ref _errorMessage);

    public void Update(bool isConfigured, string? errorMessage = null)
    {
        _isConfigured = isConfigured;
        var normalized = string.IsNullOrWhiteSpace(errorMessage)
            ? (isConfigured ? null : DefaultErrorMessage)
            : errorMessage.Trim();
        Volatile.Write(ref _errorMessage, normalized);
    }
}
