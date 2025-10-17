using System;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Dataverse;

/// <summary>
/// Placeholder Dataverse implementation for <see cref="IFullManagementDataImporter"/>.
/// </summary>
public sealed class DataverseFullManagementDataImporter : DataverseServiceBase, IFullManagementDataImporter
{
    public DataverseFullManagementDataImporter(
        IDataverseRepository repository,
        DataverseEntityMetadataRegistry metadataRegistry,
        ILogger<DataverseFullManagementDataImporter> logger)
        : base(repository, metadataRegistry, logger)
    {
    }

    public Task<FullManagementDataImportResult> ImportAsync(string filePath)
    {
        return Task.FromException<FullManagementDataImportResult>(new InvalidOperationException("Full management data imports are not available when using the Dataverse backend."));
    }
}
