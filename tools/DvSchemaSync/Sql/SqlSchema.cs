namespace DvSchemaSync.Sql;

internal sealed record SqlSchema(IReadOnlyDictionary<string, SqlTable> Tables);
