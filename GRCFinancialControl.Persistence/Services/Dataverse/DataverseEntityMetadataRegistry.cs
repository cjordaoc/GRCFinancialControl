using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Stores the Dataverse entity metadata used by Dataverse-backed services.
/// </summary>
public sealed class DataverseEntityMetadataRegistry
{
    private readonly Dictionary<string, DataverseEntityMetadata> _entities;

    public DataverseEntityMetadataRegistry(IEnumerable<DataverseEntityMetadata> entities)
    {
        _entities = new Dictionary<string, DataverseEntityMetadata>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            _entities[entity.Key] = entity;
        }
    }

    public DataverseEntityMetadata Get(string key)
    {
        if (!_entities.TryGetValue(key, out var metadata))
        {
            throw new KeyNotFoundException($"Entity metadata '{key}' has not been registered for the Dataverse backend.");
        }

        return metadata;
    }

    public static DataverseEntityMetadataRegistry CreateDefault(string customizationPrefix = "grc")
    {
        if (string.IsNullOrWhiteSpace(customizationPrefix))
        {
            throw new ArgumentException("Customization prefix cannot be empty.", nameof(customizationPrefix));
        }

        static string Attribute(string prefix, string entity, string attribute)
        {
            return $"{prefix}_{entity}{attribute}".ToLowerInvariant();
        }

        var customers = new DataverseEntityMetadata(
            key: "Customers",
            logicalName: $"{customizationPrefix}_customers".ToLowerInvariant(),
            primaryIdAttribute: $"{customizationPrefix}_customersid".ToLowerInvariant(),
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = Attribute(customizationPrefix, "customer", "sqlid"),
                ["Name"] = Attribute(customizationPrefix, "customer", "name"),
                ["CustomerCode"] = Attribute(customizationPrefix, "customer", "code"),
            });

        var managers = new DataverseEntityMetadata(
            key: "Managers",
            logicalName: $"{customizationPrefix}_managers".ToLowerInvariant(),
            primaryIdAttribute: $"{customizationPrefix}_managersid".ToLowerInvariant(),
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = Attribute(customizationPrefix, "manager", "sqlid"),
                ["Name"] = Attribute(customizationPrefix, "manager", "name"),
                ["Email"] = Attribute(customizationPrefix, "manager", "email"),
                ["Position"] = Attribute(customizationPrefix, "manager", "position"),
            });

        var engagements = new DataverseEntityMetadata(
            key: "Engagements",
            logicalName: $"{customizationPrefix}_engagements".ToLowerInvariant(),
            primaryIdAttribute: $"{customizationPrefix}_engagementsid".ToLowerInvariant(),
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = Attribute(customizationPrefix, "engagement", "sqlid"),
                ["CustomerId"] = Attribute(customizationPrefix, "engagement", "customer_sqlid"),
            });

        var actualsEntries = new DataverseEntityMetadata(
            key: "ActualsEntries",
            logicalName: $"{customizationPrefix}_actualsentries".ToLowerInvariant(),
            primaryIdAttribute: $"{customizationPrefix}_actualsentriesid".ToLowerInvariant(),
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EngagementId"] = Attribute(customizationPrefix, "actualsentry", "engagement_sqlid"),
            });

        var plannedAllocations = new DataverseEntityMetadata(
            key: "PlannedAllocations",
            logicalName: $"{customizationPrefix}_plannedallocations".ToLowerInvariant(),
            primaryIdAttribute: $"{customizationPrefix}_plannedallocationsid".ToLowerInvariant(),
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EngagementId"] = Attribute(customizationPrefix, "plannedallocation", "engagement_sqlid"),
            });

        var engagementPapds = new DataverseEntityMetadata(
            key: "EngagementPapds",
            logicalName: $"{customizationPrefix}_engagementpapds".ToLowerInvariant(),
            primaryIdAttribute: $"{customizationPrefix}_engagementpapdsid".ToLowerInvariant(),
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EngagementId"] = Attribute(customizationPrefix, "engagementpapd", "engagement_sqlid"),
            });

        var engagementRankBudgets = new DataverseEntityMetadata(
            key: "EngagementRankBudgets",
            logicalName: $"{customizationPrefix}_engagementrankbudgets".ToLowerInvariant(),
            primaryIdAttribute: $"{customizationPrefix}_engagementrankbudgetsid".ToLowerInvariant(),
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EngagementId"] = Attribute(customizationPrefix, "rankbudget", "engagement_sqlid"),
            });

        var financialEvolutions = new DataverseEntityMetadata(
            key: "FinancialEvolutions",
            logicalName: $"{customizationPrefix}_financialevolutions".ToLowerInvariant(),
            primaryIdAttribute: $"{customizationPrefix}_financialevolutionsid".ToLowerInvariant(),
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EngagementId"] = Attribute(customizationPrefix, "financialevolution", "engagement_sqlid"),
            });

        var engagementFiscalYearAllocations = new DataverseEntityMetadata(
            key: "EngagementFiscalYearAllocations",
            logicalName: $"{customizationPrefix}_engagementfiscalyearallocations".ToLowerInvariant(),
            primaryIdAttribute: $"{customizationPrefix}_engagementfiscalyearallocationsid".ToLowerInvariant(),
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EngagementId"] = Attribute(customizationPrefix, "fiscalyearallocation", "engagement_sqlid"),
            });

        var engagementFiscalYearRevenueAllocations = new DataverseEntityMetadata(
            key: "EngagementFiscalYearRevenueAllocations",
            logicalName: $"{customizationPrefix}_engagementfiscalyearrevenueallocations".ToLowerInvariant(),
            primaryIdAttribute: $"{customizationPrefix}_engagementfiscalyearrevenueallocationsid".ToLowerInvariant(),
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EngagementId"] = Attribute(customizationPrefix, "fiscalyearrevenue", "engagement_sqlid"),
            });

        var systemUsers = new DataverseEntityMetadata(
            key: "SystemUsers",
            logicalName: "systemuser",
            primaryIdAttribute: "systemuserid",
            attributeMap: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FullName"] = "fullname",
                ["InternalEmailAddress"] = "internalemailaddress",
                ["DomainName"] = "domainname",
            });

        return new DataverseEntityMetadataRegistry(
            new[]
            {
                customers,
                managers,
                engagements,
                actualsEntries,
                plannedAllocations,
                engagementPapds,
                engagementRankBudgets,
                financialEvolutions,
                engagementFiscalYearAllocations,
                engagementFiscalYearRevenueAllocations,
                systemUsers,
            });
    }
}
