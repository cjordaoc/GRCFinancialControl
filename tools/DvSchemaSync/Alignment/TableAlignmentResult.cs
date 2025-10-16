using DvSchemaSync.Dataverse;
using DvSchemaSync.Sql;

namespace DvSchemaSync.Alignment;

internal sealed record TableAlignmentResult(
    SqlTable Table,
    string? DataverseEntityName,
    DataverseEntityMetadata? DataverseEntity,
    IReadOnlyList<ColumnAlignmentResult> Columns,
    IReadOnlyList<KeyAlignmentResult> Keys,
    IReadOnlyList<ForeignKeyAlignmentResult> ForeignKeys,
    IReadOnlyList<DataverseAttributeMetadata> UnmatchedDataverseAttributes,
    IReadOnlyList<DataverseAlternateKey> UnmatchedDataverseAlternateKeys,
    IReadOnlyList<DataverseRelationshipMetadata> UnmatchedRelationships);
