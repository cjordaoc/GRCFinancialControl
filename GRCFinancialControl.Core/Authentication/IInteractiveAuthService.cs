using System.Threading;
using System.Threading.Tasks;

namespace GRCFinancialControl.Core.Authentication;

/// <summary>
/// Provides interactive authentication capabilities with silent-first semantics.
/// </summary>
public interface IInteractiveAuthService
{
    /// <summary>
    /// Attempts to acquire an access token for the provided scopes, preferring a silent flow before prompting the user.
    /// </summary>
    /// <param name="scopes">The scopes to request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The authentication result.</returns>
    Task<AuthResult> AcquireTokenAsync(string[] scopes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears any cached accounts or tokens so that the next call triggers sign-in.
    /// </summary>
    Task SignOutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently signed-in user if available.
    /// </summary>
    Task<UserInfo?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
