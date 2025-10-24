using System;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Exporters.Json;

public sealed class LoggingPowerAutomateExportTelemetry : IPowerAutomateExportTelemetry
{
    private readonly ILogger<LoggingPowerAutomateExportTelemetry> _logger;

    public LoggingPowerAutomateExportTelemetry(ILogger<LoggingPowerAutomateExportTelemetry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void TrackExport(PowerAutomateExportMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        _logger.LogInformation(
            "Export telemetry — scheduledAt: {ScheduledAt:o}, timezone: {Timezone}, locale: {Locale}, totalManagers: {TotalManagers}, managersWithData: {ManagersWithData}, managersWithWarnings: {ManagersWithWarnings}, outputPath: {OutputPath}.",
            metrics.ScheduledAt,
            metrics.Timezone,
            metrics.Locale,
            metrics.TotalManagers,
            metrics.ManagersWithData,
            metrics.ManagersWithWarnings,
            metrics.OutputPath);
    }
}
