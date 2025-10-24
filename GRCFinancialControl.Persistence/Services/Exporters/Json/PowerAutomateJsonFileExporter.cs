using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Exporters.Json;

public sealed class PowerAutomateJsonFileExporter : IPowerAutomateJsonFileExporter
{
    private const string OutputDirectory = "artifacts/output";
    private readonly IJsonExportService _jsonExportService;
    private readonly IPowerAutomateJsonPayloadBuilder _payloadBuilder;
    private readonly IPowerAutomateExportTelemetry _telemetry;
    private readonly ILogger<PowerAutomateJsonFileExporter> _logger;

    public PowerAutomateJsonFileExporter(
        IJsonExportService jsonExportService,
        IPowerAutomateJsonPayloadBuilder payloadBuilder,
        IPowerAutomateExportTelemetry telemetry,
        ILogger<PowerAutomateJsonFileExporter> logger)
    {
        _jsonExportService = jsonExportService ?? throw new ArgumentNullException(nameof(jsonExportService));
        _payloadBuilder = payloadBuilder ?? throw new ArgumentNullException(nameof(payloadBuilder));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ExportAsync(
        PowerAutomateJsonExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filters = request.Filters ?? PowerAutomateJsonExportFilters.Empty;
        var managerData = await _jsonExportService.LoadManagerEmailDataAsync(filters, cancellationToken);

        var totalManagers = managerData.Count;
        var managersWithData = managerData.Count(manager => manager.HasInvoices || manager.HasEtcs);
        var managersWithWarnings = managerData.Count(manager => !string.IsNullOrWhiteSpace(manager.WarningBodyHtml));

        _logger.LogInformation(
            "Generating Power Automate export: {TotalManagers} managers processed ({ManagersWithData} with data, {ManagersWithWarnings} with warnings).",
            totalManagers,
            managersWithData,
            managersWithWarnings);

        if (totalManagers == 0)
        {
            _logger.LogWarning("Power Automate export returned no manager data.");
        }

        var scheduledAt = request.ScheduledAt;

        var resolvedTimezone = string.IsNullOrWhiteSpace(request.Timezone)
            ? PowerAutomateJsonPayloadBuilder.DefaultTimezone
            : request.Timezone!;

        var resolvedLocale = string.IsNullOrWhiteSpace(request.Locale)
            ? PowerAutomateJsonPayloadBuilder.DefaultLocale
            : request.Locale!;

        var payload = _payloadBuilder.BuildPayload(managerData, scheduledAt, resolvedTimezone, resolvedLocale);

        using (JsonDocument.Parse(payload))
        {
            // Parsing validates the JSON structure before it is persisted to disk.
        }

        var outputPath = BuildOutputPath(scheduledAt);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, payload, Encoding.UTF8, cancellationToken);

        Console.WriteLine($"Power Automate JSON export created at: {outputPath}");
        Console.WriteLine($"Managers processed: {totalManagers}. With data: {managersWithData}. With warnings: {managersWithWarnings}.");

        if (managersWithWarnings > 0)
        {
            foreach (var manager in managerData.Where(manager => !string.IsNullOrWhiteSpace(manager.WarningBodyHtml)))
            {
                _logger.LogWarning(
                    "Manager {ManagerName} ({ManagerEmail}) exported with a warning body.",
                    manager.ManagerName,
                    manager.ManagerEmail);
            }
        }

        _telemetry.TrackExport(new PowerAutomateExportMetrics(
            scheduledAt,
            resolvedTimezone,
            resolvedLocale,
            totalManagers,
            managersWithData,
            managersWithWarnings,
            outputPath));

        return outputPath;
    }

    private static string BuildOutputPath(DateTimeOffset scheduledAt)
    {
        var dateFragment = scheduledAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return Path.Combine(OutputDirectory, $"Tasks_{dateFragment}.json");
    }
}
