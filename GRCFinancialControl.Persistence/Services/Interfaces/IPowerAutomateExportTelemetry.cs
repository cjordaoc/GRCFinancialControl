using GRCFinancialControl.Persistence.Services.Exporters.Json;

namespace GRCFinancialControl.Persistence.Services.Interfaces;

public interface IPowerAutomateExportTelemetry
{
    void TrackExport(PowerAutomateExportMetrics metrics);
}
