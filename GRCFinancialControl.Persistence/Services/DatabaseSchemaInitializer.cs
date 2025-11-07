using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using MySqlConnector;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Ensures database schema creation and handles legacy table migrations.
    /// </summary>
    public class DatabaseSchemaInitializer : IDatabaseSchemaInitializer
    {
        private const string DisableForeignKeyChecksSql = "SET FOREIGN_KEY_CHECKS = 0;";
        private const string EnableForeignKeyChecksSql = "SET FOREIGN_KEY_CHECKS = 1;";

        private static readonly string[] TablesToClear =
        {
            "ActualsEntries",
            "PlannedAllocations",
            "EngagementFiscalYearRevenueAllocations",
            "EngagementRankBudgets",
            "FinancialEvolution",
            "Exceptions"
        };

        private const string TableExistsSql =
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName;";

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
            await ExecuteWithContextAsync(async context =>
            {
                await context.Database.EnsureCreatedAsync().ConfigureAwait(false);

                if (!IsMySqlProvider(context))
                {
                    return;
                }

                var connection = context.Database.GetDbConnection();
                var shouldCloseConnection = connection.State != ConnectionState.Open;

                if (shouldCloseConnection)
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                }

                try
                {
                    await EnsurePlannedAllocationsTableAsync(context, connection).ConfigureAwait(false);
                    await DropLegacyBudgetTriggersAsync(context).ConfigureAwait(false);
                }
                finally
                {
                    if (shouldCloseConnection)
                    {
                        await connection.CloseAsync().ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        public async Task ClearAllDataAsync()
        {
            await ExecuteWithContextAsync(async context =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync().ConfigureAwait(false);

                await context.Database.ExecuteSqlRawAsync(DisableForeignKeyChecksSql).ConfigureAwait(false);

                try
                {
                    var connection = context.Database.GetDbConnection();

                    foreach (var tableName in TablesToClear)
                    {
                        await DeleteAllRowsIfTableExistsAsync(context, connection, tableName).ConfigureAwait(false);
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

        private static async Task DeleteAllRowsIfTableExistsAsync(
            ApplicationDbContext context,
            DbConnection connection,
            string tableName)
        {
            if (!await TableExistsAsync(connection, tableName).ConfigureAwait(false))
            {
                return;
            }

            var statement = $"DELETE FROM `{tableName}`;";
            await context.Database.ExecuteSqlRawAsync(statement).ConfigureAwait(false);
        }

        private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = TableExistsSql;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            var count = Convert.ToInt32(result, CultureInfo.InvariantCulture);

            return count > 0;
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

        private static bool IsMySqlProvider(ApplicationDbContext context)
        {
            return context.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static async Task EnsurePlannedAllocationsTableAsync(
            ApplicationDbContext context,
            DbConnection connection)
        {
            if (await TableExistsAsync(connection, "PlannedAllocations").ConfigureAwait(false))
            {
                return;
            }

            const string createSql = @"
CREATE TABLE `PlannedAllocations` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `EngagementId` INT NOT NULL,
    `ClosingPeriodId` INT NOT NULL,
    `AllocatedHours` DECIMAL(18, 2) NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UX_PlannedAllocations_EngagementPeriod` (`EngagementId`, `ClosingPeriodId`),
    KEY `IX_PlannedAllocations_ClosingPeriodId` (`ClosingPeriodId`),
    CONSTRAINT `FK_PlannedAllocations_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_PlannedAllocations_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

            await context.Database.ExecuteSqlRawAsync(createSql).ConfigureAwait(false);
        }

        private static async Task DropLegacyBudgetTriggersAsync(ApplicationDbContext context)
        {
            const string dropInsertTrigger = "DROP TRIGGER IF EXISTS `trg_EngagementRankBudgets_bi`;";
            const string dropUpdateTrigger = "DROP TRIGGER IF EXISTS `trg_EngagementRankBudgets_bu`;";

            await context.Database.ExecuteSqlRawAsync(dropInsertTrigger).ConfigureAwait(false);
            await context.Database.ExecuteSqlRawAsync(dropUpdateTrigger).ConfigureAwait(false);
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
