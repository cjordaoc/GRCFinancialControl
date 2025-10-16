namespace DvSchemaSync.Dataverse;

internal sealed record DataverseRelationshipMetadata(
    string SchemaName,
    string ReferencingAttribute,
    string ReferencingEntity,
    string ReferencedAttribute,
    string ReferencedEntity,
    bool IsCustomRelationship);
