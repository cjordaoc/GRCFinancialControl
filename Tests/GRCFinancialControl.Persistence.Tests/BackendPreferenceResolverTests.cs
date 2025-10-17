using System;
using System.IO;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Configuration;
using GRCFinancialControl.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GRCFinancialControl.Persistence.Tests;

public sealed class BackendPreferenceResolverTests : IDisposable
{
    private readonly SettingsDatabaseHarness _harness = new();

    [Fact]
    public void Resolve_ReturnsDefaultBackendWhenNoPreferenceIsStored()
    {
        var backend = BackendPreferenceResolver.Resolve(_harness.CreateContext);

        Assert.Equal(DataBackend.MySql, backend);
    }

    [Fact]
    public async Task Resolve_ReturnsPreferenceStoredBySettingsService()
    {
        await using (var context = _harness.CreateContext())
        {
            var service = new SettingsService(context);
            await service.SetBackendPreferenceAsync(DataBackend.Dataverse);
        }

        var backend = BackendPreferenceResolver.Resolve(_harness.CreateContext);

        Assert.Equal(DataBackend.Dataverse, backend);
    }

    [Fact]
    public async Task Resolve_PrefersEnvironmentVariableOverStoredPreference()
    {
        await using (var context = _harness.CreateContext())
        {
            var service = new SettingsService(context);
            await service.SetBackendPreferenceAsync(DataBackend.MySql);
        }

        var originalValue = Environment.GetEnvironmentVariable(DataBackendConfiguration.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(DataBackendConfiguration.EnvironmentVariableName, DataBackend.Dataverse.ToString());

        try
        {
            var backend = BackendPreferenceResolver.Resolve(_harness.CreateContext, defaultBackend: DataBackend.MySql);

            Assert.Equal(DataBackend.Dataverse, backend);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DataBackendConfiguration.EnvironmentVariableName, originalValue);
        }
    }

    public void Dispose()
    {
        _harness.Dispose();
    }

    private sealed class SettingsDatabaseHarness : IDisposable
    {
        private readonly string _databasePath;
        private readonly DbContextOptions<SettingsDbContext> _options;

        public SettingsDatabaseHarness()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"settings-tests-{Guid.NewGuid():N}.db");
            var connectionString = $"Data Source={_databasePath}";

            _options = new DbContextOptionsBuilder<SettingsDbContext>()
                .UseSqlite(connectionString)
                .Options;

            using var context = new SettingsDbContext(_options);
            context.Database.EnsureCreated();
        }

        public SettingsDbContext CreateContext()
        {
            return new SettingsDbContext(_options);
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }
            }
            catch
            {
                // Tests should not fail because the temporary database could not be cleaned up.
            }
        }
    }
}
