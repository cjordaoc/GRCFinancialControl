using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Persistence.Services.Dataverse.Provisioning;

public sealed class DataverseProvisioningResult
{
    public DataverseProvisioningResult(bool succeeded, IReadOnlyList<string> actions)
    {
        Succeeded = succeeded;
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public bool Succeeded { get; }

    public IReadOnlyList<string> Actions { get; }
}
