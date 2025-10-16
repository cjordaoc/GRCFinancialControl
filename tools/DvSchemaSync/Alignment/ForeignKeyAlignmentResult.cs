using DvSchemaSync.Sql;

namespace DvSchemaSync.Alignment;

internal sealed record ForeignKeyAlignmentResult(
    SqlForeignKey ForeignKey,
    ForeignKeyAlignmentStatus Status,
    string? MatchedRelationshipSchemaName,
    string? MatchedReferencingAttribute);
