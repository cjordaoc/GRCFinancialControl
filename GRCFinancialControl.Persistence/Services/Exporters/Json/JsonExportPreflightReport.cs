using System;
using System.Collections.Generic;
using System.Linq;

namespace GRCFinancialControl.Persistence.Services.Exporters.Json;

public sealed class JsonExportPreflightReport
{
    public JsonExportPreflightReport(
        IEnumerable<ModelCheckResult> modelChecks,
        IEnumerable<FieldCheckResult> fieldChecks)
    {
        ModelChecks = modelChecks?.ToArray() ?? Array.Empty<ModelCheckResult>();
        FieldChecks = fieldChecks?.ToArray() ?? Array.Empty<FieldCheckResult>();
    }

    public IReadOnlyList<ModelCheckResult> ModelChecks { get; }

    public IReadOnlyList<FieldCheckResult> FieldChecks { get; }

    public bool HasErrors =>
        ModelChecks.Any(result => !result.Exists) ||
        FieldChecks.Any(result => !result.Exists);
}

public sealed record ModelCheckResult(string ModelName, bool Exists);

public sealed record FieldCheckResult(string ModelName, string FieldName, bool Exists);
