using System;

namespace GRCFinancialControl.Core.Authentication;

/// <summary>
/// Represents the result of an authentication flow that issues an access token.
/// </summary>
public sealed record AuthResult(string AccessToken, DateTimeOffset ExpiresOn, UserInfo User)
{
    /// <summary>
    /// An empty result that can be used as a placeholder before a real token is acquired.
    /// </summary>
    public static AuthResult Empty { get; } = new(string.Empty, DateTimeOffset.MinValue, new UserInfo(null, null));
}
