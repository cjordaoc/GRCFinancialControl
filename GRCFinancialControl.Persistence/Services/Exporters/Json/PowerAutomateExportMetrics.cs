using System;

namespace GRCFinancialControl.Persistence.Services.Exporters.Json;

public sealed record PowerAutomateExportMetrics(
    DateTimeOffset ScheduledAt,
    string Timezone,
    string Locale,
    int TotalManagers,
    int ManagersWithData,
    int ManagersWithWarnings,
    string OutputPath);
