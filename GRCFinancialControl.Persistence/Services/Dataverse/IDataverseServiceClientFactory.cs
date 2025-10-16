using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

public interface IDataverseServiceClientFactory
{
    Task<ServiceClient> CreateClientAsync(CancellationToken cancellationToken = default);
}
