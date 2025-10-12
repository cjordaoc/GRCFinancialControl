using System;
using System.Data.Common;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class DatabaseSchemaInitializer : IDatabaseSchemaInitializer
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public DatabaseSchemaInitializer(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task EnsureSchemaAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var connection = context.Database.GetDbConnection();

            await connection.OpenAsync().ConfigureAwait(false);
            try
            {
                var discriminatorExists = await ColumnExistsAsync(connection, "ClosingPeriods", "Discriminator").ConfigureAwait(false);
                if (!discriminatorExists)
                {
                    await context.Database.ExecuteSqlRawAsync("ALTER TABLE `ClosingPeriods` ADD COLUMN `Discriminator` VARCHAR(64) NOT NULL DEFAULT 'ClosingPeriod';").ConfigureAwait(false);
                }

                await context.Database.ExecuteSqlRawAsync("UPDATE `ClosingPeriods` SET `Discriminator` = 'ClosingPeriod' WHERE `Discriminator` IS NULL OR `Discriminator` = ''; ").ConfigureAwait(false);

                if (await TableExistsAsync(connection, "FiscalYears").ConfigureAwait(false))
                {
                    await MigrateFiscalYearsAsync(context, connection).ConfigureAwait(false);
                }
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }

        private static async Task<bool> ColumnExistsAsync(DbConnection connection, string tableName, string columnName)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column;";

            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "@table";
            tableParameter.Value = tableName;
            command.Parameters.Add(tableParameter);

            var columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "@column";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);

            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return Convert.ToInt64(result) > 0;
        }

        private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table;";

            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "@table";
            tableParameter.Value = tableName;
            command.Parameters.Add(tableParameter);

            var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return Convert.ToInt64(result) > 0;
        }

        private static async Task MigrateFiscalYearsAsync(ApplicationDbContext context, DbConnection connection)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `Name`, `StartDate`, `EndDate` FROM `FiscalYears`;";
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var name = reader.GetString(0);
                var startDate = reader.GetDateTime(1);
                var endDate = reader.GetDateTime(2);

                var rowsAffected = await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE `ClosingPeriods` SET `Discriminator` = 'FiscalYear', `PeriodStart` = {startDate}, `PeriodEnd` = {endDate} WHERE `Name` = {name};").ConfigureAwait(false);

                if (rowsAffected == 0)
                {
                    await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO `ClosingPeriods` (`Name`, `PeriodStart`, `PeriodEnd`, `Discriminator`) VALUES ({name}, {startDate}, {endDate}, 'FiscalYear');").ConfigureAwait(false);
                }
            }
        }
    }
}
