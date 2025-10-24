using System;
using System.Collections.Generic;
using GRCFinancialControl.Persistence.Services.Exporters.Json;

namespace GRCFinancialControl.Persistence.Services.Interfaces;

public interface IPowerAutomateJsonPayloadBuilder
{
    string BuildPayload(
        IReadOnlyCollection<ManagerEmailData> managerData,
        DateTimeOffset scheduledAt,
        string timezone,
        string locale);
}
