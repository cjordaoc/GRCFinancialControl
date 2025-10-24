using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Provides persistence and connection validation for application settings stored locally.
    /// </summary>
    public sealed class SettingsService : ISettingsService
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

        /// <summary>
        /// Retrieves all persisted settings as a dictionary keyed by setting name.
        /// </summary>
        /// <returns>A dictionary containing the stored settings.</returns>
        public Dictionary<string, string> GetAll()
        {
            return _context.Settings
                .AsNoTracking()
                .ToDictionary(s => s.Key, s => s.Value);
        }

        /// <summary>
        /// Retrieves all persisted settings as a dictionary keyed by setting name.
        /// </summary>
        /// <returns>A dictionary containing the stored settings.</returns>
        public async Task<Dictionary<string, string>> GetAllAsync()
        {
            return await _context.Settings
                .AsNoTracking()
                .ToDictionaryAsync(s => s.Key, s => s.Value)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Persists the provided settings, updating, adding, or removing records as needed.
        /// </summary>
        /// <param name="settings">The settings to persist.</param>
        public async Task SaveAllAsync(Dictionary<string, string> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            foreach (var setting in settings)
            {
                if (string.IsNullOrWhiteSpace(setting.Key))
                {
                    throw new ArgumentException("Setting keys must be provided.", nameof(settings));
                }
            }

            var existingSettings = await _context.Settings
                .ToListAsync()
                .ConfigureAwait(false);

            var existingMap = existingSettings.ToDictionary(s => s.Key, StringComparer.Ordinal);
            var settingsToAdd = new List<Setting>();

            foreach (var (key, value) in settings)
            {
                if (existingMap.TryGetValue(key, out var existing))
                {
                    existing.Value = value;
                    existingMap.Remove(key);
                    continue;
                }

                settingsToAdd.Add(new Setting { Key = key, Value = value });
            }

            if (settingsToAdd.Count > 0)
            {
                await _context.Settings.AddRangeAsync(settingsToAdd).ConfigureAwait(false);
            }

            if (existingMap.Count > 0)
            {
                foreach (var obsolete in existingMap.Values)
                {
                    _context.Settings.Remove(obsolete);
                }
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to open a connection to the configured MySQL instance.
        /// </summary>
        /// <param name="server">MySQL server address.</param>
        /// <param name="database">Database name to connect to.</param>
        /// <param name="user">Username used for the connection.</param>
        /// <param name="password">Password used for the connection.</param>
        /// <returns>The outcome of the connectivity test.</returns>
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
                await connection.OpenAsync().ConfigureAwait(false);
                return new ConnectionTestResult(true, ConnectionSuccessfulMessage);
            }
            catch (Exception ex)
            {
                return new ConnectionTestResult(false, string.Concat(ConnectionFailedPrefix, ex.Message));
            }
        }

        /// <summary>
        /// Gets the identifier of the default fiscal year configured in settings.
        /// </summary>
        /// <returns>The default fiscal year identifier, or <c>null</c> when not configured.</returns>
        public async Task<int?> GetDefaultFiscalYearIdAsync()
        {
            var setting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == SettingKeys.DefaultFiscalYearId)
                .ConfigureAwait(false);
            if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            {
                return null;
            }

            if (int.TryParse(setting.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fiscalYearId))
            {
                return fiscalYearId;
            }

            return null;
        }

        /// <summary>
        /// Persists the identifier of the default fiscal year.
        /// </summary>
        /// <param name="fiscalYearId">The fiscal year identifier to persist, or <c>null</c> to clear it.</param>
        public async Task SetDefaultFiscalYearIdAsync(int? fiscalYearId)
        {
            var existingSetting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == SettingKeys.DefaultFiscalYearId)
                .ConfigureAwait(false);

            if (fiscalYearId.HasValue)
            {
                var value = fiscalYearId.Value.ToString(CultureInfo.InvariantCulture);
                if (existingSetting != null)
                {
                    existingSetting.Value = value;
                }
                else
                {
                    await _context.Settings.AddAsync(new Setting
                    {
                        Key = SettingKeys.DefaultFiscalYearId,
                        Value = value
                    }).ConfigureAwait(false);
                }
            }
            else if (existingSetting != null)
            {
                _context.Settings.Remove(existingSetting);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the identifier of the default closing period configured in settings.
        /// </summary>
        /// <returns>The default closing period identifier, or <c>null</c> when not configured.</returns>
        public async Task<int?> GetDefaultClosingPeriodIdAsync()
        {
            var setting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == SettingKeys.DefaultClosingPeriodId)
                .ConfigureAwait(false);

            if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            {
                return null;
            }

            if (int.TryParse(setting.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var closingPeriodId))
            {
                return closingPeriodId;
            }

            return null;
        }

        /// <summary>
        /// Persists the identifier of the default closing period.
        /// </summary>
        /// <param name="closingPeriodId">The closing period identifier to persist, or <c>null</c> to clear it.</param>
        public async Task SetDefaultClosingPeriodIdAsync(int? closingPeriodId)
        {
            var existingSetting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == SettingKeys.DefaultClosingPeriodId)
                .ConfigureAwait(false);

            if (closingPeriodId.HasValue)
            {
                var value = closingPeriodId.Value.ToString(CultureInfo.InvariantCulture);
                if (existingSetting != null)
                {
                    existingSetting.Value = value;
                }
                else
                {
                    await _context.Settings.AddAsync(new Setting
                    {
                        Key = SettingKeys.DefaultClosingPeriodId,
                        Value = value
                    }).ConfigureAwait(false);
                }
            }
            else if (existingSetting != null)
            {
                _context.Settings.Remove(existingSetting);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

    }
}
