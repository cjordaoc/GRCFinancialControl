using DvSchemaSync.Dataverse;
using DvSchemaSync.Sql;

namespace DvSchemaSync.Alignment;

internal sealed record ColumnAlignmentResult(
    SqlColumn Column,
    ColumnAlignmentStatus Status,
    DataverseAttributeMetadata? DataverseAttribute,
    string? Notes);
