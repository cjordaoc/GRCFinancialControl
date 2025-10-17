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

            var statusMessage = string.Empty;
            Uri? dashboardUri = null;

            if (!string.IsNullOrWhiteSpace(embedUrl))
            {
                if (Uri.TryCreate(embedUrl, UriKind.Absolute, out var parsedUri))
                {
                    dashboardUri = parsedUri;
                    statusMessage = "Dashboard ready.";
                }
                else
                {
                    statusMessage = "The configured Publish to Web URL is invalid.";
                }
            }
            else
            {
                statusMessage = "Add a Publish to Web URL in Settings to view the dashboard.";
            }

            return new PowerBiEmbedConfiguration
            {
                DashboardUri = dashboardUri,
                StatusMessage = statusMessage
            };
        }
    }
}
