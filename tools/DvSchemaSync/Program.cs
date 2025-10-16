using DvSchemaSync.Alignment;
using DvSchemaSync.Configuration;
using DvSchemaSync.Dataverse;
using System.Linq;
using System.Text.Json;
using DvSchemaSync.Planning;
using DvSchemaSync.Sql;

var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cancellationTokenSource.Cancel();
};

try
{
    var options = ExecutionOptions.Parse(args);
    var parser = new SqlSchemaParser();
    var schema = parser.ParseFromFile(options.SqlSchemaPath);

    var tableMap = BuildTableMap(schema, options.TableToEntityMap);

    var connectionSettings = new DataverseConnectionSettings(
        options.DataverseOrgUrl,
        options.DataverseClientId,
        options.DataverseClientSecret,
        options.DataverseTenantId);

    var metadataProvider = new DataverseMetadataProvider(connectionSettings);

    var metadataResult = await metadataProvider.LoadMetadataAsync(tableMap.Values, cancellationTokenSource.Token).ConfigureAwait(false);

    var analyzer = new AlignmentAnalyzer(NativeFieldMappings.SqlToDataverse);
    var analysis = analyzer.Analyze(schema, metadataResult, tableMap);
    var planner = new SchemaChangePlanner(NativeFieldMappings.SqlToDataverse);
    var changePlan = planner.BuildPlan(analysis);

    var writer = new AlignmentReportWriter(NativeFieldMappings.SqlToDataverse);
    writer.Write(analysis, metadataResult, options.OutputPath);

    Console.WriteLine($"Alignment report written to {options.OutputPath}.");
    if (!metadataResult.ConnectionAvailable)
    {
        Console.WriteLine("Dataverse metadata could not be retrieved; report contains SQL-only details.");
    }

    PrintPlanSummary(changePlan);

    if (options.DryRun)
    {
        WriteDeleteCandidates(changePlan, options.DeleteCandidatesPath);
        Console.WriteLine($"Dry-run complete. Deletion candidates written to {options.DeleteCandidatesPath}.");
    }

    if (options.ApplyChanges)
    {
        if (!metadataResult.ConnectionAvailable)
        {
            throw new InvalidOperationException("Cannot apply schema changes without a Dataverse connection.");
        }

        var applier = new DataverseSchemaApplier(connectionSettings);
        await applier.ApplyAsync(changePlan, options, metadataResult, cancellationTokenSource.Token).ConfigureAwait(false);
    }

    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operation cancelled.");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static IReadOnlyDictionary<string, string> BuildTableMap(SqlSchema schema, IReadOnlyDictionary<string, string> overrides)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var table in schema.Tables.Values)
    {
        if (overrides.TryGetValue(table.Name, out var mapped))
        {
            map[table.Name] = mapped;
        }
        else
        {
            map[table.Name] = table.Name;
        }
    }

    return map;
}

static void PrintPlanSummary(SchemaChangePlan plan)
{
    var summary = plan.CreateSummary();
    Console.WriteLine("Schema change summary:");
    Console.WriteLine($"  Attributes to add: {summary.AttributesToAdd}");
    Console.WriteLine($"  Native replacements to adopt: {summary.NativeReplacements}");
    Console.WriteLine($"  Alternate keys to add: {summary.AlternateKeysToAdd}");
    Console.WriteLine($"  Relationships to add: {summary.RelationshipsToAdd}");
    Console.WriteLine($"  Attributes to remove: {summary.AttributesToRemove}");
    Console.WriteLine($"  Alternate keys to remove: {summary.AlternateKeysToRemove}");
    Console.WriteLine($"  Relationships to remove: {summary.RelationshipsToRemove}");
    Console.WriteLine($"  Entities missing in Dataverse: {summary.MissingEntities}");

    if (!plan.HasChanges)
    {
        Console.WriteLine("No schema changes are required.");
    }
}

static void WriteDeleteCandidates(SchemaChangePlan plan, string deleteCandidatesPath)
{
    var directory = Path.GetDirectoryName(deleteCandidatesPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var payload = new
    {
        generatedAtUtc = DateTime.UtcNow,
        attributes = plan.AttributesToRemove.Select(attribute => new
        {
            entity = attribute.EntityLogicalName,
            logicalName = attribute.Attribute.LogicalName,
            schemaName = attribute.Attribute.SchemaName,
            displayName = attribute.Attribute.DisplayName,
            attributeType = attribute.Attribute.AttributeType,
            targets = attribute.Attribute.Targets,
            attribute.Reason
        }),
        relationships = plan.RelationshipsToRemove.Select(rel => new
        {
            entity = rel.EntityLogicalName,
            rel.Relationship.SchemaName,
            rel.Relationship.ReferencingEntity,
            rel.Relationship.ReferencingAttribute,
            rel.Relationship.ReferencedEntity,
            rel.Relationship.ReferencedAttribute
        }),
        alternateKeys = plan.AlternateKeysToRemove.Select(key => new
        {
            entity = key.EntityLogicalName,
            key.Key.LogicalName,
            key.Key.KeyAttributes
        }),
        missingEntities = plan.MissingEntities.Select(entity => new
        {
            entity.TableName,
            entity.ExpectedEntityLogicalName
        })
    };

    using var stream = File.Create(deleteCandidatesPath);
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };
    JsonSerializer.Serialize(stream, payload, jsonOptions);
}
