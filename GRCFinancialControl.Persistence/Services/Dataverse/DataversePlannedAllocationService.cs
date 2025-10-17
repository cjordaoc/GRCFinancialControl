using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Placeholder Dataverse implementation for <see cref="IPlannedAllocationService"/>.
/// </summary>
public sealed class DataversePlannedAllocationService : DataverseServiceBase, IPlannedAllocationService
{
    public DataversePlannedAllocationService(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataversePlannedAllocationService> logger)
        : base(repository, metadataRegistry, logger)
    {
    }

    public Task<List<PlannedAllocation>> GetAllocationsForEngagementAsync(int engagementId)
    {
        Logger.LogWarning("Planned allocations are not yet available through the Dataverse backend (EngagementId={EngagementId}).", engagementId);
        return Task.FromResult(new List<PlannedAllocation>());
    }

    public Task SaveAllocationsForEngagementAsync(int engagementId, List<PlannedAllocation> allocations)
    {
        return Task.FromException(new InvalidOperationException("Planned allocations cannot be modified from the Dataverse backend."));
    }
}
