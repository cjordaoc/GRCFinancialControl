namespace DvSchemaSync.Sql;

internal sealed record SqlForeignKey(
    string Name,
    IReadOnlyList<string> Columns,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumns,
    string? OnDelete,
    string? OnUpdate);
