using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace GRCFinancialControl.Persistence
{
    public sealed class LocalAppRepository
    {
        private const string DefaultConnectionParameter = "DefaultConnectionId";
        private readonly string _databasePath;
        private readonly string _connectionString;

        public LocalAppRepository(string? databasePath = null)
        {
            _databasePath = ResolveDatabasePath(databasePath);
            _connectionString = BuildConnectionString(_databasePath);
            EnsureSchema();
        }

        public string DatabasePath => _databasePath;

        private static string ResolveDatabasePath(string? databasePath)
        {
            if (!string.IsNullOrWhiteSpace(databasePath))
            {
                var fullPath = Path.GetFullPath(databasePath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                return fullPath;
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                appData = AppDomain.CurrentDomain.BaseDirectory;
            }

            var directoryPath = Path.Combine(appData, "GRCFinancialControl");
            Directory.CreateDirectory(directoryPath);
            return Path.Combine(directoryPath, "appdata.db");
        }

        private static string BuildConnectionString(string databasePath)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private
            };

            return builder.ToString();
        }

        private SqliteConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        private void EnsureSchema()
        {
            using var connection = CreateConnection();
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS Connections (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Server TEXT NOT NULL,
    Port INTEGER NOT NULL,
    DatabaseName TEXT NOT NULL,
    Username TEXT NOT NULL,
    Password TEXT NOT NULL,
    UseSsl INTEGER NOT NULL
);";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS AppParameters (
    Name TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS parameters (
    param_key   TEXT PRIMARY KEY,
    param_value TEXT NOT NULL,
    updated_utc TEXT NOT NULL DEFAULT (datetime('now'))
);";
                command.ExecuteNonQuery();
            }
        }

        public IReadOnlyList<ConnectionDefinition> GetConnections()
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Server, Port, DatabaseName, Username, Password, UseSsl FROM Connections ORDER BY Name";
            using var reader = command.ExecuteReader();
            var results = new List<ConnectionDefinition>();
            while (reader.Read())
            {
                results.Add(ReadConnection(reader));
            }

            return results;
        }

        public ConnectionDefinition? GetConnection(long id)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Server, Port, DatabaseName, Username, Password, UseSsl FROM Connections WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadConnection(reader);
            }

            return null;
        }

        public long InsertConnection(ConnectionDefinition definition)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO Connections (Name, Server, Port, DatabaseName, Username, Password, UseSsl)
VALUES ($name, $server, $port, $database, $username, $password, $useSsl);
SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$name", definition.Name);
            command.Parameters.AddWithValue("$server", definition.Server);
            command.Parameters.AddWithValue("$port", definition.Port);
            command.Parameters.AddWithValue("$database", definition.Database);
            command.Parameters.AddWithValue("$username", definition.Username);
            command.Parameters.AddWithValue("$password", definition.Password);
            command.Parameters.AddWithValue("$useSsl", definition.UseSsl ? 1 : 0);

            var result = command.ExecuteScalar();
            return Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        public void UpdateConnection(ConnectionDefinition definition)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE Connections
SET Name = $name,
    Server = $server,
    Port = $port,
    DatabaseName = $database,
    Username = $username,
    Password = $password,
    UseSsl = $useSsl
WHERE Id = $id";
            command.Parameters.AddWithValue("$id", definition.Id);
            command.Parameters.AddWithValue("$name", definition.Name);
            command.Parameters.AddWithValue("$server", definition.Server);
            command.Parameters.AddWithValue("$port", definition.Port);
            command.Parameters.AddWithValue("$database", definition.Database);
            command.Parameters.AddWithValue("$username", definition.Username);
            command.Parameters.AddWithValue("$password", definition.Password);
            command.Parameters.AddWithValue("$useSsl", definition.UseSsl ? 1 : 0);

            command.ExecuteNonQuery();
        }

        public void DeleteConnection(long id)
        {
            using var connection = CreateConnection();
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM Connections WHERE Id = $id";
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            }

            var defaultId = GetDefaultConnectionId();
            if (defaultId.HasValue && defaultId.Value == id)
            {
                SetDefaultConnectionId(null);
            }
        }

        public long? GetDefaultConnectionId()
        {
            var value = GetParameter(DefaultConnectionParameter);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                return id;
            }

            return null;
        }

        public void SetDefaultConnectionId(long? id)
        {
            if (id.HasValue)
            {
                SetParameter(DefaultConnectionParameter, id.Value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                RemoveParameter(DefaultConnectionParameter);
            }
        }

        private ConnectionDefinition ReadConnection(SqliteDataReader reader)
        {
            return new ConnectionDefinition
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Server = reader.GetString(2),
                Port = (uint)reader.GetInt32(3),
                Database = reader.GetString(4),
                Username = reader.GetString(5),
                Password = reader.GetString(6),
                UseSsl = reader.GetInt32(7) != 0
            };
        }

        private string? GetParameter(string name)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM AppParameters WHERE Name = $name";
            command.Parameters.AddWithValue("$name", name);

            return command.ExecuteScalar() as string;
        }

        private void SetParameter(string name, string value)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO AppParameters (Name, Value)
VALUES ($name, $value)
ON CONFLICT(Name) DO UPDATE SET Value = excluded.Value";
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }

        private void RemoveParameter(string name)
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM AppParameters WHERE Name = $name";
            command.Parameters.AddWithValue("$name", name);
            command.ExecuteNonQuery();
        }
    }
}
