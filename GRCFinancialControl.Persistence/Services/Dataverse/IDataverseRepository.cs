using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Provides an execution boundary for Dataverse operations that require a <see cref="ServiceClient"/> instance.
/// </summary>
public interface IDataverseRepository
{
    /// <summary>
    /// Executes a Dataverse operation that returns a result.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="operation">The Dataverse operation to execute.</param>
    /// <param name="cancellationToken">The optional cancellation token.</param>
    /// <returns>The result produced by the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is <c>null</c>.</exception>
    Task<TResult> ExecuteAsync<TResult>(Func<ServiceClient, Task<TResult>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a Dataverse operation that does not return a result.
    /// </summary>
    /// <param name="operation">The Dataverse operation to execute.</param>
    /// <param name="cancellationToken">The optional cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is <c>null</c>.</exception>
    Task ExecuteAsync(Func<ServiceClient, Task> operation, CancellationToken cancellationToken = default);
}
