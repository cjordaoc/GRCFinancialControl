using System;
using System.IO;
using GRCFinancialControl.Persistence;

namespace GRCFinancialControl.Persistence.Tests;

public sealed class LocalAppRepositoryTests : IDisposable
{
    private readonly TempDatabaseScope _scope = new();

    [Fact]
    public void InsertUpdateAndDeleteConnection_PersistsChanges()
    {
        var repository = CreateRepository();
        Assert.True(File.Exists(repository.DatabasePath));

        var definition = new ConnectionDefinition
        {
            Name = "Primary",
            Server = "localhost",
            Port = 3306,
            Database = "grc",
            Username = "tester",
            Password = "secret",
            UseSsl = true
        };

        var id = repository.InsertConnection(definition);
        Assert.True(id > 0);

        var fetched = repository.GetConnection(id);
        Assert.NotNull(fetched);
        Assert.Equal("Primary", fetched!.Name);
        Assert.Equal("localhost", fetched.Server);
        Assert.Equal((uint)3306, fetched.Port);
        Assert.Equal("grc", fetched.Database);
        Assert.Equal("tester", fetched.Username);
        Assert.Equal("secret", fetched.Password);
        Assert.True(fetched.UseSsl);

        fetched.Name = "Updated";
        fetched.UseSsl = false;
        repository.UpdateConnection(fetched);

        var updated = repository.GetConnection(id);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated!.Name);
        Assert.False(updated.UseSsl);

        var allConnections = repository.GetConnections();
        Assert.Single(allConnections);

        repository.DeleteConnection(id);
        Assert.Empty(repository.GetConnections());
        Assert.Null(repository.GetDefaultConnectionId());
    }

    [Fact]
    public void DefaultConnectionId_UsesParametersTable()
    {
        var repository = CreateRepository();

        var definition = new ConnectionDefinition
        {
            Name = "Default",
            Server = "db.example",
            Port = 3307,
            Database = "finance",
            Username = "app",
            Password = "pw",
            UseSsl = false
        };

        var id = repository.InsertConnection(definition);
        repository.SetDefaultConnectionId(id);

        var storedId = repository.GetDefaultConnectionId();
        Assert.Equal(id, storedId);

        repository.SetDefaultConnectionId(null);
        Assert.Null(repository.GetDefaultConnectionId());

        repository.SetDefaultConnectionId(id);
        repository.DeleteConnection(id);
        Assert.Null(repository.GetDefaultConnectionId());
    }

    private LocalAppRepository CreateRepository()
    {
        return new LocalAppRepository(_scope.DatabasePath);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }

    private sealed class TempDatabaseScope : IDisposable
    {
        public TempDatabaseScope()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "GRCFinancialControlTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            DatabasePath = Path.Combine(DirectoryPath, "appdata.db");
        }

        public string DirectoryPath { get; }
        public string DatabasePath { get; }

        public void Dispose()
        {
            try
            {
                if (File.Exists(DatabasePath))
                {
                    File.Delete(DatabasePath);
                }
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }

            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }
}
