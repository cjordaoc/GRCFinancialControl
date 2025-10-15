using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GRCFinancialControl.Persistence.Services
{
    public class SettingsService : ISettingsService
    {
        private const string ConnectionSuccessfulMessage = "Connection successful.";
        private const string ConnectionFailedPrefix = "Connection failed: ";
        private const string MissingServerOrDatabaseMessage = "Server and database are required to test the connection.";

        private readonly SettingsDbContext _context;

        public SettingsService(SettingsDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _context = context;
        }

        public async Task<Dictionary<string, string>> GetAllAsync()
        {
            return await _context.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);
        }

        public async Task SaveAllAsync(Dictionary<string, string> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            foreach (var setting in settings)
            {
                if (string.IsNullOrWhiteSpace(setting.Key))
                {
                    throw new ArgumentException("Setting keys must be provided.", nameof(settings));
                }

                var existingSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == setting.Key);
                if (existingSetting != null)
                {
                    existingSetting.Value = setting.Value;
                }
                else
                {
                    await _context.Settings.AddAsync(new Setting { Key = setting.Key, Value = setting.Value });
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<ConnectionTestResult> TestConnectionAsync(string server, string database, string user, string password)
        {
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
            {
                return new ConnectionTestResult(false, MissingServerOrDatabaseMessage);
            }

            var builder = new MySqlConnectionStringBuilder
            {
                Server = server,
                Database = database,
                UserID = user,
                Password = password,
                SslMode = MySqlSslMode.Preferred,
                AllowUserVariables = true
            };

            try
            {
                await using var connection = new MySqlConnection(builder.ConnectionString);
                await connection.OpenAsync();
                return new ConnectionTestResult(true, ConnectionSuccessfulMessage);
            }
            catch (Exception ex)
            {
                return new ConnectionTestResult(false, string.Concat(ConnectionFailedPrefix, ex.Message));
            }
        }
    }
}