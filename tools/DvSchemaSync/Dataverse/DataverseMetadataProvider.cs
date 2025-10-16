using System.Linq;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace DvSchemaSync.Dataverse;

internal sealed class DataverseMetadataProvider
{
    private readonly DataverseConnectionSettings _connectionSettings;

    public DataverseMetadataProvider(DataverseConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
    }

    public async Task<DataverseMetadataResult> LoadMetadataAsync(IEnumerable<string> logicalNames, CancellationToken cancellationToken)
    {
        var entityNames = logicalNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (entityNames.Length == 0)
        {
            return DataverseMetadataResult.Success(_connectionSettings.OrgUrl, new Dictionary<string, DataverseEntityMetadata>(StringComparer.OrdinalIgnoreCase));
        }

        if (!_connectionSettings.IsConfigured)
        {
            return DataverseMetadataResult.Unavailable(_connectionSettings.OrgUrl, "Dataverse connection is not configured. Set DV_ORG_URL, DV_CLIENT_ID, DV_CLIENT_SECRET, and DV_TENANT_ID.");
        }

        try
        {
            var connectionString = _connectionSettings.BuildConnectionString();
            using var serviceClient = new ServiceClient(connectionString);
            if (!serviceClient.IsReady)
            {
                return DataverseMetadataResult.Unavailable(_connectionSettings.OrgUrl, "The Dataverse service client could not be initialized. Check credentials and network connectivity.");
            }

            var entities = new Dictionary<string, DataverseEntityMetadata>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();

            foreach (var logicalName in entityNames)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var request = new RetrieveEntityRequest
                    {
                        LogicalName = logicalName,
                        EntityFilters = EntityFilters.Attributes | EntityFilters.Relationships | EntityFilters.Entity,
                        RetrieveAsIfPublished = true
                    };

                    var response = (RetrieveEntityResponse)await serviceClient.ExecuteAsync(request).ConfigureAwait(false);
                    var metadata = ConvertToMetadata(response.EntityMetadata);
                    entities[logicalName] = metadata;
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to retrieve metadata for '{logicalName}': {ex.Message}");
                }
            }

            return DataverseMetadataResult.Success(_connectionSettings.OrgUrl, entities, errors);
        }
        catch (Exception ex)
        {
            return DataverseMetadataResult.Unavailable(_connectionSettings.OrgUrl, $"Dataverse metadata retrieval failed: {ex.Message}");
        }
    }

    private static DataverseEntityMetadata ConvertToMetadata(EntityMetadata entityMetadata)
    {
        var attributes = entityMetadata.Attributes
            .Where(attribute => attribute.AttributeOf is null)
            .Select(attribute => ConvertAttribute(attribute))
            .ToDictionary(attribute => attribute.LogicalName, attribute => attribute, StringComparer.OrdinalIgnoreCase);

        var alternateKeys = entityMetadata.Keys
            ?.Select(key => new DataverseAlternateKey(key.LogicalName, key.KeyAttributes.ToArray()))
            .ToArray() ?? Array.Empty<DataverseAlternateKey>();

        var relationships = entityMetadata.ManyToOneRelationships
            ?.Select(rel => new DataverseRelationshipMetadata(
                rel.SchemaName,
                rel.ReferencingAttribute,
                rel.ReferencingEntity,
                rel.ReferencedAttribute,
                rel.ReferencedEntity,
                rel.IsCustomRelationship ?? false))
            .ToArray() ?? Array.Empty<DataverseRelationshipMetadata>();

        return new DataverseEntityMetadata(
            entityMetadata.LogicalName,
            entityMetadata.SchemaName,
            entityMetadata.DisplayName?.UserLocalizedLabel?.Label,
            entityMetadata.PrimaryIdAttribute,
            attributes,
            alternateKeys,
            relationships);
    }

    private static DataverseAttributeMetadata ConvertAttribute(AttributeMetadata metadata)
    {
        var logicalName = metadata.LogicalName;
        var schemaName = metadata.SchemaName;
        var attributeType = metadata.AttributeTypeName?.Value ?? metadata.AttributeType?.ToString() ?? "Unknown";
        var requiredLevel = metadata.RequiredLevel?.Value;
        var isNullable = requiredLevel is null || requiredLevel == AttributeRequiredLevel.None || requiredLevel == AttributeRequiredLevel.Recommended;
        var isCustom = metadata.IsCustomAttribute ?? false;
        var displayName = metadata.DisplayName?.UserLocalizedLabel?.Label;
        var targets = metadata is LookupAttributeMetadata lookup && lookup.Targets is not null
            ? lookup.Targets.ToArray()
            : Array.Empty<string>();

        return new DataverseAttributeMetadata(
            logicalName,
            schemaName,
            attributeType,
            isNullable,
            isCustom,
            displayName,
            targets);
    }
}
