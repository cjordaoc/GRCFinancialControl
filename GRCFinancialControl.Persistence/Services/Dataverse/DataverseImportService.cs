using System;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Placeholder Dataverse implementation for <see cref="IImportService"/>.
/// </summary>
public sealed class DataverseImportService : DataverseServiceBase, IImportService
{
    public DataverseImportService(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataverseImportService> logger)
        : base(repository, metadataRegistry, logger)
    {
    }

    public Task<string> ImportBudgetAsync(string filePath)
    {
        return Task.FromException<string>(new InvalidOperationException("Budget imports are not available when using the Dataverse backend."));
    }

    public Task<string> ImportActualsAsync(string filePath, int closingPeriodId)
    {
        return Task.FromException<string>(new InvalidOperationException("Actuals imports are not available when using the Dataverse backend."));
    }

    public Task<string> ImportFcsRevenueBacklogAsync(string filePath)
    {
        return Task.FromException<string>(new InvalidOperationException("FCS backlog imports are not available when using the Dataverse backend."));
    }

    public Task<string> ImportFullManagementDataAsync(string filePath)
    {
        return Task.FromException<string>(new InvalidOperationException("Full management data imports are not available when using the Dataverse backend."));
    }
}
