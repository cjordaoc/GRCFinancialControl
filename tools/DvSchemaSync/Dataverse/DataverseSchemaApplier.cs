using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DvSchemaSync.Configuration;
using DvSchemaSync.Planning;
using DvSchemaSync.Sql;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace DvSchemaSync.Dataverse;

internal sealed class DataverseSchemaApplier
{
    private static readonly Regex DecimalPrecisionRegex = new(@"\((?<precision>\d+),(?<scale>\d+)\)", RegexOptions.Compiled);
    private readonly DataverseConnectionSettings _connectionSettings;

    public DataverseSchemaApplier(DataverseConnectionSettings connectionSettings)
    {
        _connectionSettings = connectionSettings;
    }

    public async Task ApplyAsync(
        SchemaChangePlan plan,
        ExecutionOptions options,
        DataverseMetadataResult metadata,
        CancellationToken cancellationToken)
    {
        if (!plan.HasChanges)
        {
            Console.WriteLine("No schema changes to apply.");
            return;
        }

        if (!_connectionSettings.IsConfigured)
        {
            throw new InvalidOperationException("Dataverse connection is not configured. Set DV_ORG_URL, DV_CLIENT_ID, DV_CLIENT_SECRET, and DV_TENANT_ID.");
        }

        var connectionString = _connectionSettings.BuildConnectionString();
        using var serviceClient = new ServiceClient(connectionString);
        if (!serviceClient.IsReady)
        {
            throw new InvalidOperationException("Unable to initialize Dataverse ServiceClient for schema application.");
        }

        if (options.RequiresSolutionExport)
        {
            await ExportSolutionAsync(serviceClient, options.SolutionExportName!, options.SolutionExportDirectory, cancellationToken).ConfigureAwait(false);
        }

        await ApplyAttributeCreationsAsync(serviceClient, plan, metadata, cancellationToken).ConfigureAwait(false);
        await ApplyAlternateKeysAsync(serviceClient, plan, metadata, cancellationToken).ConfigureAwait(false);
        await ApplyRelationshipCreationsAsync(serviceClient, plan, metadata, cancellationToken).ConfigureAwait(false);

        if (options.DropsPermitted)
        {
            await ApplyRemovalsAsync(serviceClient, plan, cancellationToken).ConfigureAwait(false);
        }
        else if (plan.AttributesToRemove.Count > 0 || plan.AlternateKeysToRemove.Count > 0 || plan.RelationshipsToRemove.Count > 0)
        {
            Console.WriteLine("Drop operations were planned but skipped. Use --allow-drop and set DVSCHEMA_ALLOW_DROP=1 to enable removals.");
        }
    }

    private static async Task ApplyAttributeCreationsAsync(ServiceClient client, SchemaChangePlan plan, DataverseMetadataResult metadata, CancellationToken cancellationToken)
    {
        foreach (var addition in plan.AttributesToAdd)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!metadata.Entities.TryGetValue(addition.EntityLogicalName, out var entityMetadata))
            {
                Console.WriteLine($"Skipping attribute '{addition.Column.Name}' for entity '{addition.EntityLogicalName}' because metadata is unavailable.");
                continue;
            }

            var prefix = DetermineCustomizationPrefix(entityMetadata.SchemaName);
            if (string.IsNullOrWhiteSpace(prefix))
            {
                Console.WriteLine($"Skipping attribute '{addition.Column.Name}' for entity '{addition.EntityLogicalName}' because a customization prefix could not be determined.");
                continue;
            }

            try
            {
                var attributeMetadata = CreateAttributeMetadata(addition.Column, prefix);
                var request = new CreateAttributeRequest
                {
                    EntityName = addition.EntityLogicalName,
                    Attribute = attributeMetadata
                };

                await client.ExecuteAsync(request).ConfigureAwait(false);
                Console.WriteLine($"Created attribute '{attributeMetadata.LogicalName}' on entity '{addition.EntityLogicalName}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create attribute '{addition.Column.Name}' on entity '{addition.EntityLogicalName}': {ex.Message}");
            }
        }
    }

    private static async Task ApplyAlternateKeysAsync(ServiceClient client, SchemaChangePlan plan, DataverseMetadataResult metadata, CancellationToken cancellationToken)
    {
        foreach (var key in plan.AlternateKeysToAdd)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!metadata.Entities.TryGetValue(key.EntityLogicalName, out var entityMetadata))
            {
                Console.WriteLine($"Skipping alternate key '{key.KeyName}' because metadata for entity '{key.EntityLogicalName}' is unavailable.");
                continue;
            }

            var prefix = DetermineCustomizationPrefix(entityMetadata.SchemaName);
            if (string.IsNullOrWhiteSpace(prefix))
            {
                Console.WriteLine($"Skipping alternate key '{key.KeyName}' because a customization prefix could not be determined.");
                continue;
            }

            var attributeNames = key.Columns
                .Select(column => ResolveAttributeLogicalName(key.EntityLogicalName, column, plan, entityMetadata))
                .ToArray();

            if (attributeNames.Any(name => name is null))
            {
                Console.WriteLine($"Skipping alternate key '{key.KeyName}' because one or more key attributes could not be resolved.");
                continue;
            }

            var logicalName = $"{prefix}_{SanitizeForLogicalName(key.KeyName)}";
            var request = new CreateEntityKeyRequest
            {
                EntityName = key.EntityLogicalName,
                EntityKey = new EntityKeyMetadata
                {
                    DisplayName = new Label(key.KeyName, 1033),
                    LogicalName = logicalName,
                    KeyAttributes = attributeNames!
                        .Select(name => name!)
                        .ToArray()
                }
            };

            try
            {
                await client.ExecuteAsync(request).ConfigureAwait(false);
                Console.WriteLine($"Created alternate key '{logicalName}' on entity '{key.EntityLogicalName}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create alternate key '{key.KeyName}' on entity '{key.EntityLogicalName}': {ex.Message}");
            }
        }

        foreach (var replacement in plan.NativeReplacements)
        {
            Console.WriteLine($"Column '{replacement.ColumnName}' on entity '{replacement.EntityLogicalName}' maps to native attribute '{replacement.NativeLogicalName}'. Update data layer to use the native field.");
        }
    }

    private static Task ApplyRelationshipCreationsAsync(ServiceClient client, SchemaChangePlan plan, DataverseMetadataResult metadata, CancellationToken cancellationToken)
    {
        foreach (var relationship in plan.RelationshipsToAdd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.WriteLine($"Planned relationship '{relationship.ForeignKey.Name}' between '{relationship.EntityLogicalName}' and '{relationship.ReferencedEntityLogicalName}' requires manual creation in Dataverse.");
        }

        return Task.CompletedTask;
    }

    private static async Task ApplyRemovalsAsync(ServiceClient client, SchemaChangePlan plan, CancellationToken cancellationToken)
    {
        foreach (var attribute in plan.AttributesToRemove)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var request = new DeleteAttributeRequest
                {
                    EntityLogicalName = attribute.EntityLogicalName,
                    LogicalName = attribute.Attribute.LogicalName
                };

                await client.ExecuteAsync(request).ConfigureAwait(false);
                Console.WriteLine($"Deleted attribute '{attribute.Attribute.LogicalName}' from entity '{attribute.EntityLogicalName}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete attribute '{attribute.Attribute.LogicalName}' from entity '{attribute.EntityLogicalName}': {ex.Message}");
            }
        }

        foreach (var relationship in plan.RelationshipsToRemove)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var request = new DeleteRelationshipRequest
                {
                    Name = relationship.Relationship.SchemaName
                };

                await client.ExecuteAsync(request).ConfigureAwait(false);
                Console.WriteLine($"Deleted relationship '{relationship.Relationship.SchemaName}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete relationship '{relationship.Relationship.SchemaName}': {ex.Message}");
            }
        }

        foreach (var key in plan.AlternateKeysToRemove)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var request = new DeleteEntityKeyRequest
                {
                    EntityLogicalName = key.EntityLogicalName,
                    Name = key.Key.LogicalName
                };

                await client.ExecuteAsync(request).ConfigureAwait(false);
                Console.WriteLine($"Deleted alternate key '{key.Key.LogicalName}' from entity '{key.EntityLogicalName}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete alternate key '{key.Key.LogicalName}' from entity '{key.EntityLogicalName}': {ex.Message}");
            }
        }
    }

    private static async Task ExportSolutionAsync(ServiceClient client, string solutionName, string outputDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = new ExportSolutionRequest
        {
            SolutionName = solutionName,
            Managed = false
        };

        try
        {
            var response = (ExportSolutionResponse)await client.ExecuteAsync(request).ConfigureAwait(false);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var fileName = Path.Combine(outputDirectory, $"{solutionName}_{timestamp}.zip");
            await File.WriteAllBytesAsync(fileName, response.ExportSolutionFile, cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"Exported Dataverse solution '{solutionName}' to '{fileName}'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to export Dataverse solution '{solutionName}': {ex.Message}");
        }
    }

    private static AttributeMetadata CreateAttributeMetadata(SqlColumn column, string prefix)
    {
        var logicalName = $"{prefix}_{SanitizeForLogicalName(column.Name)}";
        var schemaName = $"{prefix}_{ToPascalCase(column.Name)}";

        var requiredLevel = column.IsNullable ? AttributeRequiredLevel.None : AttributeRequiredLevel.ApplicationRequired;
        var requiredLevelProperty = new AttributeRequiredLevelManagedProperty(requiredLevel);
        var displayName = new Label(column.Name, 1033);

        var sqlType = column.DataType.ToLowerInvariant();

        if (sqlType.Contains("tinyint") && sqlType.Contains("(1)"))
        {
            return new BooleanAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                DisplayName = displayName,
                RequiredLevel = requiredLevelProperty,
                OptionSet = new BooleanOptionSetMetadata(
                    new OptionMetadata(new Label("Yes", 1033), 1),
                    new OptionMetadata(new Label("No", 1033), 0))
            };
        }

        if (sqlType.Contains("int"))
        {
            if (sqlType.Contains("bigint"))
            {
                return new BigIntAttributeMetadata
                {
                    LogicalName = logicalName,
                    SchemaName = schemaName,
                    DisplayName = displayName,
                    RequiredLevel = requiredLevelProperty
                };
            }

            return new IntegerAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                DisplayName = displayName,
                RequiredLevel = requiredLevelProperty,
                Format = IntegerFormat.None
            };
        }

        if (sqlType.Contains("decimal") || sqlType.Contains("numeric"))
        {
            var precision = 18;
            var scale = 2;
            var match = DecimalPrecisionRegex.Match(column.DataType);
            if (match.Success)
            {
                if (int.TryParse(match.Groups["precision"].Value, out var parsedPrecision))
                {
                    precision = parsedPrecision;
                }

                if (int.TryParse(match.Groups["scale"].Value, out var parsedScale))
                {
                    scale = parsedScale;
                }
            }

            return new DecimalAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                DisplayName = displayName,
                RequiredLevel = requiredLevelProperty,
                Precision = scale,
                MaxValue = (decimal)Math.Pow(10, precision - scale) - (decimal)Math.Pow(10, -scale),
                MinValue = -((decimal)Math.Pow(10, precision - scale) - (decimal)Math.Pow(10, -scale))
            };
        }

        if (sqlType.Contains("float") || sqlType.Contains("double"))
        {
            return new DoubleAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                DisplayName = displayName,
                RequiredLevel = requiredLevelProperty
            };
        }

        if (sqlType.Contains("money"))
        {
            return new MoneyAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                DisplayName = displayName,
                RequiredLevel = requiredLevelProperty,
                PrecisionSource = 2
            };
        }

        if (sqlType.Contains("datetime") || sqlType.Contains("timestamp"))
        {
            return new DateTimeAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                DisplayName = displayName,
                RequiredLevel = requiredLevelProperty,
                Format = DateTimeFormat.DateAndTime,
                DateTimeBehavior = DateTimeBehavior.UserLocal
            };
        }

        if (sqlType.Equals("date"))
        {
            return new DateTimeAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                DisplayName = displayName,
                RequiredLevel = requiredLevelProperty,
                Format = DateTimeFormat.DateOnly,
                DateTimeBehavior = DateTimeBehavior.DateOnly
            };
        }

        if (sqlType.Contains("bit"))
        {
            return new BooleanAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                DisplayName = displayName,
                RequiredLevel = requiredLevelProperty,
                OptionSet = new BooleanOptionSetMetadata(
                    new OptionMetadata(new Label("True", 1033), 1),
                    new OptionMetadata(new Label("False", 1033), 0))
            };
        }

        if (sqlType.Contains("text") || sqlType.Contains("longtext") || sqlType.Contains("mediumtext"))
        {
            return new MemoAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                DisplayName = displayName,
                RequiredLevel = requiredLevelProperty,
                MaxLength = 1048576
            };
        }

        if (sqlType.Contains("char") || sqlType.Contains("varchar"))
        {
            var length = ParseStringLength(column.DataType) ?? 255;
            var maxLength = Math.Min(length, 4000);
            return new StringAttributeMetadata
            {
                LogicalName = logicalName,
                SchemaName = schemaName,
                DisplayName = displayName,
                RequiredLevel = requiredLevelProperty,
                MaxLength = maxLength
            };
        }

        throw new NotSupportedException($"SQL data type '{column.DataType}' is not supported for automatic attribute creation.");
    }

    private static string? ResolveAttributeLogicalName(string entityLogicalName, string columnName, SchemaChangePlan plan, DataverseEntityMetadata entityMetadata)
    {
        var match = entityMetadata.Attributes.Values.FirstOrDefault(attribute =>
            attribute.LogicalName.Equals(columnName, StringComparison.OrdinalIgnoreCase) ||
            (attribute.SchemaName?.Equals(columnName, StringComparison.OrdinalIgnoreCase) ?? false));

        if (match is not null)
        {
            return match.LogicalName;
        }

        var replacement = plan.NativeReplacements.FirstOrDefault(rep =>
            rep.EntityLogicalName.Equals(entityLogicalName, StringComparison.OrdinalIgnoreCase) &&
            rep.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        if (replacement is not null)
        {
            return replacement.NativeLogicalName;
        }

        var plannedAttribute = plan.AttributesToAdd.FirstOrDefault(attr =>
            attr.EntityLogicalName.Equals(entityLogicalName, StringComparison.OrdinalIgnoreCase) &&
            attr.Column.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        if (plannedAttribute is not null)
        {
            var prefix = DetermineCustomizationPrefix(entityMetadata.SchemaName);
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                return $"{prefix}_{SanitizeForLogicalName(columnName)}";
            }
        }

        return null;
    }

    private static string? DetermineCustomizationPrefix(string? schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            return null;
        }

        var parts = schemaName.Split('_', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }

    private static string SanitizeForLogicalName(string value)
    {
        var sanitized = new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "customfield";
        }

        return sanitized.Length > 40 ? sanitized[..40] : sanitized;
    }

    private static string ToPascalCase(string value)
    {
        var parts = Regex.Split(value, @"[^A-Za-z0-9]")
            .Where(part => part.Length > 0)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant());

        var result = string.Concat(parts);
        return string.IsNullOrWhiteSpace(result) ? "CustomField" : result;
    }

    private static int? ParseStringLength(string dataType)
    {
        var start = dataType.IndexOf('(');
        var end = dataType.IndexOf(')');
        if (start >= 0 && end > start)
        {
            var number = dataType[(start + 1)..end];
            if (int.TryParse(number, out var length))
            {
                return length;
            }
        }

        return null;
    }
}
