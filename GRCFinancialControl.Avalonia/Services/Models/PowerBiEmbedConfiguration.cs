using System;

namespace GRCFinancialControl.Avalonia.Services.Models
{
    public sealed class PowerBiEmbedConfiguration
    {
        public static PowerBiEmbedConfiguration Empty { get; } = new();

        public Uri? DashboardUri { get; init; }

        public string? EmbedToken { get; init; }

        public bool RequiresAuthentication { get; init; }

        public string? StatusMessage { get; init; }

        public bool HasDashboard => DashboardUri is not null;
    }
}
