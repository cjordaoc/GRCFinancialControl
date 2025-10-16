namespace DvSchemaSync.Configuration;

internal static class NativeFieldMappings
{
    public static IReadOnlyDictionary<string, string> SqlToDataverse { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["CreatedAt"] = "createdon",
        ["UpdatedAt"] = "modifiedon",
        ["CreatedBy"] = "createdby",
        ["UpdatedBy"] = "modifiedby",
        ["OwnerId"] = "ownerid",
        ["Owner"] = "ownerid",
        ["Status"] = "statuscode",
        ["StatusText"] = "statecode",
        ["IsActive"] = "statecode",
        ["IsDeleted"] = "statecode",
        ["DeletedAt"] = "overriddencreatedon",
        ["AssignedTo"] = "ownerid"
    };
}
