namespace DvSchemaSync.Dataverse;

internal sealed record DataverseAttributeMetadata(
    string LogicalName,
    string? SchemaName,
    string AttributeType,
    bool IsNullable,
    bool IsCustomAttribute,
    string? DisplayName,
    IReadOnlyList<string> Targets);
