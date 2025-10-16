namespace DvSchemaSync.Sql;

internal sealed record SqlPrimaryKey(
    string? Name,
    IReadOnlyList<string> Columns);
