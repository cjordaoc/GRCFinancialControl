using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DvSchemaSync.Sql;

internal sealed class SqlSchemaParser
{
    private static readonly Regex CreateTableRegex = new(
        @"CREATE\s+TABLE\s+`?(?<name>[\w_]+)`?\s*\((?<body>.*?)\)\s*(ENGINE|;)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SqlSchema Parse(string sqlContent)
    {
        if (string.IsNullOrWhiteSpace(sqlContent))
        {
            throw new ArgumentException("SQL content cannot be empty.", nameof(sqlContent));
        }

        var cleaned = StripComments(sqlContent);
        var tables = new Dictionary<string, SqlTable>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in CreateTableRegex.Matches(cleaned))
        {
            var tableName = match.Groups["name"].Value;
            var body = match.Groups["body"].Value;
            var table = ParseTable(tableName, body);
            tables[tableName] = table;
        }

        if (tables.Count == 0)
        {
            throw new InvalidOperationException("No CREATE TABLE statements were found in the SQL schema.");
        }

        return new SqlSchema(tables);
    }

    public SqlSchema ParseFromFile(string path)
    {
        var content = File.ReadAllText(path);
        return Parse(content);
    }

    private static SqlTable ParseTable(string tableName, string body)
    {
        var columnList = new List<SqlColumn>();
        SqlPrimaryKey? primaryKey = null;
        var uniqueKeys = new List<SqlIndex>();
        var indexes = new List<SqlIndex>();
        var foreignKeys = new List<SqlForeignKey>();

        foreach (var part in SplitDefinitions(body))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase))
            {
                ParseConstraint(trimmed, uniqueKeys, foreignKeys, ref primaryKey);
            }
            else if (trimmed.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
            {
                primaryKey = ParsePrimaryKey(null, trimmed);
            }
            else if (trimmed.StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                uniqueKeys.Add(ParseIndex(trimmed, isUnique: true));
            }
            else if (trimmed.StartsWith("KEY", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("INDEX", StringComparison.OrdinalIgnoreCase))
            {
                indexes.Add(ParseIndex(trimmed, isUnique: false));
            }
            else if (trimmed.StartsWith("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
            {
                foreignKeys.Add(ParseForeignKey(null, trimmed));
            }
            else
            {
                columnList.Add(ParseColumn(trimmed));
            }
        }

        return new SqlTable(
            tableName,
            columnList,
            primaryKey,
            uniqueKeys,
            foreignKeys,
            indexes);
    }

    private static void ParseConstraint(string definition, List<SqlIndex> uniqueKeys, List<SqlForeignKey> foreignKeys, ref SqlPrimaryKey? primaryKey)
    {
        var remainder = definition;
        string? constraintName = null;

        var tokens = definition.Split(new[] { ' ', '\t', '\r', '\n' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 3 && tokens[0].Equals("CONSTRAINT", StringComparison.OrdinalIgnoreCase))
        {
            constraintName = tokens[1].Trim('`');
            remainder = tokens[2];
        }

        if (remainder.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
        {
            primaryKey = ParsePrimaryKey(constraintName, remainder);
        }
        else if (remainder.StartsWith("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
        {
            foreignKeys.Add(ParseForeignKey(constraintName, remainder));
        }
        else if (remainder.StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            uniqueKeys.Add(ParseIndex(remainder, isUnique: true, constraintName));
        }
    }

    private static SqlColumn ParseColumn(string definition)
    {
        var columnMatch = Regex.Match(definition, @"^`?(?<name>[\w_]+)`?\s+(?<type>[^\s]+(?:\s*\([^)]*\))?)(?<rest>.*)$", RegexOptions.IgnoreCase);
        if (!columnMatch.Success)
        {
            throw new InvalidOperationException($"Unable to parse column definition: {definition}");
        }

        var name = columnMatch.Groups["name"].Value;
        var type = columnMatch.Groups["type"].Value.Trim();
        var rest = columnMatch.Groups["rest"].Value;

        if (type.Contains("(") && !type.Contains(")"))
        {
            var closingIndex = rest.IndexOf(")");
            if (closingIndex >= 0)
            {
                var extra = rest[..(closingIndex + 1)];
                type = (type + extra).Trim();
                rest = rest[(closingIndex + 1)..];
            }
        }

        var isNullable = !rest.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase);
        var isAutoIncrement = rest.Contains("AUTO_INCREMENT", StringComparison.OrdinalIgnoreCase);
        var isUnsigned = rest.Contains("UNSIGNED", StringComparison.OrdinalIgnoreCase);
        string? defaultValue = null;

        var defaultMatch = Regex.Match(rest, @"DEFAULT\s+(?<value>(NULL|CURRENT_TIMESTAMP|[^,\s]+|'[^']*'))", RegexOptions.IgnoreCase);
        if (defaultMatch.Success)
        {
            defaultValue = defaultMatch.Groups["value"].Value.Trim();
        }

        return new SqlColumn(name, type, isNullable, defaultValue, isAutoIncrement, isUnsigned, definition);
    }

    private static SqlPrimaryKey ParsePrimaryKey(string? constraintName, string definition)
    {
        var name = constraintName;
        var columnMatch = Regex.Match(definition, @"PRIMARY\s+KEY\s*(?:`?(?<name>[\w_]+)`?\s*)?\((?<columns>[^)]*)\)", RegexOptions.IgnoreCase);
        if (columnMatch.Success)
        {
            if (string.IsNullOrWhiteSpace(name) && columnMatch.Groups["name"].Success)
            {
                name = columnMatch.Groups["name"].Value.Trim('`');
            }

            var columns = ParseColumnList(columnMatch.Groups["columns"].Value);
            return new SqlPrimaryKey(name, columns);
        }

        throw new InvalidOperationException($"Unable to parse primary key definition: {definition}");
    }

    private static SqlIndex ParseIndex(string definition, bool isUnique, string? constraintName = null)
    {
        var pattern = isUnique
            ? @"UNIQUE\s+(?:KEY|INDEX)\s*`?(?<name>[\w_]+)`?\s*\((?<columns>[^)]*)\)"
            : @"(?:KEY|INDEX)\s*`?(?<name>[\w_]+)`?\s*(?:USING\s+(?<type>\w+))?\s*\((?<columns>[^)]*)\)";

        var match = Regex.Match(definition, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            if (definition.StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase) && constraintName is not null)
            {
                // Some definitions use CONSTRAINT ... UNIQUE (`col`)
                var fallback = Regex.Match(definition, @"UNIQUE\s*\((?<columns>[^)]*)\)", RegexOptions.IgnoreCase);
                if (fallback.Success)
                {
                    var columns = ParseColumnList(fallback.Groups["columns"].Value);
                    return new SqlIndex(constraintName, columns, true, null);
                }
            }

            throw new InvalidOperationException($"Unable to parse index definition: {definition}");
        }

        var name = constraintName ?? match.Groups["name"].Value.Trim('`');
        var columnsList = ParseColumnList(match.Groups["columns"].Value);
        var indexType = match.Groups["type"].Success ? match.Groups["type"].Value : null;
        return new SqlIndex(name, columnsList, isUnique, indexType);
    }

    private static SqlForeignKey ParseForeignKey(string? constraintName, string definition)
    {
        var pattern = @"FOREIGN\s+KEY\s*(?:`?(?<name>[\w_]+)`?\s*)?\((?<columns>[^)]*)\)\s*REFERENCES\s+`?(?<refTable>[\w_]+)`?\s*\((?<refColumns>[^)]*)\)(?<rest>.*)";
        var match = Regex.Match(definition, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Unable to parse foreign key definition: {definition}");
        }

        var name = constraintName ?? (match.Groups["name"].Success ? match.Groups["name"].Value.Trim('`') : string.Empty);
        var columns = ParseColumnList(match.Groups["columns"].Value);
        var refColumns = ParseColumnList(match.Groups["refColumns"].Value);
        var referencedTable = match.Groups["refTable"].Value;
        var rest = match.Groups["rest"].Value;

        string? onDelete = null;
        string? onUpdate = null;

        var deleteMatch = Regex.Match(rest, @"ON\s+DELETE\s+(?<action>SET\s+NULL|CASCADE|RESTRICT|NO\s+ACTION)", RegexOptions.IgnoreCase);
        if (deleteMatch.Success)
        {
            onDelete = deleteMatch.Groups["action"].Value.ToUpperInvariant();
        }

        var updateMatch = Regex.Match(rest, @"ON\s+UPDATE\s+(?<action>SET\s+NULL|CASCADE|RESTRICT|NO\s+ACTION)", RegexOptions.IgnoreCase);
        if (updateMatch.Success)
        {
            onUpdate = updateMatch.Groups["action"].Value.ToUpperInvariant();
        }

        return new SqlForeignKey(name, columns, referencedTable, refColumns, onDelete, onUpdate);
    }

    private static IReadOnlyList<string> ParseColumnList(string value)
    {
        var segments = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments
            .Select(segment => segment.Trim().Trim('`').Trim())
            .Where(segment => segment.Length > 0)
            .ToArray();
    }

    private static IEnumerable<string> SplitDefinitions(string body)
    {
        var parts = new List<string>();
        var builder = new StringBuilder();
        var depth = 0;
        var inSingleQuote = false;
        var inBacktick = false;

        foreach (var ch in body)
        {
            switch (ch)
            {
                case '(' when !inSingleQuote && !inBacktick:
                    depth++;
                    builder.Append(ch);
                    break;
                case ')' when !inSingleQuote && !inBacktick:
                    depth--;
                    builder.Append(ch);
                    break;
                case '\'' when !inBacktick:
                    inSingleQuote = !inSingleQuote;
                    builder.Append(ch);
                    break;
                case '`' when !inSingleQuote:
                    inBacktick = !inBacktick;
                    builder.Append(ch);
                    break;
                case ',' when depth == 0 && !inSingleQuote && !inBacktick:
                    parts.Add(builder.ToString());
                    builder.Clear();
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        if (builder.Length > 0)
        {
            parts.Add(builder.ToString());
        }

        return parts;
    }

    private static string StripComments(string input)
    {
        var withoutBlockComments = Regex.Replace(input, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var lines = withoutBlockComments
            .Split('\n')
            .Select(line =>
            {
                var index = line.IndexOf("--", StringComparison.Ordinal);
                return index >= 0 ? line[..index] : line;
            });
        return string.Join('\n', lines);
    }
}
