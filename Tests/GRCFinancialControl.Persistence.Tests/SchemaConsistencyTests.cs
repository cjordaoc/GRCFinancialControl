using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GRCFinancialControl.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace GRCFinancialControl.Persistence.Tests;

public sealed class SchemaConsistencyTests
{
    [Fact]
    public void RebuildScriptContainsAllEfTables()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new ApplicationDbContext(options);

        var efTables = context.Model.GetEntityTypes()
            .Select(type => type.GetTableName())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "artifacts",
            "mysql",
            "rebuild_schema.sql"));

        var script = File.ReadAllText(scriptPath);
        var scriptTables = Regex.Matches(script, @"CREATE TABLE(?: IF NOT EXISTS)?\s+`(?<name>[^`]+)`", RegexOptions.IgnoreCase)
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingTables = efTables.Except(scriptTables, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.True(missingTables.Length == 0, $"rebuild_schema.sql is missing tables: {string.Join(", ", missingTables)}");
    }

    [Fact]
    public void RebuildScriptContainsAllEfColumns()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new ApplicationDbContext(options);

        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "artifacts",
            "mysql",
            "rebuild_schema.sql"));

        var script = File.ReadAllText(scriptPath);
        var tableBlocks = Regex.Matches(
                script,
                @"CREATE TABLE(?: IF NOT EXISTS)?\s+`(?<name>[^`]+)`\s*\((?<body>.*?)\)\s*ENGINE",
                RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Cast<Match>()
            .ToDictionary(
                match => match.Groups["name"].Value,
                match => ExtractColumnNames(match.Groups["body"].Value),
                StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            Assert.True(tableBlocks.TryGetValue(tableName!, out var scriptColumns),
                $"rebuild_schema.sql is missing table definition for {tableName}");

            var tableIdentifier = StoreObjectIdentifier.Table(tableName!, entityType.GetSchema());

            var efColumns = entityType.GetProperties()
                .Select(property => property.GetColumnName(tableIdentifier))
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .Select(column => column!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var missingColumns = efColumns
                .Where(column => !scriptColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            Assert.True(
                missingColumns.Length == 0,
                $"rebuild_schema.sql is missing columns for table {tableName}: {string.Join(", ", missingColumns)}");
        }
    }

    [Fact]
    public void OnlyRootSchemaScriptExistsOutsideBuildOutput()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));

        var sqlFiles = Directory.EnumerateFiles(root, "*.sql", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var expected = new[]
        {
            Path.GetFullPath(Path.Combine(root, "artifacts", "mysql", "rebuild_schema.sql"))
        };

        Assert.Equal(expected, sqlFiles);
    }

    private static IReadOnlyCollection<string> ExtractColumnNames(string tableBody)
    {
        var lines = tableBody.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("`", StringComparison.Ordinal))
            {
                continue;
            }

            var closingTick = line.IndexOf('`', 1);
            if (closingTick <= 1)
            {
                continue;
            }

            var column = line.Substring(1, closingTick - 1).Trim();
            if (!string.IsNullOrWhiteSpace(column))
            {
                columns.Add(column);
            }
        }

        return columns;
    }
}
