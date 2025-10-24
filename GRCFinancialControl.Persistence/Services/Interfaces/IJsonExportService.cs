using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Exporters.Json;

namespace GRCFinancialControl.Persistence.Services.Interfaces;

public interface IJsonExportService
{
    Task<IReadOnlyCollection<ManagerEmailData>> LoadManagerEmailDataAsync(
        PowerAutomateJsonExportFilters filters,
        CancellationToken cancellationToken = default);
}
