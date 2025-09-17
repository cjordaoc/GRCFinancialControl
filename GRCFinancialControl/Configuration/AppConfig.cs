using System;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

using GRCFinancialControl.Data;

namespace GRCFinancialControl.Configuration

{
    public sealed class AppConfig
    {
        public string Server { get; set; } = string.Empty;
        public uint Port { get; set; } = 3306;
        public string Database { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseSsl { get; set; } = true;

        public string BuildConnectionString()
        {
            if (string.IsNullOrWhiteSpace(Server))
            {
                throw new InvalidOperationException("Server is required.");
            }

            if (string.IsNullOrWhiteSpace(Database))
            {
                throw new InvalidOperationException("Database is required.");
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                throw new InvalidOperationException("Username is required.");
            }

            var builder = new MySqlConnectionStringBuilder
            {
                Server = Server,
                Port = Port,
                Database = Database,
                UserID = Username,
                Password = Password,
                SslMode = UseSsl ? MySqlSslMode.Preferred : MySqlSslMode.Disabled,
                AllowUserVariables = true,
                CharacterSet = "utf8mb4",
                ConnectionTimeout = 30,
                DefaultCommandTimeout = 180
            };

            return builder.ConnectionString;
        }
    }

    public static class DbContextFactory
    {
        public static AppDbContext Create(AppConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var connectionString = config.BuildConnectionString();
            var serverVersion = MySqlServerVersion.LatestSupportedServerVersion;

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseMySql(connectionString, serverVersion, mysqlOptions =>
            {
                mysqlOptions.CommandTimeout(180);
            });
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.EnableSensitiveDataLogging(false);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
