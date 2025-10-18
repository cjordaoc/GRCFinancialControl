using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using MySqlConnector;

namespace GRCFinancialControl.Persistence.Services
{
    public class DatabaseSchemaInitializer : IDatabaseSchemaInitializer
    {
        private const string DisableForeignKeyChecksSql = "SET FOREIGN_KEY_CHECKS = 0;";
        private const string EnableForeignKeyChecksSql = "SET FOREIGN_KEY_CHECKS = 1;";

        private static readonly string[] DeleteStatements =
        {
            "DELETE FROM `ActualsEntries`;",
            "DELETE FROM `PlannedAllocations`;",
            "DELETE FROM `EngagementFiscalYearAllocations`;",
            "DELETE FROM `EngagementFiscalYearRevenueAllocations`;",
            "DELETE FROM `EngagementRankBudgets`;",
            "DELETE FROM `FinancialEvolution`;",
            "DELETE FROM `EngagementManagerAssignments`;",
            "DELETE FROM `EngagementPapds`;",
            "DELETE FROM `Exceptions`;",
            "DELETE FROM `Engagements`;",
            "DELETE FROM `Customers`;",
            "DELETE FROM `Managers`;",
            "DELETE FROM `Papds`;",
            "DELETE FROM `ClosingPeriods`;",
            "DELETE FROM `FiscalYears`;"
        };

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ISettingsService _settingsService;

        public DatabaseSchemaInitializer(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ISettingsService settingsService)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            ArgumentNullException.ThrowIfNull(settingsService);

            _contextFactory = contextFactory;
            _settingsService = settingsService;
        }

        public async Task EnsureSchemaAsync()
        {
            await ExecuteWithContextAsync(context => context.Database.EnsureCreatedAsync()).ConfigureAwait(false);
        }

        public async Task ClearAllDataAsync()
        {
            await ExecuteWithContextAsync(async context =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);

                await context.Database.ExecuteSqlRawAsync(DisableForeignKeyChecksSql).ConfigureAwait(false);

                try
                {
                    foreach (var statement in DeleteStatements)
                    {
                        await context.Database.ExecuteSqlRawAsync(statement).ConfigureAwait(false);
                    }

                    await context.Database.ExecuteSqlRawAsync(EnableForeignKeyChecksSql).ConfigureAwait(false);
                    await transaction.CommitAsync().ConfigureAwait(false);
                }
                catch
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    await context.Database.ExecuteSqlRawAsync(EnableForeignKeyChecksSql).ConfigureAwait(false);
                    throw;
                }
            }).ConfigureAwait(false);
        }

        private async Task ExecuteWithContextAsync(Func<ApplicationDbContext, Task> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                ArgumentNullException.ThrowIfNull(context);
                await action(context).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (IsProviderMissing(ex))
            {
                await using var context = await CreateContextFromSettingsAsync().ConfigureAwait(false);
                await action(context).ConfigureAwait(false);
            }
        }

        private async Task<ApplicationDbContext> CreateContextFromSettingsAsync()
        {
            var settings = await _settingsService.GetAllAsync().ConfigureAwait(false);

            if (!TryBuildConnectionString(settings, out var connectionString))
            {
                throw new InvalidOperationException("Connection settings are incomplete. Import the connection package again.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 29)),
                options => options.EnableRetryOnFailure());

            return new ApplicationDbContext(optionsBuilder.Options);
        }

        private static bool TryBuildConnectionString(
            IReadOnlyDictionary<string, string> settings,
            out string connectionString)
        {
            connectionString = string.Empty;

            if (!settings.TryGetValue(SettingKeys.Server, out var server) || string.IsNullOrWhiteSpace(server))
            {
                return false;
            }

            if (!settings.TryGetValue(SettingKeys.Database, out var database) || string.IsNullOrWhiteSpace(database))
            {
                return false;
            }

            if (!settings.TryGetValue(SettingKeys.User, out var user) || string.IsNullOrWhiteSpace(user))
            {
                return false;
            }

            if (!settings.TryGetValue(SettingKeys.Password, out var password) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var builder = new MySqlConnectionStringBuilder
            {
                Server = server,
                Database = database,
                UserID = user,
                Password = password,
                SslMode = MySqlSslMode.Preferred,
                AllowUserVariables = true,
                ConnectionTimeout = 5
            };

            connectionString = builder.ConnectionString;
            return true;
        }

        private static bool IsProviderMissing(InvalidOperationException exception)
        {
            return exception.Message.Contains("No database provider has been configured", StringComparison.OrdinalIgnoreCase);
        }
    }
}
