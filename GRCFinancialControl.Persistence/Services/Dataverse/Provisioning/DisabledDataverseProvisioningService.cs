using System.Threading;
using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;

public sealed class DisabledDataverseProvisioningService : IDataverseProvisioningService
{
    public Task<DataverseProvisioningResult> ProvisionAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new DataverseProvisioningResult(false, new[]
        {
            "Dataverse provisioning is unavailable because the Dataverse backend is not configured for this environment.",
        }));
    }
}
