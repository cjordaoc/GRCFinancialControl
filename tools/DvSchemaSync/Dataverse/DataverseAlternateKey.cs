namespace DvSchemaSync.Dataverse;

internal sealed record DataverseAlternateKey(
    string LogicalName,
    IReadOnlyList<string> KeyAttributes);
