using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Describes shared operations exposed by Dataverse-backed services.
/// </summary>
public interface IDataverseService
{
    Task<TResult> ExecuteAsync<TResult>(Func<ServiceClient, Task<TResult>> operation, CancellationToken cancellationToken = default);

    Task ExecuteAsync(Func<ServiceClient, Task> operation, CancellationToken cancellationToken = default);

    DataverseEntityMetadata GetEntityMetadata(string logicalName);
}
