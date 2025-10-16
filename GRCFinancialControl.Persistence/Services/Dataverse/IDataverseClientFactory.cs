using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Creates Dataverse <see cref="ServiceClient" /> instances that rely on delegated authentication.
/// </summary>
public interface IDataverseClientFactory
{
    /// <summary>
    /// Creates a configured <see cref="ServiceClient" /> instance.
    /// </summary>
    Task<ServiceClient> CreateAsync(CancellationToken cancellationToken = default);
}
