using System;
using System.Diagnostics;
using System.Linq;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using Microsoft.EntityFrameworkCore;
using GRCFinancialControl.Persistence;

namespace GRCFinancialControl.Persistence.Configuration
{
    public static class BackendPreferenceResolver
    {
        public static DataBackend Resolve(Func<SettingsDbContext> contextFactory, DataBackend defaultBackend = DataBackend.MySql, string? environmentVariableName = null)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);

            var resolvedEnvironmentVariableName = string.IsNullOrWhiteSpace(environmentVariableName)
                ? DataBackendConfiguration.EnvironmentVariableName
                : environmentVariableName;

            var environmentValue = Environment.GetEnvironmentVariable(resolvedEnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(environmentValue) && Enum.TryParse(environmentValue, ignoreCase: true, out DataBackend backendFromEnvironment))
            {
                return backendFromEnvironment;
            }

            try
            {
                using var context = contextFactory();
                context.Database.EnsureCreated();

                var storedValue = context.Settings.AsNoTracking()
                    .FirstOrDefault(s => s.Key == SettingKeys.DataBackendPreference)?.Value;

                if (!string.IsNullOrWhiteSpace(storedValue) && Enum.TryParse(storedValue, ignoreCase: true, out DataBackend storedBackend))
                {
                    return storedBackend;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
            {
                Trace.TraceWarning("Failed to resolve backend preference from settings. Falling back to default. Exception: {0}", ex);
            }

            return defaultBackend;
        }
    }
}
