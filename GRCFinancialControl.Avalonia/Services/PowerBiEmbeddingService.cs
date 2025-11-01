using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Presentation.Localization;
using GRCFinancialControl.Avalonia.Services.Models;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.Services
{
    public class PowerBiEmbeddingService
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
                    statusMessage = LocalizationRegistry.Get("FINC_Reports_Status_DashboardReady");
                }
                else
                {
                    statusMessage = LocalizationRegistry.Get("FINC_Reports_Status_InvalidUrl");
                }
            }
            else
            {
                statusMessage = LocalizationRegistry.Get("FINC_Reports_Status_UrlRequired");
            }

            return new PowerBiEmbedConfiguration
            {
                DashboardUri = dashboardUri,
                StatusMessage = statusMessage
            };
        }
    }
}
