using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Creates Dataverse <see cref="ServiceClient"/> instances using MSAL-based delegated authentication.
/// </summary>
public sealed class DataverseClientFactory : IDataverseClientFactory
{
    private readonly IInteractiveAuthService _authService;
    private readonly IAuthConfig _authConfig;
    private readonly ILogger<DataverseClientFactory> _logger;

    public DataverseClientFactory(IInteractiveAuthService authService, IAuthConfig authConfig, ILogger<DataverseClientFactory> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _authConfig = authConfig ?? throw new ArgumentNullException(nameof(authConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ServiceClient> CreateAsync(CancellationToken cancellationToken = default)
    {
        var scopes = (_authConfig.Scopes ?? Array.Empty<string>()).ToArray();
        if (scopes.Length == 0)
        {
            scopes = new[] { $"{_authConfig.OrganizationUrl.AbsoluteUri.TrimEnd('/')}/user_impersonation" };
        }

        var tokenProvider = new DataverseAccessTokenProvider(_authService, scopes, _logger);
        await tokenProvider.PrimeAsync(cancellationToken).ConfigureAwait(false);

        var client = CreateClient(tokenProvider);
        var validation = await TryValidateAsync(client).ConfigureAwait(false);

        if (!validation.Success && validation.Unauthorized)
        {
            _logger.LogWarning(validation.Error, "Dataverse client returned unauthorized; refreshing token and retrying once.");
            _logger.LogInformation("Discarding cached Dataverse token and requesting a new one.");
            tokenProvider.Invalidate();
            client.Dispose();

            await tokenProvider.PrimeAsync(cancellationToken).ConfigureAwait(false);
            client = CreateClient(tokenProvider);
            validation = await TryValidateAsync(client).ConfigureAwait(false);
        }

        if (!validation.Success)
        {
            var friendlyMessage = BuildFriendlyValidationMessage(validation.Unauthorized, validation.Error, validation.LastError);
            client.Dispose();
            _logger.LogError(validation.Error, "Unable to initialize Dataverse ServiceClient: {Message}", friendlyMessage);
            throw new InvalidOperationException(friendlyMessage, validation.Error);
        }

        _logger.LogInformation("Dataverse ServiceClient initialized successfully for {Organization}.", _authConfig.OrganizationUrl);
        return client;
    }

    private ServiceClient CreateClient(DataverseAccessTokenProvider tokenProvider)
    {
        return new ServiceClient(
            _authConfig.OrganizationUrl,
            tokenProvider.GetTokenAsync,
            useUniqueInstance: true,
            _logger);
    }

    private Task<ValidationResult> TryValidateAsync(ServiceClient client)
    {
        if (client.IsReady)
        {
            return Task.FromResult(new ValidationResult(true, false, null, null));
        }

        var unauthorized = IsUnauthorized(client.LastException, client.LastError);
        Exception? error = client.LastException;
        if (error is null && !string.IsNullOrWhiteSpace(client.LastError))
        {
            error = new InvalidOperationException(client.LastError);
        }

        return Task.FromResult(new ValidationResult(false, unauthorized, error, client.LastError));
    }

    private static bool IsUnauthorized(Exception? exception, string? lastError)
    {
        if (!string.IsNullOrWhiteSpace(lastError))
        {
            if (lastError.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0
                || lastError.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        if (exception is null)
        {
            return false;
        }

        if (exception is HttpRequestException httpRequest && httpRequest.StatusCode == HttpStatusCode.Unauthorized)
        {
            return true;
        }

        if (exception.Message.Contains("401", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsUnauthorized(exception.InnerException, lastError);
    }

    private string BuildFriendlyValidationMessage(bool unauthorized, Exception? error, string? lastError)
    {
        if (unauthorized)
        {
            if (!string.IsNullOrWhiteSpace(lastError))
            {
                if (lastError.Contains("license", StringComparison.OrdinalIgnoreCase))
                {
                    return "Access to Dataverse was denied. Ensure the signed-in user has an active Dynamics 365 license.";
                }

                if (lastError.Contains("privilege", StringComparison.OrdinalIgnoreCase)
                    || lastError.Contains("security role", StringComparison.OrdinalIgnoreCase)
                    || lastError.Contains("permission", StringComparison.OrdinalIgnoreCase))
                {
                    return "Access to Dataverse was denied. Verify the user has the correct Dataverse security role assignments.";
                }
            }

            return "Access to Dataverse was denied. Ensure the signed-in user has a valid Dynamics 365 license and the necessary security role.";
        }

        if (!string.IsNullOrWhiteSpace(lastError))
        {
            return lastError;
        }

        return error?.Message ?? "An unexpected Dataverse error occurred.";
    }

    private readonly record struct ValidationResult(bool Success, bool Unauthorized, Exception? Error, string? LastError);

    private sealed class DataverseAccessTokenProvider
    {
        private readonly IInteractiveAuthService _authService;
        private readonly string[] _scopes;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private AuthResult? _currentToken;

        public DataverseAccessTokenProvider(IInteractiveAuthService authService, string[] scopes, ILogger logger)
        {
            _authService = authService;
            _scopes = scopes;
            _logger = logger;
        }

        public async Task PrimeAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _currentToken = await _authService.AcquireTokenAsync(_scopes, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<string> GetTokenAsync(string _)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_currentToken is null || TokenExpiring(_currentToken))
                {
                    _logger.LogInformation("Refreshing Dataverse access token.");
                    _currentToken = await _authService.AcquireTokenAsync(_scopes, CancellationToken.None).ConfigureAwait(false);
                }

                return _currentToken.AccessToken;
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Invalidate()
        {
            _currentToken = null;
        }

        private static bool TokenExpiring(AuthResult token)
        {
            return token.ExpiresOn <= DateTimeOffset.UtcNow.AddMinutes(5);
        }
    }
}
