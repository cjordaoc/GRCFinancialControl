using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Describes how a domain model maps to a Dataverse entity.
/// </summary>
public sealed class DataverseEntityMetadata
{
    private readonly Dictionary<string, string> _attributeMap;

    public DataverseEntityMetadata(
        string key,
        string logicalName,
        string primaryIdAttribute,
        IReadOnlyDictionary<string, string> attributeMap)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(logicalName))
        {
            throw new ArgumentException("Logical name cannot be null or empty.", nameof(logicalName));
        }

        if (string.IsNullOrWhiteSpace(primaryIdAttribute))
        {
            throw new ArgumentException("Primary ID attribute cannot be null or empty.", nameof(primaryIdAttribute));
        }

        Key = key;
        LogicalName = logicalName;
        PrimaryIdAttribute = primaryIdAttribute;

        _attributeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in attributeMap)
        {
            _attributeMap[pair.Key] = pair.Value;
        }
    }

    public string Key { get; }

    public string LogicalName { get; }

    public string PrimaryIdAttribute { get; }

    public IReadOnlyDictionary<string, string> AttributeMap => _attributeMap;

    public string GetAttribute(string logicalColumn)
    {
        if (!_attributeMap.TryGetValue(logicalColumn, out var attribute))
        {
            throw new KeyNotFoundException($"Attribute '{logicalColumn}' is not mapped for entity '{Key}'.");
        }

        return attribute;
    }
}
