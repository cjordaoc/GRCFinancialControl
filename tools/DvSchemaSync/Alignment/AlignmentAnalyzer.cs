using DvSchemaSync.Configuration;
using DvSchemaSync.Dataverse;
using DvSchemaSync.Sql;
using System.Linq;

namespace DvSchemaSync.Alignment;

internal sealed class AlignmentAnalyzer
{
    private readonly IReadOnlyDictionary<string, string> _nativeFieldMap;

    public AlignmentAnalyzer(IReadOnlyDictionary<string, string> nativeFieldMap)
    {
        _nativeFieldMap = nativeFieldMap;
    }

    public AlignmentAnalysis Analyze(SqlSchema schema, DataverseMetadataResult metadataResult, IReadOnlyDictionary<string, string> tableToEntityMap)
    {
        var tableResults = new List<TableAlignmentResult>();

        foreach (var table in schema.Tables.Values.OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase))
        {
            tableToEntityMap.TryGetValue(table.Name, out var mappedEntityName);
            var entityName = mappedEntityName ?? table.Name;

            metadataResult.Entities.TryGetValue(entityName, out var entityMetadata);
            var result = AnalyzeTable(table, entityName, entityMetadata);
            tableResults.Add(result);
        }

        return new AlignmentAnalysis(DateTime.UtcNow, tableToEntityMap, tableResults);
    }

    private TableAlignmentResult AnalyzeTable(SqlTable table, string? entityName, DataverseEntityMetadata? entityMetadata)
    {
        var columnAlignments = new List<ColumnAlignmentResult>();
        var unmatchedAttributes = entityMetadata?.Attributes is null
            ? new List<DataverseAttributeMetadata>()
            : entityMetadata.Attributes.Values.ToList();

        foreach (var column in table.Columns)
        {
            if (entityMetadata is null)
            {
                columnAlignments.Add(new ColumnAlignmentResult(column, ColumnAlignmentStatus.MissingInDataverse, null, "Dataverse entity not available."));
                continue;
            }

            DataverseAttributeMetadata? matchedAttribute = null;
            string? note = null;
            ColumnAlignmentStatus status;

            if (entityMetadata.Attributes.TryGetValue(column.Name, out var attribute))
            {
                matchedAttribute = attribute;
                status = ColumnAlignmentStatus.Matched;
                unmatchedAttributes.Remove(attribute);
            }
            else if (_nativeFieldMap.TryGetValue(column.Name, out var nativeLogicalName) &&
                     entityMetadata.Attributes.TryGetValue(nativeLogicalName, out attribute))
            {
                matchedAttribute = attribute;
                status = ColumnAlignmentStatus.NativeReplacement;
                note = $"Use native Dataverse attribute '{nativeLogicalName}'.";
                unmatchedAttributes.Remove(attribute);
            }
            else
            {
                status = ColumnAlignmentStatus.MissingInDataverse;
                note = "No matching Dataverse attribute found.";
            }

            columnAlignments.Add(new ColumnAlignmentResult(column, status, matchedAttribute, note));
        }

        var keyAlignments = AnalyzeKeys(table, entityMetadata);
        var foreignKeyAlignments = AnalyzeForeignKeys(table, entityMetadata);
        var unmatchedAlternateKeys = GetUnmatchedAlternateKeys(table, entityMetadata);
        var unmatchedRelationships = GetUnmatchedRelationships(table, entityMetadata, foreignKeyAlignments);

        return new TableAlignmentResult(
            table,
            entityName,
            entityMetadata,
            columnAlignments,
            keyAlignments,
            foreignKeyAlignments,
            unmatchedAttributes,
            unmatchedAlternateKeys,
            unmatchedRelationships);
    }

    private static IReadOnlyList<KeyAlignmentResult> AnalyzeKeys(SqlTable table, DataverseEntityMetadata? entityMetadata)
    {
        var results = new List<KeyAlignmentResult>();

        if (entityMetadata is null)
        {
            if (table.PrimaryKey is not null)
            {
                results.Add(new KeyAlignmentResult(table.PrimaryKey.Name ?? "PRIMARY", table.PrimaryKey.Columns, Array.Empty<string>(), KeyAlignmentStatus.MissingInDataverse));
            }

            foreach (var unique in table.UniqueKeys)
            {
                results.Add(new KeyAlignmentResult(unique.Name, unique.Columns, Array.Empty<string>(), KeyAlignmentStatus.MissingInDataverse));
            }

            return results;
        }

        if (table.PrimaryKey is not null)
        {
            var primaryColumns = table.PrimaryKey.Columns;
            var dataversePrimary = entityMetadata.PrimaryIdAttribute;
            var status = primaryColumns.Count == 1 && primaryColumns[0].Equals(dataversePrimary, StringComparison.OrdinalIgnoreCase)
                ? KeyAlignmentStatus.Matched
                : KeyAlignmentStatus.MissingInDataverse;

            results.Add(new KeyAlignmentResult(table.PrimaryKey.Name ?? "PRIMARY", primaryColumns, new[] { dataversePrimary }, status));
        }

        foreach (var unique in table.UniqueKeys)
        {
            var match = entityMetadata.AlternateKeys.FirstOrDefault(key =>
                key.KeyAttributes.Count == unique.Columns.Count &&
                !key.KeyAttributes.Except(unique.Columns, StringComparer.OrdinalIgnoreCase).Any());

            if (match is not null)
            {
                results.Add(new KeyAlignmentResult(unique.Name, unique.Columns, match.KeyAttributes, KeyAlignmentStatus.Matched));
            }
            else
            {
                results.Add(new KeyAlignmentResult(unique.Name, unique.Columns, Array.Empty<string>(), KeyAlignmentStatus.MissingInDataverse));
            }
        }

        foreach (var dvKey in entityMetadata.AlternateKeys)
        {
            var hasMatch = table.UniqueKeys.Any(unique =>
                unique.Columns.Count == dvKey.KeyAttributes.Count &&
                !unique.Columns.Except(dvKey.KeyAttributes, StringComparer.OrdinalIgnoreCase).Any());

            if (!hasMatch)
            {
                results.Add(new KeyAlignmentResult(dvKey.LogicalName, Array.Empty<string>(), dvKey.KeyAttributes, KeyAlignmentStatus.AdditionalInDataverse));
            }
        }

        return results;
    }

    private static IReadOnlyList<ForeignKeyAlignmentResult> AnalyzeForeignKeys(SqlTable table, DataverseEntityMetadata? entityMetadata)
    {
        if (entityMetadata is null)
        {
            return table.ForeignKeys
                .Select(fk => new ForeignKeyAlignmentResult(fk, ForeignKeyAlignmentStatus.MissingInDataverse, null, null))
                .ToArray();
        }

        var results = new List<ForeignKeyAlignmentResult>();
        foreach (var foreignKey in table.ForeignKeys)
        {
            DataverseRelationshipMetadata? match = null;

            if (foreignKey.Columns.Count == 1)
            {
                match = entityMetadata.ManyToOneRelationships.FirstOrDefault(rel =>
                    rel.ReferencingAttribute.Equals(foreignKey.Columns[0], StringComparison.OrdinalIgnoreCase));
            }

            if (match is not null)
            {
                results.Add(new ForeignKeyAlignmentResult(
                    foreignKey,
                    ForeignKeyAlignmentStatus.Matched,
                    match.SchemaName,
                    match.ReferencingAttribute));
            }
            else
            {
                results.Add(new ForeignKeyAlignmentResult(foreignKey, ForeignKeyAlignmentStatus.MissingInDataverse, null, null));
            }
        }

        return results;
    }

    private static IReadOnlyList<DataverseAlternateKey> GetUnmatchedAlternateKeys(SqlTable table, DataverseEntityMetadata? entityMetadata)
    {
        if (entityMetadata is null)
        {
            return Array.Empty<DataverseAlternateKey>();
        }

        return entityMetadata.AlternateKeys
            .Where(key => table.UniqueKeys.All(unique =>
                unique.Columns.Count != key.KeyAttributes.Count ||
                unique.Columns.Except(key.KeyAttributes, StringComparer.OrdinalIgnoreCase).Any()))
            .ToArray();
    }

    private static IReadOnlyList<DataverseRelationshipMetadata> GetUnmatchedRelationships(SqlTable table, DataverseEntityMetadata? entityMetadata, IReadOnlyList<ForeignKeyAlignmentResult> foreignKeyResults)
    {
        if (entityMetadata is null)
        {
            return Array.Empty<DataverseRelationshipMetadata>();
        }

        var matchedRelationshipSchemaNames = new HashSet<string>(foreignKeyResults
            .Where(result => result.Status == ForeignKeyAlignmentStatus.Matched && result.MatchedRelationshipSchemaName is not null)
            .Select(result => result.MatchedRelationshipSchemaName!),
            StringComparer.OrdinalIgnoreCase);

        return entityMetadata.ManyToOneRelationships
            .Where(rel => !matchedRelationshipSchemaNames.Contains(rel.SchemaName))
            .ToArray();
    }
}
