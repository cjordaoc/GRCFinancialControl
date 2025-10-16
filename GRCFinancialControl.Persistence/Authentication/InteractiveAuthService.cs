using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;

namespace GRCFinancialControl.Persistence.Authentication;

/// <summary>
/// Provides MSAL-based interactive authentication with silent-first semantics and shared token caching.
/// </summary>
public sealed class InteractiveAuthService : IInteractiveAuthService, IDisposable
{
    private const string CacheFileName = "msal_cache.dat";
    private const string CacheDirectoryName = "GRCFinancialControl";
    private const string CacheSubDirectoryName = "AuthCache";
    private const string KeyChainService = "com.grcfinancialcontrol.auth";
    private const string KeyChainAccount = "MSALCache";
    private const string LinuxSchema = "com.grcfinancialcontrol.auth";
    private const string LinuxCollection = "default";
    private const string LinuxLabel = "GRC Financial Control Token Cache";

    private readonly IAuthConfig _config;
    private readonly ILogger<InteractiveAuthService> _logger;
    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private readonly string[] _defaultScopes;
    private readonly string[] _fallbackScopes;

    private IPublicClientApplication? _publicClient;
    private MsalCacheHelper? _cacheHelper;
    private UserInfo? _currentUser;

    public InteractiveAuthService(IAuthConfig config, ILogger<InteractiveAuthService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var configuredScopes = (config.Scopes ?? Array.Empty<string>())
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (configuredScopes.Length == 0)
        {
            var impersonationScope = $"{config.OrganizationUrl.AbsoluteUri.TrimEnd('/')}/user_impersonation";
            configuredScopes = new[] { impersonationScope };
        }

        _defaultScopes = configuredScopes;
        _fallbackScopes = new[] { $"{config.OrganizationUrl.AbsoluteUri.TrimEnd('/')}/.default" };
    }

    /// <inheritdoc />
    public async Task<AuthResult> AcquireTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
    {
        var normalizedScopes = NormalizeScopes(scopes);
        var scopeCandidates = BuildScopeCandidates(normalizedScopes);
        var client = await GetOrCreateClientAsync(cancellationToken).ConfigureAwait(false);

        Exception? lastError = null;

        foreach (var (candidateScopes, isFallback) in scopeCandidates)
        {
            try
            {
                var authenticationResult = await AcquireTokenInternalAsync(client, candidateScopes, cancellationToken).ConfigureAwait(false);
                var userInfo = ExtractUserInfo(authenticationResult);
                _currentUser = userInfo;
                _logger.LogInformation("Acquired Dataverse access token for {User}.", userInfo.UserPrincipalName ?? "unknown user");
                return new AuthResult(authenticationResult.AccessToken, authenticationResult.ExpiresOn, userInfo);
            }
            catch (MsalServiceException ex) when (!isFallback && ShouldRetryWithFallback(ex))
            {
                _logger.LogWarning(ex, "Token acquisition for scopes {Scopes} failed; attempting fallback scopes.", candidateScopes);
                lastError = ex;
            }
            catch (MsalException ex)
            {
                _logger.LogError(ex, "Token acquisition failed for scopes {Scopes}.", candidateScopes);
                throw;
            }
        }

        _logger.LogError(lastError, "Failed to acquire a Dataverse access token after trying all scope variants.");
        throw lastError ?? new InvalidOperationException("Unable to acquire a Dataverse access token.");
    }

    /// <inheritdoc />
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var client = await GetOrCreateClientAsync(cancellationToken).ConfigureAwait(false);
        var accounts = await client.GetAccountsAsync().ConfigureAwait(false);

        foreach (var account in accounts)
        {
            _logger.LogInformation("Removing cached account {Account}.", account.Username);
            await client.RemoveAsync(account).ConfigureAwait(false);
        }

        _logger.LogInformation("Dataverse sign-out completed.");
        _currentUser = null;
    }

    /// <inheritdoc />
    public async Task<UserInfo?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser is not null)
        {
            return _currentUser;
        }

        var client = await GetOrCreateClientAsync(cancellationToken).ConfigureAwait(false);
        var account = (await client.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault();
        if (account is null)
        {
            return null;
        }

        var user = new UserInfo(account.Username, account.Username);
        _currentUser = user;
        return user;
    }

    private string[] NormalizeScopes(string[] scopes)
    {
        if (scopes is { Length: > 0 })
        {
            return scopes;
        }

        return _defaultScopes;
    }

    private IEnumerable<(string[] Scopes, bool IsFallback)> BuildScopeCandidates(string[] preferredScopes)
    {
        yield return (preferredScopes, false);

        if (!_fallbackScopes.SequenceEqual(preferredScopes, StringComparer.OrdinalIgnoreCase))
        {
            yield return (_fallbackScopes, true);
        }
    }

    private static bool ShouldRetryWithFallback(MsalServiceException exception)
    {
        return string.Equals(exception.ErrorCode, "invalid_scope", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("AADSTS65001", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<AuthenticationResult> AcquireTokenInternalAsync(IPublicClientApplication client, string[] scopes, CancellationToken cancellationToken)
    {
        var accounts = await client.GetAccountsAsync().ConfigureAwait(false);

        foreach (var account in accounts)
        {
            try
            {
                _logger.LogDebug("Attempting silent token acquisition for account {Account}.", account.Username);
                return await client.AcquireTokenSilent(scopes, account)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (MsalUiRequiredException ex)
            {
                _logger.LogInformation("Silent acquisition requires interaction for account {Account}: {Reason}.", account.Username, ex.ErrorCode);
            }
        }

        _logger.LogInformation("Falling back to interactive sign-in for scopes {Scopes}.", scopes);
        var interactiveBuilder = client.AcquireTokenInteractive(scopes)
            .WithPrompt(Prompt.SelectAccount);

        if (OperatingSystem.IsWindows())
        {
            interactiveBuilder = interactiveBuilder.WithUseEmbeddedWebView(false);
        }

        return await interactiveBuilder.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static UserInfo ExtractUserInfo(AuthenticationResult result)
    {
        var principal = result?.ClaimsPrincipal;
        var displayName = principal?.FindFirst(ClaimTypes.Name)?.Value
            ?? principal?.FindFirst("name")?.Value
            ?? result?.Account?.Username;
        var upn = principal?.FindFirst("preferred_username")?.Value
            ?? principal?.FindFirst(ClaimTypes.Upn)?.Value
            ?? result?.Account?.Username;

        return new UserInfo(displayName, upn);
    }

    private async Task<IPublicClientApplication> GetOrCreateClientAsync(CancellationToken cancellationToken)
    {
        if (_publicClient is not null)
        {
            return _publicClient;
        }

        await _clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_publicClient is not null)
            {
                return _publicClient;
            }

            var builder = PublicClientApplicationBuilder.Create(_config.ClientId)
                .WithAuthority(_config.Authority)
                .WithDefaultRedirectUri();

            if (OperatingSystem.IsWindows())
            {
                var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);
                builder = builder.WithBroker(brokerOptions);
            }

            var client = builder.Build();
            var cacheHelper = await CreateCacheHelperAsync().ConfigureAwait(false);
            cacheHelper.RegisterCache(client.UserTokenCache);

            _cacheHelper = cacheHelper;
            _publicClient = client;

            return client;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private async Task<MsalCacheHelper> CreateCacheHelperAsync()
    {
        var cacheDirectory = EnsureCacheDirectory();
        var storageBuilder = new StorageCreationPropertiesBuilder(CacheFileName, cacheDirectory);

        if (OperatingSystem.IsMacOS())
        {
            storageBuilder = storageBuilder.WithMacKeyChain(KeyChainService, KeyChainAccount);
        }
        else if (OperatingSystem.IsLinux())
        {
            storageBuilder = storageBuilder.WithLinuxKeyring(
                schemaName: LinuxSchema,
                collection: LinuxCollection,
                secretLabel: LinuxLabel,
                attribute1: new KeyValuePair<string, string>("Version", "1"),
                attribute2: new KeyValuePair<string, string>("Product", "GRCFinancialControl"));
        }

        var storageProperties = storageBuilder.Build();
        return await MsalCacheHelper.CreateAsync(storageProperties).ConfigureAwait(false);
    }

    private static string EnsureCacheDirectory()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = AppContext.BaseDirectory;
        }

        var cacheDirectory = Path.Combine(basePath, CacheDirectoryName, CacheSubDirectoryName);
        Directory.CreateDirectory(cacheDirectory);
        return cacheDirectory;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _clientLock.Dispose();
    }
}
