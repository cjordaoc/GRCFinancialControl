using System.Threading;
using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;

public interface IDataverseProvisioningService
{
    Task<DataverseProvisioningResult> ProvisionAsync(CancellationToken cancellationToken);
}
