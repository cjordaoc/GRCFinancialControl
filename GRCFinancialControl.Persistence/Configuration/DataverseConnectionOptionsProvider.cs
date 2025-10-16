using System;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Dataverse;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using GRCFinancialControl.Persistence;

namespace GRCFinancialControl.Persistence.Configuration
{
    public static class DataverseConnectionOptionsProvider
    {
        public static DataverseConnectionOptions Resolve(Func<SettingsDbContext> contextFactory, Func<SettingsDbContext, ISettingsService>? settingsServiceFactory = null)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);

            if (DataverseConnectionOptions.TryFromEnvironment(out var environmentOptions) && environmentOptions is not null)
            {
                return environmentOptions;
            }

            try
            {
                using var context = contextFactory();
                context.Database.EnsureCreated();

                var settingsService = settingsServiceFactory?.Invoke(context) ?? new SettingsService(context);
                ArgumentNullException.ThrowIfNull(settingsService);
                var storedSettings = settingsService.GetDataverseSettingsAsync().GetAwaiter().GetResult();
                return DataverseConnectionOptions.FromSettings(storedSettings);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("Configure Dataverse credentials in Settings before selecting the Dataverse backend.", ex);
            }
        }
    }
}
