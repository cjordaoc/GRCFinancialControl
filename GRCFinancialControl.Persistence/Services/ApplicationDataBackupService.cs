using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;

namespace GRCFinancialControl.Persistence.Services
{
    public sealed class ApplicationDataBackupService : IApplicationDataBackupService
    {
        private const string DisableForeignKeyChecksSql = "SET FOREIGN_KEY_CHECKS = 0;";
        private const string EnableForeignKeyChecksSql = "SET FOREIGN_KEY_CHECKS = 1;";
        private static readonly XName RootElementName = "GRCFinancialControlData";
        private static readonly XName TableElementName = "Table";
        private static readonly XName RowElementName = "Row";
        private static readonly XName ColumnElementName = "Column";

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ISettingsService _settingsService;

        public ApplicationDataBackupService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ISettingsService settingsService)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public async Task ExportAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A destination file path is required.", nameof(filePath));
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            await using var context = await CreateContextAsync().ConfigureAwait(false);
            await using var connection = context.Database.GetDbConnection();
            await connection.OpenAsync().ConfigureAwait(false);

            var tableNames = GetOrderedTableNames(context.Model);
            var root = new XElement(
                RootElementName,
                new XAttribute("exportedAtUtc", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture)));

            foreach (var tableName in tableNames)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM `{tableName}`;";

                await using (var reader = await command
                    .ExecuteReaderAsync(CommandBehavior.CloseConnection)
                    .ConfigureAwait(false))
                {
                    var tableElement = new XElement(TableElementName, new XAttribute("name", tableName));

                    var fieldTypes = new Type[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        fieldTypes[i] = reader.GetFieldType(i);
                    }

                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var rowElement = new XElement(RowElementName);

                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var columnName = reader.GetName(i);
                            var columnElement = new XElement(ColumnElementName);
                            columnElement.SetAttributeValue("name", columnName);

                            var columnType = fieldTypes[i] ?? typeof(string);
                            columnElement.SetAttributeValue("type", columnType.AssemblyQualifiedName ?? columnType.FullName);

                            if (reader.IsDBNull(i))
                            {
                                columnElement.SetAttributeValue("isNull", true);
                            }
                            else
                            {
                                var value = reader.GetValue(i);
                                columnElement.Value = SerializeValue(value, columnType);
                            }

                            rowElement.Add(columnElement);
                        }

                        tableElement.Add(rowElement);
                    }

                    root.Add(tableElement);
                }

                await connection.OpenAsync().ConfigureAwait(false);
            }

            var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            using var stream = File.Create(filePath);
            using var writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Async = false
            });
            document.WriteTo(writer);
            writer.Flush();
        }

        public async Task ImportAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A backup file path is required.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("The specified backup file does not exist.", filePath);
            }

            var document = XDocument.Load(filePath, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var root = document.Root;
            if (root is null || root.Name != RootElementName)
            {
                throw new InvalidDataException("The backup file is invalid or corrupted.");
            }

            await using var context = await CreateContextAsync().ConfigureAwait(false);
            await using var connection = context.Database.GetDbConnection();
            await connection.OpenAsync().ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);

            var foreignKeysDisabled = false;

            try
            {
                await ExecuteNonQueryAsync(connection, transaction, DisableForeignKeyChecksSql).ConfigureAwait(false);
                foreignKeysDisabled = true;

                var tableNames = GetOrderedTableNames(context.Model);
                var tableElements = root
                    .Elements(TableElementName)
                    .ToDictionary(
                        element => element.Attribute("name")?.Value ?? string.Empty,
                        element => element,
                        StringComparer.OrdinalIgnoreCase);

                foreach (var tableName in tableNames)
                {
                    await ExecuteNonQueryAsync(connection, transaction, $"DELETE FROM `{tableName}`;").ConfigureAwait(false);

                    if (!tableElements.TryGetValue(tableName, out var tableElement))
                    {
                        continue;
                    }

                    foreach (var rowElement in tableElement.Elements(RowElementName))
                    {
                        var columns = rowElement
                            .Elements(ColumnElementName)
                            .Select(column => new ColumnValue(
                                column.Attribute("name")?.Value ?? string.Empty,
                                column.Attribute("type")?.Value,
                                column.Attribute("isNull")?.Value,
                                column.Value))
                            .Where(column => !string.IsNullOrWhiteSpace(column.Name))
                            .ToList();

                        if (columns.Count == 0)
                        {
                            continue;
                        }

                        var columnNames = string.Join(", ", columns.Select(c => $"`{c.Name}`"));
                        var parameterNames = string.Join(", ", columns.Select((_, index) => $"@p{index}"));

                        await using var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = $"INSERT INTO `{tableName}` ({columnNames}) VALUES ({parameterNames});";

                        for (var i = 0; i < columns.Count; i++)
                        {
                            var column = columns[i];
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = $"@p{i}";

                            var value = DeserializeValue(column);
                            if (value is DBNull)
                            {
                                parameter.Value = DBNull.Value;
                            }
                            else
                            {
                                parameter.Value = value ?? DBNull.Value;
                                var dbType = GetDbType(value?.GetType());
                                if (dbType.HasValue)
                                {
                                    parameter.DbType = dbType.Value;
                                }
                            }

                            command.Parameters.Add(parameter);
                        }

                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }

                await ExecuteNonQueryAsync(connection, transaction, EnableForeignKeyChecksSql).ConfigureAwait(false);
                foreignKeysDisabled = false;
                await transaction.CommitAsync().ConfigureAwait(false);
            }
            catch
            {
                if (foreignKeysDisabled)
                {
                    await ExecuteNonQueryAsync(connection, transaction: null, EnableForeignKeyChecksSql).ConfigureAwait(false);
                }

                await transaction.RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }

        private async Task<ApplicationDbContext> CreateContextAsync()
        {
            try
            {
                return await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (IsProviderMissing(ex))
            {
                return await CreateContextFromSettingsAsync().ConfigureAwait(false);
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

        private static async Task ExecuteNonQueryAsync(
            System.Data.Common.DbConnection connection,
            System.Data.Common.DbTransaction? transaction,
            string commandText)
        {
            await using var command = connection.CreateCommand();
            if (transaction is not null)
            {
                command.Transaction = transaction;
            }
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static IReadOnlyList<string> GetOrderedTableNames(IModel model)
        {
            return model
                .GetEntityTypes()
                .Select(entity => entity.GetTableName())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string SerializeValue(object value, Type columnType)
        {
            switch (value)
            {
                case byte[] bytes:
                    return Convert.ToBase64String(bytes);
                case DateTime dateTime:
                    var unspecified = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
                    return XmlConvert.ToString(unspecified, XmlDateTimeSerializationMode.Unspecified);
                case DateTimeOffset dateTimeOffset:
                    return XmlConvert.ToString(dateTimeOffset);
                case bool boolean:
                    return XmlConvert.ToString(boolean);
                case Guid guid:
                    return guid.ToString("D", CultureInfo.InvariantCulture);
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        private static object? DeserializeValue(ColumnValue column)
        {
            if (bool.TryParse(column.IsNullIndicator, out var isNull) && isNull)
            {
                return DBNull.Value;
            }

            if (string.IsNullOrWhiteSpace(column.TypeName))
            {
                return column.Value;
            }

            var type = Type.GetType(column.TypeName, throwOnError: false);
            if (type is null)
            {
                return column.Value;
            }

            if (type == typeof(string))
            {
                return column.Value;
            }

            if (type == typeof(byte[]))
            {
                return string.IsNullOrEmpty(column.Value)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(column.Value);
            }

            if (type == typeof(DateTime))
            {
                return XmlConvert.ToDateTime(column.Value, XmlDateTimeSerializationMode.Unspecified);
            }

            if (type == typeof(DateTimeOffset))
            {
                return XmlConvert.ToDateTimeOffset(column.Value);
            }

            if (type == typeof(bool))
            {
                return XmlConvert.ToBoolean(column.Value);
            }

            if (type == typeof(Guid))
            {
                return Guid.Parse(column.Value);
            }

            if (type == typeof(decimal))
            {
                return XmlConvert.ToDecimal(column.Value);
            }

            if (type == typeof(double))
            {
                return XmlConvert.ToDouble(column.Value);
            }

            if (type == typeof(float))
            {
                return XmlConvert.ToSingle(column.Value);
            }

            if (type == typeof(long))
            {
                return XmlConvert.ToInt64(column.Value);
            }

            if (type == typeof(int))
            {
                return XmlConvert.ToInt32(column.Value);
            }

            if (type == typeof(short))
            {
                return XmlConvert.ToInt16(column.Value);
            }

            if (type == typeof(byte))
            {
                return XmlConvert.ToByte(column.Value);
            }

            if (type == typeof(TimeSpan))
            {
                return XmlConvert.ToTimeSpan(column.Value);
            }

            return Convert.ChangeType(column.Value, type, CultureInfo.InvariantCulture);
        }

        private static DbType? GetDbType(Type? type)
        {
            if (type is null)
            {
                return null;
            }

            if (type == typeof(string))
            {
                return DbType.String;
            }

            if (type == typeof(int))
            {
                return DbType.Int32;
            }

            if (type == typeof(long))
            {
                return DbType.Int64;
            }

            if (type == typeof(short))
            {
                return DbType.Int16;
            }

            if (type == typeof(byte))
            {
                return DbType.Byte;
            }

            if (type == typeof(bool))
            {
                return DbType.Boolean;
            }

            if (type == typeof(decimal))
            {
                return DbType.Decimal;
            }

            if (type == typeof(double))
            {
                return DbType.Double;
            }

            if (type == typeof(float))
            {
                return DbType.Single;
            }

            if (type == typeof(DateTime))
            {
                return DbType.DateTime;
            }

            if (type == typeof(DateTimeOffset))
            {
                return DbType.DateTimeOffset;
            }

            if (type == typeof(Guid))
            {
                return DbType.Guid;
            }

            if (type == typeof(byte[]))
            {
                return DbType.Binary;
            }

            if (type == typeof(TimeSpan))
            {
                return DbType.Time;
            }

            return null;
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

        private sealed record ColumnValue(string Name, string? TypeName, string? IsNullIndicator, string Value);
    }
}
