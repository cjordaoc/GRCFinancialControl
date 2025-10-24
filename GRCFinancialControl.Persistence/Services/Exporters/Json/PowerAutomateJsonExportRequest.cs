using System;

namespace GRCFinancialControl.Persistence.Services.Exporters.Json;

public sealed class PowerAutomateJsonExportRequest
{
    public DateTimeOffset ScheduledAt { get; init; } = DateTimeOffset.Now;

    public string? Timezone { get; init; }

    public string? Locale { get; init; }

    public PowerAutomateJsonExportFilters Filters { get; init; } = PowerAutomateJsonExportFilters.Empty;
}
