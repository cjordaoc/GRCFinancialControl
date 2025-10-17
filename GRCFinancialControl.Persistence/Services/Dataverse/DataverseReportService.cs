using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models.Reporting;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Placeholder Dataverse implementation for <see cref="IReportService"/>.
/// </summary>
public sealed class DataverseReportService : DataverseServiceBase, IReportService
{
    public DataverseReportService(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataverseReportService> logger)
        : base(repository, metadataRegistry, logger)
    {
    }

    public Task<List<PapdContributionData>> GetPapdContributionDataAsync()
    {
        Logger.LogWarning("PAPD contribution reports are not yet available through the Dataverse backend.");
        return Task.FromResult(new List<PapdContributionData>());
    }

    public Task<List<FinancialEvolutionPoint>> GetFinancialEvolutionPointsAsync(string engagementId)
    {
        Logger.LogWarning("Financial evolution data is not yet available through the Dataverse backend (EngagementId={EngagementId}).", engagementId);
        return Task.FromResult(new List<FinancialEvolutionPoint>());
    }
}
