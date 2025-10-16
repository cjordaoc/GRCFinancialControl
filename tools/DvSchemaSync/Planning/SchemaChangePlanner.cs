using System.Linq;
using DvSchemaSync.Alignment;
using DvSchemaSync.Dataverse;

namespace DvSchemaSync.Planning;

internal sealed class SchemaChangePlanner
{
    private readonly IReadOnlyDictionary<string, string> _nativeFieldMap;
    private readonly HashSet<string> _nativeLogicalNames;

    public SchemaChangePlanner(IReadOnlyDictionary<string, string> nativeFieldMap)
    {
        _nativeFieldMap = nativeFieldMap;
        _nativeLogicalNames = new HashSet<string>(nativeFieldMap.Values, StringComparer.OrdinalIgnoreCase);
    }

    public SchemaChangePlan BuildPlan(AlignmentAnalysis analysis)
    {
        var attributesToAdd = new List<PlannedAttributeAddition>();
        var nativeReplacements = new List<NativeFieldReplacement>();
        var alternateKeysToAdd = new List<PlannedAlternateKeyAddition>();
        var relationshipsToAdd = new List<PlannedRelationshipAddition>();
        var attributesToRemove = new List<PlannedAttributeRemoval>();
        var alternateKeysToRemove = new List<PlannedAlternateKeyRemoval>();
        var relationshipsToRemove = new List<PlannedRelationshipRemoval>();
        var missingEntities = new List<MissingEntityPlan>();

        foreach (var table in analysis.Tables)
        {
            var entityName = table.DataverseEntityName ??
                             (analysis.TableToEntityMap.TryGetValue(table.Table.Name, out var mapped) ? mapped : table.Table.Name);

            if (table.DataverseEntity is null)
            {
                missingEntities.Add(new MissingEntityPlan(table.Table.Name, entityName));
            }

            foreach (var columnAlignment in table.Columns)
            {
                switch (columnAlignment.Status)
                {
                    case ColumnAlignmentStatus.MissingInDataverse:
                        attributesToAdd.Add(new PlannedAttributeAddition(table.Table.Name, entityName, columnAlignment.Column));
                        break;
                    case ColumnAlignmentStatus.NativeReplacement when columnAlignment.DataverseAttribute is not null:
                        nativeReplacements.Add(new NativeFieldReplacement(
                            table.Table.Name,
                            entityName,
                            columnAlignment.Column.Name,
                            columnAlignment.DataverseAttribute.LogicalName));
                        break;
                }
            }

            if (table.DataverseEntity is not null)
            {
                foreach (var attribute in table.UnmatchedDataverseAttributes)
                {
                    if (!ShouldProposeAttributeRemoval(table, attribute))
                    {
                        continue;
                    }

                    attributesToRemove.Add(new PlannedAttributeRemoval(
                        table.DataverseEntity.LogicalName,
                        attribute,
                        "Custom attribute is not present in the MySQL schema."));
                }

                foreach (var relationship in table.UnmatchedRelationships)
                {
                    if (!relationship.IsCustomRelationship)
                    {
                        continue;
                    }

                    relationshipsToRemove.Add(new PlannedRelationshipRemoval(table.DataverseEntity.LogicalName, relationship));
                }

                foreach (var key in table.UnmatchedDataverseAlternateKeys)
                {
                    alternateKeysToRemove.Add(new PlannedAlternateKeyRemoval(table.DataverseEntity.LogicalName, key));
                }
            }

            foreach (var keyAlignment in table.Keys)
            {
                if (keyAlignment.Status == KeyAlignmentStatus.MissingInDataverse && keyAlignment.SqlColumns.Count > 0)
                {
                    alternateKeysToAdd.Add(new PlannedAlternateKeyAddition(
                        table.Table.Name,
                        entityName,
                        keyAlignment.Name,
                        keyAlignment.SqlColumns));
                }
            }

            foreach (var foreignKeyAlignment in table.ForeignKeys)
            {
                if (foreignKeyAlignment.Status != ForeignKeyAlignmentStatus.MissingInDataverse)
                {
                    continue;
                }

                if (foreignKeyAlignment.ForeignKey.Columns.Count != 1)
                {
                    continue;
                }

                var referencedEntity = analysis.TableToEntityMap.TryGetValue(foreignKeyAlignment.ForeignKey.ReferencedTable, out var mappedEntity)
                    ? mappedEntity
                    : foreignKeyAlignment.ForeignKey.ReferencedTable;

                relationshipsToAdd.Add(new PlannedRelationshipAddition(
                    table.Table.Name,
                    entityName,
                    referencedEntity,
                    foreignKeyAlignment.ForeignKey));
            }
        }

        return new SchemaChangePlan(
            attributesToAdd,
            nativeReplacements,
            alternateKeysToAdd,
            relationshipsToAdd,
            attributesToRemove,
            alternateKeysToRemove,
            relationshipsToRemove,
            missingEntities);
    }

    private bool ShouldProposeAttributeRemoval(TableAlignmentResult table, DataverseAttributeMetadata attribute)
    {
        if (!attribute.IsCustomAttribute)
        {
            return false;
        }

        if (_nativeLogicalNames.Contains(attribute.LogicalName))
        {
            return false;
        }

        if (table.DataverseEntity is not null &&
            string.Equals(attribute.LogicalName, table.DataverseEntity.PrimaryIdAttribute, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (table.ForeignKeys.Any(fk =>
                fk.Status == ForeignKeyAlignmentStatus.Matched &&
                fk.MatchedReferencingAttribute is not null &&
                string.Equals(fk.MatchedReferencingAttribute, attribute.LogicalName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (attribute.Targets.Count > 0)
        {
            // Lookup attributes may participate in relationships that are not represented in SQL.
            return false;
        }

        return true;
    }
}
