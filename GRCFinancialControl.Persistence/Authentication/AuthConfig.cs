using System;
using System.Collections.Generic;
using GRCFinancialControl.Core.Authentication;

namespace GRCFinancialControl.Persistence.Authentication;

/// <summary>
/// Immutable implementation of <see cref="IAuthConfig"/> used to supply MSAL configuration values.
/// </summary>
public sealed class AuthConfig : IAuthConfig
{
    public AuthConfig(string clientId, string authority, Uri organizationUrl, IReadOnlyCollection<string> scopes)
    {
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        Authority = authority ?? throw new ArgumentNullException(nameof(authority));
        OrganizationUrl = organizationUrl ?? throw new ArgumentNullException(nameof(organizationUrl));
        Scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    }

    /// <inheritdoc />
    public string ClientId { get; }

    /// <inheritdoc />
    public string Authority { get; }

    /// <inheritdoc />
    public Uri OrganizationUrl { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Scopes { get; }
}
