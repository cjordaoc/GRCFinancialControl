using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;

public sealed class StaffAllocationProcessor
{
    private readonly StaffAllocationWorksheetParser _parser;

    public StaffAllocationProcessor(StaffAllocationWorksheetParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public StaffAllocationProcessingResult Process(DataTable worksheet, IReadOnlyDictionary<string, Employee> employees)
    {
        var parseResult = _parser.Parse(worksheet, employees);
        var summary = StaffAllocationProcessingSummary.FromParseResult(parseResult);
        return new StaffAllocationProcessingResult(parseResult, summary);
    }
}

public sealed class StaffAllocationProcessingResult
{
    public StaffAllocationProcessingResult(StaffAllocationParseResult parseResult, StaffAllocationProcessingSummary summary)
    {
        ParseResult = parseResult ?? throw new ArgumentNullException(nameof(parseResult));
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
    }

    public StaffAllocationParseResult ParseResult { get; }

    public StaffAllocationProcessingSummary Summary { get; }
}

public sealed class StaffAllocationProcessingSummary
{
    public StaffAllocationProcessingSummary(
        int processedRowCount,
        int distinctEngagementCount,
        IReadOnlyCollection<string> distinctRanks)
    {
        ProcessedRowCount = processedRowCount;
        DistinctEngagementCount = distinctEngagementCount;
        DistinctRanks = distinctRanks ?? throw new ArgumentNullException(nameof(distinctRanks));
    }

    public int ProcessedRowCount { get; }

    public int DistinctEngagementCount { get; }

    public IReadOnlyCollection<string> DistinctRanks { get; }

    public int DistinctRankCount => DistinctRanks.Count;

    public static StaffAllocationProcessingSummary FromParseResult(StaffAllocationParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        var distinctEngagements = parseResult.Records
            .Select(r => r.EngagementCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var distinctRanks = parseResult.Records
            .Select(r => r.Rank?.Trim())
            .Where(rank => !string.IsNullOrWhiteSpace(rank))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(rank => rank!)
            .OrderBy(rank => rank, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new StaffAllocationProcessingSummary(
            parseResult.ProcessedRowCount,
            distinctEngagements,
            new ReadOnlyCollection<string>(distinctRanks));
    }
}
