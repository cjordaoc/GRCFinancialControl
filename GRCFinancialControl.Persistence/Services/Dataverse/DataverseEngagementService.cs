using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Placeholder Dataverse implementation for <see cref="IEngagementService"/>.
/// </summary>
public sealed class DataverseEngagementService : DataverseServiceBase, IEngagementService
{
    public DataverseEngagementService(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataverseEngagementService> logger)
        : base(repository, metadataRegistry, logger)
    {
    }

    public Task<List<Engagement>> GetAllAsync()
    {
        Logger.LogWarning("Engagement data is not yet available through the Dataverse backend.");
        return Task.FromResult(new List<Engagement>());
    }

    public Task<Engagement?> GetByIdAsync(int id)
    {
        Logger.LogWarning("Engagement details are not yet available through the Dataverse backend (Id={EngagementId}).", id);
        return Task.FromResult<Engagement?>(null);
    }

    public Task<Papd?> GetPapdForDateAsync(int engagementId, DateTime date)
    {
        Logger.LogWarning("PAPD assignments are not yet available through the Dataverse backend (EngagementId={EngagementId}).", engagementId);
        return Task.FromResult<Papd?>(null);
    }

    public Task AddAsync(Engagement engagement)
    {
        return Task.FromException(new InvalidOperationException("Engagements cannot be created from the Dataverse backend."));
    }

    public Task UpdateAsync(Engagement engagement)
    {
        return Task.FromException(new InvalidOperationException("Engagements cannot be edited from the Dataverse backend."));
    }

    public Task DeleteAsync(int id)
    {
        return Task.FromException(new InvalidOperationException("Engagements cannot be deleted from the Dataverse backend."));
    }

    public Task DeleteDataAsync(int engagementId)
    {
        return Task.FromException(new InvalidOperationException("Engagement data cannot be deleted from the Dataverse backend."));
    }
}
