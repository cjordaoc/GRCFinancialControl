using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Persistence.Services.Dataverse;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk.Query;

namespace GRCFinancialControl.Persistence.Services.People;

public sealed class DataversePersonDirectory : DataverseServiceBase, IPersonDirectory
{
    private readonly DataversePeopleOptions _options;
    private readonly DataverseEntityMetadata _systemUsersMetadata;
    private readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();

    public DataversePersonDirectory(
        IDataverseServiceClientFactory clientFactory,
        DataverseEntityMetadataRegistry metadataRegistry,
        DataversePeopleOptions options,
        ILogger<DataversePersonDirectory> logger)
        : base(clientFactory, metadataRegistry, logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _systemUsersMetadata = metadataRegistry.Get("SystemUsers");
    }

    public string? TryGetDisplayName(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        var normalized = identifier.Trim();
        if (!_options.EnablePeopleEnrichment)
        {
            return null;
        }

        lock (_syncRoot)
        {
            if (_cache.TryGetValue(normalized, out var cached))
            {
                return cached;
            }
        }

        var resolved = ResolveDisplayNameInternal(normalized);

        lock (_syncRoot)
        {
            _cache[normalized] = resolved;
        }

        return resolved;
    }

    public IReadOnlyDictionary<string, string> TryResolveDisplayNames(IEnumerable<string> identifiers)
    {
        if (!_options.EnablePeopleEnrichment)
        {
            return EmptyResults;
        }

        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var identifier in identifiers)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                continue;
            }

            var normalized = identifier.Trim();
            var displayName = TryGetDisplayName(normalized);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                results[normalized] = displayName!;
            }
        }

        return results;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyResults = new Dictionary<string, string>();

    private string? ResolveDisplayNameInternal(string identifier)
    {
        try
        {
            return FetchDisplayNameAsync(identifier).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to resolve person for identifier {Identifier}", identifier);
            return null;
        }
    }

    private async System.Threading.Tasks.Task<string?> FetchDisplayNameAsync(string identifier)
    {
        return await ExecuteAsync(async client =>
        {
            var query = new QueryExpression(_systemUsersMetadata.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    _systemUsersMetadata.GetAttribute("FullName"),
                    _systemUsersMetadata.GetAttribute("InternalEmailAddress"),
                    _systemUsersMetadata.GetAttribute("DomainName")),
                TopCount = 1,
            };

            var filter = new FilterExpression(LogicalOperator.Or);
            filter.AddCondition(_systemUsersMetadata.GetAttribute("InternalEmailAddress"), ConditionOperator.Equal, identifier);
            filter.AddCondition(_systemUsersMetadata.GetAttribute("DomainName"), ConditionOperator.Equal, identifier);
            query.Criteria.AddFilter(filter);

            var result = await client.RetrieveMultipleAsync(query).ConfigureAwait(false);
            if (result.Entities.Count == 0)
            {
                return null;
            }

            var entity = result.Entities[0];
            var fullName = entity.GetString(_systemUsersMetadata.GetAttribute("FullName"));
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            var email = entity.GetString(_systemUsersMetadata.GetAttribute("InternalEmailAddress"));
            if (!string.IsNullOrWhiteSpace(email))
            {
                return email;
            }

            var domainName = entity.GetString(_systemUsersMetadata.GetAttribute("DomainName"));
            return string.IsNullOrWhiteSpace(domainName) ? null : domainName;
        }).ConfigureAwait(false);
    }
}
