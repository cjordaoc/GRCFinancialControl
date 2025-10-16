namespace DvSchemaSync.Sql;

internal sealed record SqlTable(
    string Name,
    IReadOnlyList<SqlColumn> Columns,
    SqlPrimaryKey? PrimaryKey,
    IReadOnlyList<SqlIndex> UniqueKeys,
    IReadOnlyList<SqlForeignKey> ForeignKeys,
    IReadOnlyList<SqlIndex> Indexes);
