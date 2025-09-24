#if NET8_0_WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xunit;

public sealed class MySqlModelSmokeTests
{
    [Fact]
    public void Model_configuration_matches_mysql_schema()
    {
        var options = new DbContextOptionsBuilder<MySqlDbContext>()
            .UseMySql(
                "Server=localhost;Port=3306;Database=grc_financial_control_model_check;Uid=model;Pwd=model;",
                MySqlServerVersion.LatestSupportedServerVersion)
            .Options;

        using var context = new MySqlDbContext(options);

        var creator = context.GetService<IRelationalDatabaseCreator>();
        var script = creator.GenerateCreateScript();
        Assert.False(string.IsNullOrWhiteSpace(script));

        var schema = LoadSchemaFromCsv();

        foreach (var entityType in context.Model.GetEntityTypes().Where(e => !e.IsOwned()))
        {
            if (entityType.IsKeyless)
            {
                Assert.False(string.IsNullOrEmpty(entityType.GetViewName()));
                continue;
            }

            var tableName = entityType.GetTableName();
            Assert.False(string.IsNullOrWhiteSpace(tableName));
            Assert.True(
                schema.TryGetValue(tableName!, out var expectedColumns),
                $"Missing table mapping for {tableName}.");

            var tableIdentifier = StoreObjectIdentifier.Table(tableName!, entityType.GetSchema());
            var actualColumns = entityType.GetProperties()
                .Select(property => new
                {
                    Column = property.GetColumnName(tableIdentifier),
                    Type = property.GetColumnType()
                })
                .Where(info => !string.IsNullOrEmpty(info.Column))
                .ToList();

            foreach (var column in actualColumns)
            {
                Assert.True(
                    expectedColumns.TryGetValue(column.Column!, out var expectedType),
                    $"Column {tableName}.{column.Column} missing in schema CSV.");
                Assert.Equal(expectedType, column.Type);
            }

            Assert.True(
                expectedColumns.Keys.All(col => actualColumns.Any(actual => string.Equals(actual.Column, col, StringComparison.Ordinal))),
                $"Entity {entityType.Name} is missing column(s) from schema CSV.");
        }
    }

    private static Dictionary<string, Dictionary<string, string>> LoadSchemaFromCsv()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var csvPath = Path.GetFullPath(Path.Combine(baseDirectory, "../../../../GRCFinancialControl/MySQL Specs/Tables.csv"));
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException("Tables.csv was not found for schema verification.", csvPath);
        }

        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        using var reader = new StreamReader(csvPath);
        reader.ReadLine(); // Skip header

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var fields = SplitCsv(line);
            if (fields.Count < 3)
            {
                continue;
            }

            var tableName = fields[0];
            if (string.IsNullOrWhiteSpace(tableName) || !char.IsUpper(tableName[0]))
            {
                continue;
            }

            var columnName = fields[1];
            var columnType = fields[2].Trim().Trim('"');

            if (!result.TryGetValue(tableName, out var columns))
            {
                columns = new Dictionary<string, string>(StringComparer.Ordinal);
                result[tableName] = columns;
            }

            columns[columnName] = columnType;
        }

        return result;
    }

    private static List<string> SplitCsv(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (character == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}
#endif
