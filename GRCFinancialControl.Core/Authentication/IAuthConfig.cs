using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Core.Authentication;

/// <summary>
/// Provides the configuration values required to acquire delegated Dataverse tokens.
/// </summary>
public interface IAuthConfig
{
    /// <summary>
    /// Gets the application (client) identifier registered for delegated Dataverse access.
    /// </summary>
    string ClientId { get; }

    /// <summary>
    /// Gets the authority to use for sign-in. This can target a specific tenant or <c>common</c>.
    /// </summary>
    string Authority { get; }

    /// <summary>
    /// Gets the Dataverse organization URL that issued resources should target.
    /// </summary>
    Uri OrganizationUrl { get; }

    /// <summary>
    /// Gets the scopes to request when acquiring tokens.
    /// </summary>
    IReadOnlyCollection<string> Scopes { get; }
}
