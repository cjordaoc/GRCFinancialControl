using System;
using System.Collections.Generic;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Provides the Dataverse connection settings sourced from environment variables.
/// </summary>
public sealed class DataverseConnectionOptions
{
    public DataverseConnectionOptions(string orgUrl, string clientId, string clientSecret, string tenantId)
    {
        OrgUrl = orgUrl ?? throw new ArgumentNullException(nameof(orgUrl));
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        ClientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
    }

    public string OrgUrl { get; }

    public string ClientId { get; }

    public string ClientSecret { get; }

    public string TenantId { get; }

    public static DataverseConnectionOptions FromEnvironment()
    {
        if (TryFromEnvironment(out var options))
        {
            return options!;
        }

        throw new InvalidOperationException("Dataverse environment variables must be provided to use the Dataverse backend.");
    }

    public static bool TryFromEnvironment(out DataverseConnectionOptions? options)
    {
        var orgUrl = Environment.GetEnvironmentVariable("DV_ORG_URL");
        var clientId = Environment.GetEnvironmentVariable("DV_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("DV_CLIENT_SECRET");
        var tenantId = Environment.GetEnvironmentVariable("DV_TENANT_ID");

        if (string.IsNullOrWhiteSpace(orgUrl)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            || string.IsNullOrWhiteSpace(tenantId))
        {
            options = null;
            return false;
        }

        options = new DataverseConnectionOptions(orgUrl, clientId, clientSecret, tenantId);
        return true;
    }

    public static DataverseConnectionOptions FromSettings(DataverseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.OrgUrl))
        {
            missing.Add(nameof(settings.OrgUrl));
        }

        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            missing.Add(nameof(settings.ClientId));
        }

        if (string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            missing.Add(nameof(settings.ClientSecret));
        }

        if (string.IsNullOrWhiteSpace(settings.TenantId))
        {
            missing.Add(nameof(settings.TenantId));
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Stored Dataverse settings are incomplete: {string.Join(", ", missing)}.");
        }

        return new DataverseConnectionOptions(settings.OrgUrl, settings.ClientId, settings.ClientSecret, settings.TenantId);
    }

    public string BuildConnectionString()
    {
        return $"AuthType=ClientSecret;Url={OrgUrl};ClientId={ClientId};ClientSecret={ClientSecret};TenantId={TenantId};";
    }
}
