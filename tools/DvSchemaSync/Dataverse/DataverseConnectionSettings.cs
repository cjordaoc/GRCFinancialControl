using System;

namespace DvSchemaSync.Dataverse;

internal sealed record DataverseConnectionSettings(
    string? OrgUrl,
    string? ClientId,
    string? ClientSecret,
    string? TenantId)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(OrgUrl) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(TenantId);

    public string BuildConnectionString()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Dataverse connection details are incomplete. Ensure DV_ORG_URL, DV_CLIENT_ID, DV_CLIENT_SECRET, and DV_TENANT_ID are set.");
        }

        return $"AuthType=ClientSecret;Url={OrgUrl};ClientId={ClientId};ClientSecret={ClientSecret};TenantId={TenantId};";
    }
}
