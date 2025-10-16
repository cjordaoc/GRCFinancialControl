using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.Services.Models;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.Services
{
    public class PowerBiEmbeddingService : IPowerBiEmbeddingService
    {
        private readonly ISettingsService _settingsService;

        public PowerBiEmbeddingService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<PowerBiEmbedConfiguration> GetConfigurationAsync()
        {
            var settings = await _settingsService.GetAllAsync();
            var configuration = BuildConfiguration(settings);

            return configuration;
        }

        private static PowerBiEmbedConfiguration BuildConfiguration(Dictionary<string, string> settings)
        {
            settings.TryGetValue(SettingKeys.PowerBiEmbedUrl, out var embedUrl);
            settings.TryGetValue(SettingKeys.PowerBiWorkspaceId, out var workspaceId);
            settings.TryGetValue(SettingKeys.PowerBiReportId, out var reportId);
            settings.TryGetValue(SettingKeys.PowerBiEmbedToken, out var embedToken);

            var statusMessage = string.Empty;
            Uri? dashboardUri = null;

            if (!string.IsNullOrWhiteSpace(embedUrl) && Uri.TryCreate(embedUrl, UriKind.Absolute, out var parsedUri))
            {
                dashboardUri = parsedUri;
            }
            else if (!string.IsNullOrWhiteSpace(workspaceId) && !string.IsNullOrWhiteSpace(reportId))
            {
                var builder = new UriBuilder("https://app.powerbi.com/reportEmbed")
                {
                    Query = $"reportId={reportId}&groupId={workspaceId}"
                };
                dashboardUri = builder.Uri;
                statusMessage = "Embed URL assembled from workspace and report identifiers. Ensure authentication is configured.";
            }
            else
            {
                statusMessage = "Add a Publish to Web URL or both workspace and report identifiers in Settings.";
            }

            return new PowerBiEmbedConfiguration
            {
                DashboardUri = dashboardUri,
                EmbedToken = string.IsNullOrWhiteSpace(embedToken) ? null : embedToken,
                RequiresAuthentication = !string.IsNullOrWhiteSpace(workspaceId) && !string.IsNullOrWhiteSpace(reportId) && string.IsNullOrWhiteSpace(embedUrl),
                StatusMessage = statusMessage
            };
        }
    }
}
