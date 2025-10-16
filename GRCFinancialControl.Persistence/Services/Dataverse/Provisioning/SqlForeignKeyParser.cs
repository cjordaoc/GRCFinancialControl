using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;

public sealed class SqlForeignKeyParser
{
    private static readonly Regex ForeignKeyRegex = new(
        @"CONSTRAINT\s+`(?<name>[^`]+)`\s+FOREIGN KEY\s*\((?<child>[^\)]+)\)\s+REFERENCES\s+`(?<parentTable>[^`]+)`\s*\((?<parent>[^\)]+)\)(?:\s+ON DELETE\s+(?<delete>[A-Z ]+))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<SqlForeignKeyDefinition> Parse(string sqlFilePath)
    {
        if (!File.Exists(sqlFilePath))
        {
            throw new FileNotFoundException("The SQL schema definition file could not be found.", sqlFilePath);
        }

        var lines = File.ReadAllLines(sqlFilePath);
        var tableName = string.Empty;
        var results = new List<SqlForeignKeyDefinition>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed.Split('`');
                if (parts.Length > 1)
                {
                    tableName = parts[1];
                }
                continue;
            }

            if (trimmed.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase) && tableName.Length > 0)
            {
                var match = ForeignKeyRegex.Match(trimmed);
                if (match.Success)
                {
                    var childColumns = match.Groups["child"].Value.Split(',').Select(c => c.Trim(' ', '`')).ToArray();
                    var parentColumns = match.Groups["parent"].Value.Split(',').Select(c => c.Trim(' ', '`')).ToArray();
                    var deleteBehavior = match.Groups["delete"].Success ? match.Groups["delete"].Value.Trim().ToUpperInvariant() : "RESTRICT";
                    results.Add(new SqlForeignKeyDefinition(
                        match.Groups["name"].Value,
                        tableName,
                        childColumns,
                        match.Groups["parentTable"].Value,
                        parentColumns,
                        deleteBehavior));
                }
            }
        }

        return results;
    }
}

public sealed record SqlForeignKeyDefinition(
    string Name,
    string ChildTable,
    IReadOnlyList<string> ChildColumns,
    string ParentTable,
    IReadOnlyList<string> ParentColumns,
    string DeleteBehavior);
