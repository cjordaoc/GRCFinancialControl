namespace DvSchemaSync.Dataverse;

internal sealed record DataverseMetadataResult(
    bool ConnectionAvailable,
    string? OrgUrl,
    IReadOnlyDictionary<string, DataverseEntityMetadata> Entities,
    IReadOnlyList<string> Errors)
{
    public static DataverseMetadataResult Unavailable(string? orgUrl, params string[] errors) =>
        new(false, orgUrl, new Dictionary<string, DataverseEntityMetadata>(StringComparer.OrdinalIgnoreCase), errors);

    public static DataverseMetadataResult Success(string? orgUrl, IReadOnlyDictionary<string, DataverseEntityMetadata> entities, IEnumerable<string>? errors = null)
    {
        return new DataverseMetadataResult(
            true,
            orgUrl,
            entities,
            errors?.ToArray() ?? Array.Empty<string>());
    }
}
