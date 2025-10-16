namespace DvSchemaSync.Alignment;

internal sealed record AlignmentAnalysis(
    DateTime GeneratedAtUtc,
    IReadOnlyDictionary<string, string> TableToEntityMap,
    IReadOnlyList<TableAlignmentResult> Tables);
