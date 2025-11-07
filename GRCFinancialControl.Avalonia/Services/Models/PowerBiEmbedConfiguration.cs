using System;

namespace GRCFinancialControl.Avalonia.Services.Models
{
    /// <summary>
    /// Represents Power BI dashboard embed configuration with URI and status.
    /// </summary>
    public sealed class PowerBiEmbedConfiguration
    {
        public static PowerBiEmbedConfiguration Empty { get; } = new();

        public Uri? DashboardUri { get; init; }

        public string? StatusMessage { get; init; }

        public bool HasDashboard => DashboardUri is not null;
    }
}
