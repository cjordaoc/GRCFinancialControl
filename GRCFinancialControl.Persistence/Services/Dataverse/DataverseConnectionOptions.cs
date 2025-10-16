using System;
using System.Collections.Generic;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Provides the Dataverse connection settings sourced from environment variables.
/// </summary>
public sealed class DataverseConnectionOptions
{
    public DataverseConnectionOptions(string orgUrl, string clientId, string clientSecret, string tenantId, DataverseAuthMode authMode)
    {
        OrgUrl = orgUrl ?? throw new ArgumentNullException(nameof(orgUrl));
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        ClientSecret = clientSecret ?? string.Empty;
        TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        AuthMode = authMode;
    }

    public string OrgUrl { get; }

    public string ClientId { get; }

    public string ClientSecret { get; }

    public string TenantId { get; }

    public DataverseAuthMode AuthMode { get; }

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
        var authModeValue = Environment.GetEnvironmentVariable("DV_AUTH_MODE");

        if (string.IsNullOrWhiteSpace(orgUrl) || string.IsNullOrWhiteSpace(clientId))
        {
            options = null;
            return false;
        }

        var authMode = Enum.TryParse(authModeValue, ignoreCase: true, out DataverseAuthMode parsedMode)
            ? parsedMode
            : (!string.IsNullOrWhiteSpace(clientSecret) ? DataverseAuthMode.ClientSecret : DataverseAuthMode.Interactive);

        if (authMode == DataverseAuthMode.ClientSecret && string.IsNullOrWhiteSpace(clientSecret))
        {
            options = null;
            return false;
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = "common";
        }

        options = new DataverseConnectionOptions(orgUrl, clientId, clientSecret ?? string.Empty, tenantId, authMode);
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

        if (settings.AuthMode == DataverseAuthMode.ClientSecret)
        {
            if (string.IsNullOrWhiteSpace(settings.ClientSecret))
            {
                missing.Add(nameof(settings.ClientSecret));
            }

            if (string.IsNullOrWhiteSpace(settings.TenantId))
            {
                missing.Add(nameof(settings.TenantId));
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Stored Dataverse settings are incomplete: {string.Join(", ", missing)}.");
        }

        var tenant = string.IsNullOrWhiteSpace(settings.TenantId) ? "common" : settings.TenantId;
        var clientSecret = settings.AuthMode == DataverseAuthMode.ClientSecret ? settings.ClientSecret : string.Empty;

        return new DataverseConnectionOptions(settings.OrgUrl, settings.ClientId, clientSecret ?? string.Empty, tenant, settings.AuthMode);
    }

    public string BuildConnectionString()
    {
        if (AuthMode != DataverseAuthMode.ClientSecret)
        {
            throw new InvalidOperationException("Connection strings are only available for client secret authentication.");
        }

        return $"AuthType=ClientSecret;Url={OrgUrl};ClientId={ClientId};ClientSecret={ClientSecret};TenantId={TenantId};";
    }
}
