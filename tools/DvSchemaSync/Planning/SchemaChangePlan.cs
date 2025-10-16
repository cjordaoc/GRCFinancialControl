using DvSchemaSync.Alignment;
using DvSchemaSync.Dataverse;
using DvSchemaSync.Sql;

namespace DvSchemaSync.Planning;

internal sealed record SchemaChangePlan(
    IReadOnlyList<PlannedAttributeAddition> AttributesToAdd,
    IReadOnlyList<NativeFieldReplacement> NativeReplacements,
    IReadOnlyList<PlannedAlternateKeyAddition> AlternateKeysToAdd,
    IReadOnlyList<PlannedRelationshipAddition> RelationshipsToAdd,
    IReadOnlyList<PlannedAttributeRemoval> AttributesToRemove,
    IReadOnlyList<PlannedAlternateKeyRemoval> AlternateKeysToRemove,
    IReadOnlyList<PlannedRelationshipRemoval> RelationshipsToRemove,
    IReadOnlyList<MissingEntityPlan> MissingEntities)
{
    public bool HasChanges =>
        AttributesToAdd.Count > 0 ||
        AlternateKeysToAdd.Count > 0 ||
        RelationshipsToAdd.Count > 0 ||
        AttributesToRemove.Count > 0 ||
        AlternateKeysToRemove.Count > 0 ||
        RelationshipsToRemove.Count > 0 ||
        MissingEntities.Count > 0;

    public SchemaChangePlanSummary CreateSummary() => new(
        AttributesToAdd.Count,
        NativeReplacements.Count,
        AlternateKeysToAdd.Count,
        RelationshipsToAdd.Count,
        AttributesToRemove.Count,
        AlternateKeysToRemove.Count,
        RelationshipsToRemove.Count,
        MissingEntities.Count);
}

internal sealed record PlannedAttributeAddition(
    string TableName,
    string EntityLogicalName,
    SqlColumn Column);

internal sealed record NativeFieldReplacement(
    string TableName,
    string EntityLogicalName,
    string ColumnName,
    string NativeLogicalName);

internal sealed record PlannedAttributeRemoval(
    string EntityLogicalName,
    DataverseAttributeMetadata Attribute,
    string Reason);

internal sealed record PlannedAlternateKeyAddition(
    string TableName,
    string EntityLogicalName,
    string KeyName,
    IReadOnlyList<string> Columns);

internal sealed record PlannedAlternateKeyRemoval(
    string EntityLogicalName,
    DataverseAlternateKey Key);

internal sealed record PlannedRelationshipAddition(
    string TableName,
    string EntityLogicalName,
    string ReferencedEntityLogicalName,
    SqlForeignKey ForeignKey);

internal sealed record PlannedRelationshipRemoval(
    string EntityLogicalName,
    DataverseRelationshipMetadata Relationship);

internal sealed record MissingEntityPlan(
    string TableName,
    string ExpectedEntityLogicalName);

internal sealed record SchemaChangePlanSummary(
    int AttributesToAdd,
    int NativeReplacements,
    int AlternateKeysToAdd,
    int RelationshipsToAdd,
    int AttributesToRemove,
    int AlternateKeysToRemove,
    int RelationshipsToRemove,
    int MissingEntities);
