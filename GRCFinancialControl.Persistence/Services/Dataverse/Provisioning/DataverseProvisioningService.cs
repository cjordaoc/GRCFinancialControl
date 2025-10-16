
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Extensions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;

public sealed class DataverseProvisioningService : IDataverseProvisioningService
{
    private const int EnglishLcid = 1033;

    private readonly IDataverseRepository _repository;
    private readonly SqlForeignKeyParser _foreignKeyParser;
    private readonly ILogger<DataverseProvisioningService> _logger;
    private readonly DataverseProvisioningOptions _options;

    public DataverseProvisioningService(
        IDataverseRepository repository,
        SqlForeignKeyParser foreignKeyParser,
        IOptions<DataverseProvisioningOptions> options,
        ILogger<DataverseProvisioningService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _foreignKeyParser = foreignKeyParser ?? throw new ArgumentNullException(nameof(foreignKeyParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public async Task<DataverseProvisioningResult> ProvisionAsync(CancellationToken cancellationToken)
    {
        var loader = new DataverseMetadataLoader(_options.MetadataPath);
        var changeSet = loader.Load();
        var foreignKeys = _foreignKeyParser.Parse(_options.SqlSchemaPath);
        ValidateForeignKeyCoverage(changeSet, foreignKeys);

        var actions = new List<string>();

        await _repository.ExecuteAsync(async client =>
        {
            await ValidateConnectionAsync(client, cancellationToken).ConfigureAwait(false);
            actions.Add("Validated Dataverse connectivity.");

            var publisherId = await EnsurePublisherAsync(client, changeSet.Solution.Publisher, actions, cancellationToken).ConfigureAwait(false);
            await EnsureSolutionAsync(client, changeSet.Solution, publisherId, actions, cancellationToken).ConfigureAwait(false);
            await EnsureGlobalOptionSetsAsync(client, changeSet.OptionSets, actions, cancellationToken).ConfigureAwait(false);

            await DropExistingTablesAsync(client, changeSet.Tables, actions, cancellationToken).ConfigureAwait(false);

            var entityMetadata = new Dictionary<string, EntityMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in changeSet.Tables)
            {
                var metadata = await EnsureTableAsync(client, table, actions, cancellationToken).ConfigureAwait(false);
                entityMetadata[table.LogicalName] = metadata;
            }

            foreach (var table in changeSet.Tables)
            {
                var metadata = entityMetadata[table.LogicalName];
                await EnsureAttributesAsync(client, table, metadata, changeSet.Solution.UniqueName, actions, cancellationToken).ConfigureAwait(false);
                await EnsureAlternateKeysAsync(client, table, metadata, changeSet.Solution.UniqueName, actions, cancellationToken).ConfigureAwait(false);
            }

            await EnsureRelationshipsAsync(client, changeSet.Tables, foreignKeys, changeSet.Solution.UniqueName, actions, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return new DataverseProvisioningResult(true, actions);
    }

    private async Task DropExistingTablesAsync(ServiceClient client, IReadOnlyList<DataverseTableDefinition> tables, IList<string> actions, CancellationToken cancellationToken)
    {
        foreach (var table in tables.Reverse())
        {
            var logicalName = table.LogicalName;
            var metadata = await TryRetrieveEntityMetadataAsync(client, logicalName, cancellationToken).ConfigureAwait(false);
            if (metadata is null)
            {
                continue;
            }

            await DeleteCustomRelationshipsAsync(client, metadata, actions, cancellationToken).ConfigureAwait(false);

            var request = new DeleteEntityRequest
            {
                LogicalName = logicalName,
            };

            try
            {
                await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
                actions.Add($"Dropped Dataverse table '{logicalName}'.");
            }
            catch (FaultException<OrganizationServiceFault> ex) when (IsMissingObject(ex))
            {
                _logger.LogDebug(ex, "Dataverse table {Table} no longer exists during drop operation.", logicalName);
            }
        }
    }

    private static async Task DeleteCustomRelationshipsAsync(ServiceClient client, EntityMetadata metadata, IList<string> actions, CancellationToken cancellationToken)
    {
        foreach (var relationship in metadata.OneToManyRelationships ?? Array.Empty<OneToManyRelationshipMetadata>())
        {
            if (relationship.IsCustomRelationship != true)
            {
                continue;
            }

            await DeleteRelationshipAsync(client, relationship.SchemaName, actions, cancellationToken).ConfigureAwait(false);
        }

        foreach (var relationship in metadata.ManyToManyRelationships ?? Array.Empty<ManyToManyRelationshipMetadata>())
        {
            if (relationship.IsCustomRelationship != true)
            {
                continue;
            }

            await DeleteRelationshipAsync(client, relationship.SchemaName, actions, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task DeleteRelationshipAsync(ServiceClient client, string relationshipSchemaName, IList<string> actions, CancellationToken cancellationToken)
    {
        var request = new DeleteRelationshipRequest
        {
            Name = relationshipSchemaName,
        };

        try
        {
            await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            actions.Add($"Removed Dataverse relationship '{relationshipSchemaName}'.");
        }
        catch (FaultException<OrganizationServiceFault> ex) when (IsMissingObject(ex))
        {
            // Relationship was already removed.
        }
    }

    private async Task<EntityMetadata?> TryRetrieveEntityMetadataAsync(ServiceClient client, string logicalName, CancellationToken cancellationToken)
    {
        var request = new RetrieveEntityRequest
        {
            LogicalName = logicalName,
            EntityFilters = EntityFilters.Entity | EntityFilters.Relationships,
        };

        try
        {
            var response = (RetrieveEntityResponse)await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return response.EntityMetadata;
        }
        catch (FaultException<OrganizationServiceFault> ex) when (IsMissingObject(ex))
        {
            return null;
        }
    }

    private static bool IsMissingObject(FaultException<OrganizationServiceFault> exception)
    {
        const int ObjectNotFound = unchecked((int)0x8006088A);
        const int DoesNotExist = unchecked((int)0x80040217);
        return exception.Detail?.ErrorCode is ObjectNotFound or DoesNotExist;
    }

    private static void ValidateForeignKeyCoverage(DataverseMetadataChangeSet changeSet, IReadOnlyList<SqlForeignKeyDefinition> foreignKeys)
    {
        var lookupMap = changeSet.Tables
            .SelectMany(table => table.Columns.Where(c => string.Equals(c.Type, "Lookup", StringComparison.OrdinalIgnoreCase)),
                (table, column) => (Table: table.Key, Column: CreateLookupKey(table.Key, column.SqlColumnName ?? column.LogicalName)))
            .Select(pair => pair.Column)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var foreignKeyKeys = foreignKeys
            .SelectMany(fk => fk.ChildColumns.Select(column => CreateLookupKey(fk.ChildTable, column)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in foreignKeys)
        {
            var matches = fk.ChildColumns.Any(column => lookupMap.Contains(CreateLookupKey(fk.ChildTable, column)));
            if (!matches)
            {
                throw new InvalidOperationException($"No Dataverse lookup column mapping was found for SQL foreign key '{fk.Name}' ({fk.ChildTable}.{string.Join(",", fk.ChildColumns)}).");
            }
        }

        foreach (var lookupKey in lookupMap)
        {
            if (!foreignKeyKeys.Contains(lookupKey))
            {
                throw new InvalidOperationException($"Lookup column '{lookupKey}' has no corresponding SQL foreign key definition.");
            }
        }
    }

    private static string CreateLookupKey(string table, string column) => $"{table}|{column}";

    private static async Task ValidateConnectionAsync(ServiceClient client, CancellationToken cancellationToken)
    {
        var response = await client.ExecuteAsync(new OrganizationRequest("WhoAmI"), cancellationToken).ConfigureAwait(false);
        if (response is null || !response.Results.TryGetValue("UserId", out var userIdValue) || userIdValue is not Guid userId || userId == Guid.Empty)
        {
            throw new InvalidOperationException("Unable to validate Dataverse connectivity. The WhoAmI request returned an empty user identifier.");
        }
    }

    private async Task<Guid> EnsurePublisherAsync(ServiceClient client, DataversePublisherDefinition publisherDefinition, IList<string> actions, CancellationToken cancellationToken)
    {
        var query = new QueryExpression("publisher")
        {
            ColumnSet = new ColumnSet("publisherid"),
            Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("uniquename", ConditionOperator.Equal, publisherDefinition.UniqueName),
                },
            },
        };

        var existing = await client.RetrieveMultipleAsync(query, cancellationToken).ConfigureAwait(false);
        if (existing.Entities.Count > 0)
        {
            return existing.Entities[0].Id;
        }

        var publisher = new Entity("publisher")
        {
            ["uniquename"] = publisherDefinition.UniqueName,
            ["friendlyname"] = publisherDefinition.DisplayName,
            ["customizationprefix"] = publisherDefinition.Prefix,
            ["customizationoptionvalueprefix"] = 95000,
            ["description"] = publisherDefinition.DisplayName,
        };

        var publisherId = await client.CreateAsync(publisher, cancellationToken).ConfigureAwait(false);
        actions.Add($"Created Dataverse publisher '{publisherDefinition.DisplayName}'.");
        return publisherId;
    }

    private async Task EnsureSolutionAsync(ServiceClient client, DataverseSolutionDefinition solutionDefinition, Guid publisherId, IList<string> actions, CancellationToken cancellationToken)
    {
        var query = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("solutionid"),
            Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("uniquename", ConditionOperator.Equal, solutionDefinition.UniqueName),
                },
            },
        };

        var existing = await client.RetrieveMultipleAsync(query, cancellationToken).ConfigureAwait(false);
        if (existing.Entities.Count > 0)
        {
            return;
        }

        var solution = new Entity("solution")
        {
            ["uniquename"] = solutionDefinition.UniqueName,
            ["friendlyname"] = solutionDefinition.DisplayName,
            ["publisherid"] = new EntityReference("publisher", publisherId),
            ["version"] = "1.0.0.0",
        };

        if (!string.IsNullOrWhiteSpace(solutionDefinition.Description))
        {
            solution["description"] = solutionDefinition.Description;
        }

        await client.CreateAsync(solution, cancellationToken).ConfigureAwait(false);
        actions.Add($"Created Dataverse solution '{solutionDefinition.DisplayName}'.");
    }

    private async Task EnsureGlobalOptionSetsAsync(ServiceClient client, IReadOnlyList<DataverseOptionSetDefinition> optionSets, IList<string> actions, CancellationToken cancellationToken)
    {
        foreach (var optionSet in optionSets)
        {
            var retrieveRequest = new RetrieveOptionSetRequest
            {
                Name = optionSet.Name,
            };

            try
            {
                await client.ExecuteAsync(retrieveRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                var optionSetMetadata = new OptionSetMetadata
                {
                    Name = optionSet.Name,
                    DisplayName = CreateLabel(optionSet.DisplayName),
                    IsGlobal = string.Equals(optionSet.Type, "Global", StringComparison.OrdinalIgnoreCase),
                };

                foreach (var option in optionSet.Options)
                {
                    optionSetMetadata.Options.Add(new OptionMetadata(CreateLabel(option.Label), option.Value));
                }

                var createRequest = new CreateOptionSetRequest
                {
                    OptionSet = optionSetMetadata,
                };

                await client.ExecuteAsync(createRequest, cancellationToken).ConfigureAwait(false);
                actions.Add($"Created global option set '{optionSet.Name}'.");
            }
        }
    }

    private async Task<EntityMetadata> EnsureTableAsync(ServiceClient client, DataverseTableDefinition table, IList<string> actions, CancellationToken cancellationToken)
    {
        try
        {
            var response = (RetrieveEntityResponse)await client.ExecuteAsync(new RetrieveEntityRequest
            {
                LogicalName = table.LogicalName,
                EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships,
            }, cancellationToken).ConfigureAwait(false);

            await EnsureEntitySettingsAsync(client, response.EntityMetadata, table, actions, cancellationToken).ConfigureAwait(false);
            return response.EntityMetadata;
        }
        catch (FaultException<OrganizationServiceFault> ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            var entityMetadata = new EntityMetadata
            {
                SchemaName = ToSchemaName(table.LogicalName),
                LogicalName = table.LogicalName,
                DisplayName = CreateLabel(table.DisplayName),
                DisplayCollectionName = CreateLabel(table.DisplayName + "s"),
                Description = string.IsNullOrWhiteSpace(table.Description) ? null : CreateLabel(table.Description!),
                OwnershipType = ParseOwnershipType(table.OwnershipType),
                IsActivity = false,
                HasNotes = false,
                HasActivities = false,
                IsAuditEnabled = new BooleanManagedProperty(table.AuditingEnabled),
                ChangeTrackingEnabled = table.ChangeTrackingEnabled,
            };

            var primaryColumn = table.Columns.FirstOrDefault(c => string.Equals(c.LogicalName, table.PrimaryNameAttribute, StringComparison.OrdinalIgnoreCase));
            if (primaryColumn is null)
            {
                throw new InvalidOperationException($"The table definition '{table.Key}' is missing a column for the primary name attribute '{table.PrimaryNameAttribute}'.");
            }

            var createRequest = new CreateEntityRequest
            {
                HasActivities = false,
                HasNotes = false,
                Entity = entityMetadata,
                PrimaryAttribute = (StringAttributeMetadata)CreateAttributeMetadata(primaryColumn),
            };

            await client.ExecuteAsync(createRequest, cancellationToken).ConfigureAwait(false);
            actions.Add($"Created Dataverse table '{table.LogicalName}'.");

            var response = (RetrieveEntityResponse)await client.ExecuteAsync(new RetrieveEntityRequest
            {
                LogicalName = table.LogicalName,
                EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships,
            }, cancellationToken).ConfigureAwait(false);

            await EnsureEntitySettingsAsync(client, response.EntityMetadata, table, actions, cancellationToken).ConfigureAwait(false);
            return response.EntityMetadata;
        }
    }

    private async Task EnsureEntitySettingsAsync(ServiceClient client, EntityMetadata metadata, DataverseTableDefinition table, IList<string> actions, CancellationToken cancellationToken)
    {
        var requiresUpdate = (metadata.ChangeTrackingEnabled ?? false) != table.ChangeTrackingEnabled || (metadata.IsAuditEnabled?.Value ?? false) != table.AuditingEnabled;
        if (!requiresUpdate)
        {
            return;
        }

        metadata.ChangeTrackingEnabled = table.ChangeTrackingEnabled;
        metadata.IsAuditEnabled = new BooleanManagedProperty(table.AuditingEnabled);

        var updateRequest = new UpdateEntityRequest
        {
            Entity = metadata,
        };

        await client.ExecuteAsync(updateRequest, cancellationToken).ConfigureAwait(false);
        actions.Add($"Updated Dataverse table '{table.LogicalName}' settings.");
    }

    private async Task EnsureAttributesAsync(ServiceClient client, DataverseTableDefinition table, EntityMetadata metadata, string solutionUniqueName, IList<string> actions, CancellationToken cancellationToken)
    {
        foreach (var column in table.Columns)
        {
            if (string.Equals(column.LogicalName, table.PrimaryNameAttribute, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(column.Type, "Lookup", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var existing = metadata.Attributes.FirstOrDefault(a => string.Equals(a.LogicalName, column.LogicalName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                continue;
            }

            var attributeMetadata = CreateAttributeMetadata(column);
            var createAttribute = new CreateAttributeRequest
            {
                EntityName = table.LogicalName,
                Attribute = attributeMetadata,
                SolutionUniqueName = solutionUniqueName,
            };

            await client.ExecuteAsync(createAttribute, cancellationToken).ConfigureAwait(false);
            actions.Add($"Created column '{column.LogicalName}' on table '{table.LogicalName}'.");
        }
    }

    private async Task EnsureAlternateKeysAsync(ServiceClient client, DataverseTableDefinition table, EntityMetadata metadata, string solutionUniqueName, IList<string> actions, CancellationToken cancellationToken)
    {
        var keys = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in table.Columns.Where(c => c.IsAlternateKey && !string.IsNullOrWhiteSpace(c.AlternateKeyName)))
        {
            var keyName = column.AlternateKeyName!;
            if (!keys.TryGetValue(keyName, out var columns))
            {
                columns = new List<string>();
                keys[keyName] = columns;
            }

            columns.Add(column.LogicalName);
        }

        foreach (var key in table.AlternateKeys)
        {
            if (!keys.ContainsKey(key.Name))
            {
                keys[key.Name] = new List<string>(key.Fields);
            }
        }

        foreach (var (keyName, columns) in keys)
        {
            var existing = metadata.Keys.FirstOrDefault(k => string.Equals(k.LogicalName, keyName, StringComparison.OrdinalIgnoreCase) || string.Equals(k.SchemaName, keyName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null && existing.EntityKeyIndexStatus == EntityKeyIndexStatus.Active)
            {
                continue;
            }

            if (existing is null)
            {
                var createKey = new CreateEntityKeyRequest
                {
                    EntityName = table.LogicalName,
                    EntityKey = new EntityKeyMetadata
                    {
                        DisplayName = CreateLabel(keyName),
                        SchemaName = keyName,
                        LogicalName = keyName,
                        KeyAttributes = columns.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    },
                    SolutionUniqueName = solutionUniqueName,
                };

                await client.ExecuteAsync(createKey, cancellationToken).ConfigureAwait(false);
                actions.Add($"Created alternate key '{keyName}' on '{table.LogicalName}'.");
            }
            else if (existing.EntityKeyIndexStatus != EntityKeyIndexStatus.Active)
            {
                var activate = new OrganizationRequest("PublishDuplicateRule")
                {
                    ["EntityName"] = table.LogicalName,
                    ["EntityKeyName"] = existing.LogicalName,
                };

                await client.ExecuteAsync(activate, cancellationToken).ConfigureAwait(false);
                actions.Add($"Activated alternate key '{existing.LogicalName}' on '{table.LogicalName}'.");
            }
        }
    }

    private async Task EnsureRelationshipsAsync(ServiceClient client, IReadOnlyList<DataverseTableDefinition> tables, IReadOnlyList<SqlForeignKeyDefinition> foreignKeys, string solutionUniqueName, IList<string> actions, CancellationToken cancellationToken)
    {
        var tableMap = tables.ToDictionary(t => t.Key, t => t, StringComparer.OrdinalIgnoreCase);
        var foreignKeyMap = new Dictionary<string, SqlForeignKeyDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in foreignKeys)
        {
            foreach (var column in fk.ChildColumns)
            {
                foreignKeyMap[CreateLookupKey(fk.ChildTable, column)] = fk;
            }
        }

        foreach (var table in tables)
        {
            var referencingMetadata = (RetrieveEntityResponse)await client.ExecuteAsync(new RetrieveEntityRequest
            {
                LogicalName = table.LogicalName,
                EntityFilters = EntityFilters.Relationships,
            }, cancellationToken).ConfigureAwait(false);

            foreach (var lookupColumn in table.Columns.Where(c => string.Equals(c.Type, "Lookup", StringComparison.OrdinalIgnoreCase)))
            {
                var lookupKey = CreateLookupKey(table.Key, lookupColumn.SqlColumnName ?? lookupColumn.LogicalName);
                if (!foreignKeyMap.TryGetValue(lookupKey, out var matchingFk))
                {
                    _logger.LogWarning("No SQL foreign key mapping found for lookup column {Lookup} on table {Table}.", lookupColumn.LogicalName, table.LogicalName);
                    continue;
                }

                var relationship = referencingMetadata.EntityMetadata.ManyToOneRelationships.FirstOrDefault(r => string.Equals(r.ReferencingAttribute, lookupColumn.LogicalName, StringComparison.OrdinalIgnoreCase));
                if (relationship is null)
                {
                    await CreateLookupRelationshipAsync(client, table, lookupColumn, tableMap, matchingFk, solutionUniqueName, actions, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await EnsureRelationshipCascadeAsync(client, relationship, lookupColumn, matchingFk, actions, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task CreateLookupRelationshipAsync(ServiceClient client, DataverseTableDefinition table, DataverseColumnDefinition lookupColumn, IReadOnlyDictionary<string, DataverseTableDefinition> tableMap, SqlForeignKeyDefinition foreignKey, string solutionUniqueName, IList<string> actions, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(lookupColumn.TargetTable))
        {
            throw new InvalidOperationException($"Lookup column '{lookupColumn.LogicalName}' on '{table.LogicalName}' is missing a target table definition.");
        }

        if (!tableMap.TryGetValue(foreignKey.ParentTable, out var referencedTable))
        {
            throw new InvalidOperationException($"The SQL foreign key '{foreignKey.Name}' references table '{foreignKey.ParentTable}', which has no Dataverse mapping.");
        }

        var lookupMetadata = new LookupAttributeMetadata
        {
            LogicalName = lookupColumn.LogicalName,
            SchemaName = ToSchemaName(lookupColumn.LogicalName),
            DisplayName = CreateLabel(lookupColumn.DisplayName),
            Description = CreateLabel($"Lookup to {lookupColumn.TargetTable}"),
            RequiredLevel = new AttributeRequiredLevelManagedProperty(ParseRequiredLevel(lookupColumn.RequiredLevel)),
            Targets = new[] { lookupColumn.TargetTable },
        };

        var relationshipMetadata = new OneToManyRelationshipMetadata
        {
            ReferencedEntity = lookupColumn.TargetTable,
            ReferencedAttribute = referencedTable.PrimaryIdAttribute,
            ReferencingEntity = table.LogicalName,
            ReferencingAttribute = lookupColumn.LogicalName,
            SchemaName = ToSchemaName(lookupColumn.LogicalName + "_relationship"),
            CascadeConfiguration = CreateCascadeConfiguration(foreignKey.DeleteBehavior ?? lookupColumn.DeleteBehavior),
        };

        var request = new CreateOneToManyRequest
        {
            Lookup = lookupMetadata,
            OneToManyRelationship = relationshipMetadata,
            SolutionUniqueName = solutionUniqueName,
        };

        await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        actions.Add($"Created relationship for '{table.LogicalName}.{lookupColumn.LogicalName}'.");
    }

    private async Task EnsureRelationshipCascadeAsync(ServiceClient client, OneToManyRelationshipMetadata relationship, DataverseColumnDefinition lookupColumn, SqlForeignKeyDefinition foreignKey, IList<string> actions, CancellationToken cancellationToken)
    {
        var desiredCascade = CreateCascadeConfiguration(foreignKey.DeleteBehavior ?? lookupColumn.DeleteBehavior);
        if (relationship.CascadeConfiguration is null || relationship.CascadeConfiguration.Delete != desiredCascade.Delete)
        {
            relationship.CascadeConfiguration ??= new CascadeConfiguration();
            relationship.CascadeConfiguration.Delete = desiredCascade.Delete;

            var update = new UpdateRelationshipRequest
            {
                Relationship = relationship,
            };

            await client.ExecuteAsync(update, cancellationToken).ConfigureAwait(false);
            actions.Add($"Updated delete behavior for relationship '{relationship.SchemaName}'.");
        }
    }

    private static CascadeConfiguration CreateCascadeConfiguration(string? deleteBehavior)
    {
        var cascade = new CascadeConfiguration
        {
            Assign = CascadeType.NoCascade,
            Share = CascadeType.NoCascade,
            Unshare = CascadeType.NoCascade,
            Merge = CascadeType.NoCascade,
            Reparent = CascadeType.NoCascade,
        };

        cascade.Delete = deleteBehavior?.ToUpperInvariant() switch
        {
            "CASCADE" => CascadeType.Cascade,
            "SET NULL" => CascadeType.RemoveLink,
            "REMOVE LINK" => CascadeType.RemoveLink,
            "RESTRICT" => CascadeType.Restrict,
            "NO ACTION" => CascadeType.Restrict,
            _ => CascadeType.Restrict,
        };

        return cascade;
    }

    private static AttributeMetadata CreateAttributeMetadata(DataverseColumnDefinition column)
    {
        AttributeMetadata metadata = column.Type switch
        {
            "WholeNumber" => new IntegerAttributeMetadata
            {
                MinValue = column.MinValue.HasValue ? (int)column.MinValue.Value : (int?)null,
                MaxValue = column.MaxValue.HasValue ? (int)column.MaxValue.Value : (int?)null,
                Format = IntegerFormat.None,
            },
            "Decimal" => new DecimalAttributeMetadata
            {
                Precision = column.Scale ?? 2,
            },
            "Money" => new MoneyAttributeMetadata
            {
                Precision = column.Scale ?? 2,
            },
            "Boolean" => new BooleanAttributeMetadata
            {
                OptionSet = new BooleanOptionSetMetadata(new OptionMetadata(CreateLabel("Yes"), 1), new OptionMetadata(CreateLabel("No"), 0)),
            },
            "SingleLine.Text" => new StringAttributeMetadata
            {
                MaxLength = column.MaxLength ?? 200,
                Format = StringFormat.Text,
            },
            "SingleLine.Email" => new StringAttributeMetadata
            {
                MaxLength = column.MaxLength ?? 254,
                Format = StringFormat.Email,
            },
            "MultipleLines.Text" => new MemoAttributeMetadata
            {
                Format = StringFormat.Text,
                MaxLength = column.MaxLength ?? 2000,
            },
            "Choice" => new PicklistAttributeMetadata
            {
                OptionSet = new OptionSetMetadata
                {
                    IsGlobal = true,
                    Name = column.OptionSetName,
                },
            },
            "DateTime" => new DateTimeAttributeMetadata
            {
                Format = ParseDateTimeFormat(column.DateTimeBehavior),
                DateTimeBehavior = ParseDateTimeBehavior(column.DateTimeBehavior),
            },
            _ => new StringAttributeMetadata
            {
                MaxLength = column.MaxLength ?? 200,
                Format = StringFormat.Text,
            },
        };

        metadata.LogicalName = column.LogicalName;
        metadata.SchemaName = ToSchemaName(column.LogicalName);
        metadata.DisplayName = CreateLabel(column.DisplayName);
        metadata.RequiredLevel = new AttributeRequiredLevelManagedProperty(ParseRequiredLevel(column.RequiredLevel));
        metadata.Description = CreateLabel(column.DisplayName);
        return metadata;
    }

    private static AttributeRequiredLevel ParseRequiredLevel(string? requiredLevel)
    {
        if (string.IsNullOrWhiteSpace(requiredLevel))
        {
            return AttributeRequiredLevel.None;
        }

        return Enum.TryParse<AttributeRequiredLevel>(requiredLevel.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase), true, out var parsed)
            ? parsed
            : AttributeRequiredLevel.None;
    }

    private static DateTimeFormat ParseDateTimeFormat(string? behavior)
    {
        return behavior?.ToUpperInvariant() switch
        {
            "DATEONLY" => DateTimeFormat.DateOnly,
            _ => DateTimeFormat.DateAndTime,
        };
    }

    private static DateTimeBehavior ParseDateTimeBehavior(string? behavior)
    {
        return behavior?.ToUpperInvariant() switch
        {
            "DATEONLY" => DateTimeBehavior.DateOnly,
            "USERLOCAL" => DateTimeBehavior.UserLocal,
            _ => DateTimeBehavior.UserLocal,
        };
    }

    private static OwnershipTypes ParseOwnershipType(string ownershipType)
    {
        return ownershipType?.ToUpperInvariant() switch
        {
            "USEROWNED" => OwnershipTypes.UserOwned,
            "ORGANIZATIONOWNED" => OwnershipTypes.OrganizationOwned,
            _ => OwnershipTypes.UserOwned,
        };
    }

    private static string ToSchemaName(string logicalName)
    {
        var parts = logicalName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return logicalName;
        }

        var prefix = parts[0];
        var suffix = string.Concat(parts.Skip(1).Select(p => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(p.ToLowerInvariant())));
        return parts.Length == 1 ? prefix : prefix + "_" + suffix;
    }

    private static Label CreateLabel(string text)
    {
        return new Label(text, EnglishLcid);
    }
}
