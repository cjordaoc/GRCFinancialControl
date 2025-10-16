using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Creates <see cref="ServiceClient"/> instances using the configured Dataverse credentials.
/// </summary>
public sealed class DataverseServiceClientFactory : IDataverseServiceClientFactory
{
    private const int DefaultRetryCount = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly DataverseConnectionOptions _options;
    private readonly ILogger<DataverseServiceClientFactory> _logger;

    public DataverseServiceClientFactory(DataverseConnectionOptions options, ILogger<DataverseServiceClientFactory> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ServiceClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = _options.BuildConnectionString();

        for (var attempt = 1; attempt <= DefaultRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var client = new ServiceClient(connectionString);
                if (!client.IsReady)
                {
                    client.Dispose();
                    throw new InvalidOperationException("Dataverse ServiceClient failed to initialize.");
                }

                return client;
            }
            catch (Exception ex) when (attempt < DefaultRetryCount)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} to create Dataverse ServiceClient failed. Retrying in {Delay}.", attempt, RetryDelay);
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Unable to initialize Dataverse ServiceClient after multiple attempts.");
    }
}
