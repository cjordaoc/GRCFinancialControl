namespace DvSchemaSync.Dataverse;

internal sealed record DataverseEntityMetadata(
    string LogicalName,
    string? SchemaName,
    string? DisplayName,
    string PrimaryIdAttribute,
    IReadOnlyDictionary<string, DataverseAttributeMetadata> Attributes,
    IReadOnlyList<DataverseAlternateKey> AlternateKeys,
    IReadOnlyList<DataverseRelationshipMetadata> ManyToOneRelationships);
