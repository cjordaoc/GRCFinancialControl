using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Placeholder Dataverse implementation for <see cref="IPapdService"/>.
/// </summary>
public sealed class DataversePapdService : DataverseServiceBase, IPapdService
{
    public DataversePapdService(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataversePapdService> logger)
        : base(repository, metadataRegistry, logger)
    {
    }

    public Task<List<Papd>> GetAllAsync()
    {
        Logger.LogWarning("PAPD records are not yet available through the Dataverse backend.");
        return Task.FromResult(new List<Papd>());
    }

    public Task AddAsync(Papd papd)
    {
        return Task.FromException(new InvalidOperationException("PAPD records cannot be created from the Dataverse backend."));
    }

    public Task UpdateAsync(Papd papd)
    {
        return Task.FromException(new InvalidOperationException("PAPD records cannot be edited from the Dataverse backend."));
    }

    public Task DeleteAsync(int id)
    {
        return Task.FromException(new InvalidOperationException("PAPD records cannot be deleted from the Dataverse backend."));
    }

    public Task DeleteDataAsync(int papdId)
    {
        return Task.FromException(new InvalidOperationException("PAPD data cannot be deleted from the Dataverse backend."));
    }
}
