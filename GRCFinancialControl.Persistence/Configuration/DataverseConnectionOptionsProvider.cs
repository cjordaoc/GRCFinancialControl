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
            if (TryResolve(contextFactory, out var options, out var failureReason, settingsServiceFactory) && options is not null)
            {
                return options;
            }

            throw new InvalidOperationException(failureReason ?? "Configure Dataverse credentials in Settings before selecting the Dataverse backend.");
        }

        public static bool TryResolve(
            Func<SettingsDbContext> contextFactory,
            out DataverseConnectionOptions? options,
            out string? failureReason,
            Func<SettingsDbContext, ISettingsService>? settingsServiceFactory = null)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);

            if (DataverseConnectionOptions.TryFromEnvironment(out var environmentOptions) && environmentOptions is not null)
            {
                options = environmentOptions;
                failureReason = null;
                return true;
            }

            try
            {
                using var context = contextFactory();
                context.Database.EnsureCreated();

                var settingsService = settingsServiceFactory?.Invoke(context) ?? new SettingsService(context);
                ArgumentNullException.ThrowIfNull(settingsService);
                var storedSettings = settingsService.GetDataverseSettingsAsync().GetAwaiter().GetResult();
                options = DataverseConnectionOptions.FromSettings(storedSettings);
                failureReason = null;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                options = null;
                failureReason = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }
    }
}
