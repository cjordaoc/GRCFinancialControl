namespace DvSchemaSync.Sql;

internal sealed record SqlIndex(
    string Name,
    IReadOnlyList<string> Columns,
    bool IsUnique,
    string? IndexType);
