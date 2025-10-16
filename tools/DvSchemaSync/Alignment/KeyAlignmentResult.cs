namespace DvSchemaSync.Alignment;

internal sealed record KeyAlignmentResult(
    string Name,
    IReadOnlyList<string> SqlColumns,
    IReadOnlyList<string> DataverseColumns,
    KeyAlignmentStatus Status);
