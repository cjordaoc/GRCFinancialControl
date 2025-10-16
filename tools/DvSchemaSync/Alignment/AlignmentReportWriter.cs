using System.IO;
using System.Linq;
using System.Text;
using DvSchemaSync.Configuration;
using DvSchemaSync.Dataverse;

namespace DvSchemaSync.Alignment;

internal sealed class AlignmentReportWriter
{
    private readonly IReadOnlyDictionary<string, string> _nativeFieldMap;

    public AlignmentReportWriter(IReadOnlyDictionary<string, string> nativeFieldMap)
    {
        _nativeFieldMap = nativeFieldMap;
    }

    public void Write(AlignmentAnalysis analysis, DataverseMetadataResult metadataResult, string outputPath)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Dataverse Alignment Report");
        builder.AppendLine();
        builder.AppendLine($"Generated: {analysis.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine($"Source schema: `{analysis.TableToEntityMap.Count}` tables parsed from artifacts/mysql/rebuild_schema.sql");
        builder.AppendLine($"Dataverse org URL: {metadataResult.OrgUrl ?? "(not configured)"}");
        builder.AppendLine($"Dataverse connection: {(metadataResult.ConnectionAvailable ? "available" : "not available")}");

        if (metadataResult.Errors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Connection Warnings");
            foreach (var error in metadataResult.Errors)
            {
                builder.AppendLine($"- {error}");
            }
        }

        if (_nativeFieldMap.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Native Field Replacement Map");
            foreach (var pair in _nativeFieldMap)
            {
                builder.AppendLine($"- `{pair.Key}` → `{pair.Value}`");
            }
        }

        foreach (var tableResult in analysis.Tables)
        {
            AppendTableSection(builder, tableResult);
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, builder.ToString());
    }

    private static void AppendTableSection(StringBuilder builder, TableAlignmentResult tableResult)
    {
        builder.AppendLine();
        builder.AppendLine($"## Table `{tableResult.Table.Name}`");
        builder.AppendLine();
        builder.AppendLine($"- MySQL table: `{tableResult.Table.Name}`");
        builder.AppendLine($"- Dataverse entity: `{tableResult.DataverseEntityName ?? "(not specified)"}`");
        builder.AppendLine($"- Dataverse entity status: {(tableResult.DataverseEntity is null ? "missing" : "retrieved")}");

        builder.AppendLine();
        builder.AppendLine("### Column Alignment");
        builder.AppendLine();
        builder.AppendLine("| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

        foreach (var column in tableResult.Columns)
        {
            var attribute = column.DataverseAttribute;
            var alignment = column.Status switch
            {
                ColumnAlignmentStatus.Matched => "Matched",
                ColumnAlignmentStatus.NativeReplacement => "Use native",
                ColumnAlignmentStatus.MissingInDataverse => "Missing",
                _ => column.Status.ToString()
            };

            var dataverseName = attribute?.LogicalName ?? string.Empty;
            var dataverseType = attribute?.AttributeType ?? string.Empty;
            var nullable = column.Column.IsNullable ? "Yes" : "No";
            var notes = column.Notes ?? string.Empty;

            builder.AppendLine($"| `{column.Column.Name}` | `{column.Column.DataType}` | {nullable} | {alignment} | `{dataverseName}` | `{dataverseType}` | {notes} |");
        }

        builder.AppendLine();
        builder.AppendLine("### Key Alignment");
        builder.AppendLine();
        builder.AppendLine("| Key | SQL Columns | Dataverse Columns | Status |");
        builder.AppendLine("| --- | --- | --- | --- |");

        foreach (var key in tableResult.Keys)
        {
            var statusText = key.Status switch
            {
                KeyAlignmentStatus.Matched => "Matched",
                KeyAlignmentStatus.MissingInDataverse => "Missing",
                KeyAlignmentStatus.AdditionalInDataverse => "Additional in Dataverse",
                _ => key.Status.ToString()
            };

            builder.AppendLine($"| `{key.Name}` | `{string.Join(", ", key.SqlColumns)}` | `{string.Join(", ", key.DataverseColumns)}` | {statusText} |");
        }

        builder.AppendLine();
        builder.AppendLine("### Foreign Key Alignment");
        builder.AppendLine();
        builder.AppendLine("| Foreign Key | Columns | References | Status | Matched Relationship |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (var foreignKey in tableResult.ForeignKeys)
        {
            var statusText = foreignKey.Status switch
            {
                ForeignKeyAlignmentStatus.Matched => "Matched",
                ForeignKeyAlignmentStatus.MissingInDataverse => "Missing",
                _ => foreignKey.Status.ToString()
            };

            var fk = foreignKey.ForeignKey;
            builder.AppendLine($"| `{fk.Name}` | `{string.Join(", ", fk.Columns)}` | `{fk.ReferencedTable}({string.Join(", ", fk.ReferencedColumns)})` | {statusText} | `{foreignKey.MatchedRelationshipSchemaName ?? string.Empty}` |");
        }

        if (tableResult.UnmatchedDataverseAttributes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("### Unmatched Dataverse Attributes");
            foreach (var attribute in tableResult.UnmatchedDataverseAttributes.OrderBy(attr => attr.LogicalName, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"- `{attribute.LogicalName}` ({attribute.AttributeType})");
            }
        }

        if (tableResult.UnmatchedDataverseAlternateKeys.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("### Unmatched Dataverse Alternate Keys");
            foreach (var key in tableResult.UnmatchedDataverseAlternateKeys.OrderBy(k => k.LogicalName, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"- `{key.LogicalName}` → `{string.Join(", ", key.KeyAttributes)}`");
            }
        }

        if (tableResult.UnmatchedRelationships.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("### Unmatched Dataverse Relationships");
            foreach (var relationship in tableResult.UnmatchedRelationships.OrderBy(rel => rel.SchemaName, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"- `{relationship.SchemaName}` ({relationship.ReferencingAttribute} → {relationship.ReferencedEntity})");
            }
        }
    }
}
