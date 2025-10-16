namespace DvSchemaSync.Sql;

internal sealed record SqlColumn(
    string Name,
    string DataType,
    bool IsNullable,
    string? DefaultValue,
    bool IsAutoIncrement,
    bool IsUnsigned,
    string RawDefinition);
