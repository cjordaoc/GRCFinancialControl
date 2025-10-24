using System.Threading;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Exporters.Json;

namespace GRCFinancialControl.Persistence.Services.Interfaces;

public interface IPowerAutomateJsonFileExporter
{
    Task<string> ExportAsync(
        PowerAutomateJsonExportRequest request,
        CancellationToken cancellationToken = default);
}
