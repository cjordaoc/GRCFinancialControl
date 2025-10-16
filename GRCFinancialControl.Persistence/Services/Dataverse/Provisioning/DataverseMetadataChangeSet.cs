using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;

public sealed class DataverseMetadataChangeSet
{
    [JsonPropertyName("solution")]
    public required DataverseSolutionDefinition Solution { get; init; }

    [JsonPropertyName("optionSets")]
    public IReadOnlyList<DataverseOptionSetDefinition> OptionSets { get; init; } = Array.Empty<DataverseOptionSetDefinition>();

    [JsonPropertyName("tables")]
    public IReadOnlyList<DataverseTableDefinition> Tables { get; init; } = Array.Empty<DataverseTableDefinition>();
}

public sealed class DataverseSolutionDefinition
{
    [JsonPropertyName("uniqueName")]
    public required string UniqueName { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("publisher")]
    public required DataversePublisherDefinition Publisher { get; init; }
}

public sealed class DataversePublisherDefinition
{
    [JsonPropertyName("uniqueName")]
    public required string UniqueName { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("prefix")]
    public required string Prefix { get; init; }
}

public sealed class DataverseOptionSetDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("options")]
    public IReadOnlyList<DataverseOptionSetValueDefinition> Options { get; init; } = Array.Empty<DataverseOptionSetValueDefinition>();
}

public sealed class DataverseOptionSetValueDefinition
{
    [JsonPropertyName("value")]
    public required int Value { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }
}

public sealed class DataverseTableDefinition
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("logicalName")]
    public required string LogicalName { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("primaryIdAttribute")]
    public required string PrimaryIdAttribute { get; init; }

    [JsonPropertyName("primaryNameAttribute")]
    public required string PrimaryNameAttribute { get; init; }

    [JsonPropertyName("ownershipType")]
    public required string OwnershipType { get; init; }

    [JsonPropertyName("changeTrackingEnabled")]
    public bool ChangeTrackingEnabled { get; init; }

    [JsonPropertyName("auditingEnabled")]
    public bool AuditingEnabled { get; init; }

    [JsonPropertyName("columns")]
    public IReadOnlyList<DataverseColumnDefinition> Columns { get; init; } = Array.Empty<DataverseColumnDefinition>();

    [JsonPropertyName("alternateKeys")]
    public IReadOnlyList<DataverseAlternateKeyDefinition> AlternateKeys { get; init; } = Array.Empty<DataverseAlternateKeyDefinition>();
}

public sealed class DataverseColumnDefinition
{
    [JsonPropertyName("logicalName")]
    public required string LogicalName { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("requiredLevel")]
    public string? RequiredLevel { get; init; }

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; init; }

    [JsonPropertyName("precision")]
    public int? Precision { get; init; }

    [JsonPropertyName("scale")]
    public int? Scale { get; init; }

    [JsonPropertyName("minValue")]
    public decimal? MinValue { get; init; }

    [JsonPropertyName("maxValue")]
    public decimal? MaxValue { get; init; }

    [JsonPropertyName("isAlternateKey")]
    public bool IsAlternateKey { get; init; }

    [JsonPropertyName("alternateKeyName")]
    public string? AlternateKeyName { get; init; }

    [JsonPropertyName("optionSetName")]
    public string? OptionSetName { get; init; }

    [JsonPropertyName("targetTable")]
    public string? TargetTable { get; init; }

    [JsonPropertyName("deleteBehavior")]
    public string? DeleteBehavior { get; init; }

    [JsonPropertyName("isPrimaryName")]
    public bool IsPrimaryName { get; init; }

    [JsonPropertyName("behavior")]
    public string? DateTimeBehavior { get; init; }

    [JsonPropertyName("sqlColumnName")]
    public string? SqlColumnName { get; init; }
}

public sealed class DataverseAlternateKeyDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyList<string> Fields { get; init; } = Array.Empty<string>();
}
