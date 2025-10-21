using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
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

    public StaffAllocationProcessingResult Process(
        DataTable worksheet,
        IReadOnlyDictionary<string, Employee> employees,
        DateTime uploadTimestamp,
        IReadOnlyList<FiscalYear> fiscalYears,
        IReadOnlyList<ClosingPeriod> closingPeriods)
    {
        var parseResult = _parser.Parse(worksheet, employees, uploadTimestamp);
        var mappedRecords = MapToFiscalCalendar(parseResult.Records, fiscalYears, closingPeriods);
        var summary = StaffAllocationProcessingSummary.FromParseResult(parseResult);
        return new StaffAllocationProcessingResult(parseResult, summary, mappedRecords);
    }

    private static IReadOnlyList<StaffAllocationTemporaryRecord> MapToFiscalCalendar(
        IReadOnlyList<StaffAllocationTemporaryRecord> records,
        IReadOnlyList<FiscalYear> fiscalYears,
        IReadOnlyList<ClosingPeriod> closingPeriods)
    {
        if (records == null)
        {
            throw new ArgumentNullException(nameof(records));
        }

        if (fiscalYears == null)
        {
            throw new ArgumentNullException(nameof(fiscalYears));
        }

        if (closingPeriods == null)
        {
            throw new ArgumentNullException(nameof(closingPeriods));
        }

        if (records.Count == 0)
        {
            return Array.Empty<StaffAllocationTemporaryRecord>();
        }

        var orderedFiscalYears = fiscalYears
            .OrderBy(fy => fy.StartDate)
            .ToList();

        var periodsByFiscalYear = closingPeriods
            .GroupBy(cp => cp.FiscalYearId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(cp => cp.PeriodStart)
                    .ToList());

        var mapped = new List<StaffAllocationTemporaryRecord>(records.Count);

        foreach (var record in records)
        {
            var normalizedWeekStart = StaffAllocationCellHelper.NormalizeWeekStart(record.WeekStartMon);
            var fiscalYear = orderedFiscalYears
                .FirstOrDefault(fy => normalizedWeekStart >= fy.StartDate.Date && normalizedWeekStart <= fy.EndDate.Date);

            if (fiscalYear is null)
            {
                throw new InvalidDataException($"Week {normalizedWeekStart:yyyy-MM-dd} does not fall within any configured fiscal year.");
            }

            if (!periodsByFiscalYear.TryGetValue(fiscalYear.Id, out var fiscalYearPeriods) || fiscalYearPeriods.Count == 0)
            {
                throw new InvalidDataException($"Fiscal year '{fiscalYear.Name}' does not have closing periods configured.");
            }

            var closingPeriod = fiscalYearPeriods
                .FirstOrDefault(cp => normalizedWeekStart >= cp.PeriodStart.Date && normalizedWeekStart <= cp.PeriodEnd.Date);

            if (closingPeriod is null)
            {
                throw new InvalidDataException(
                    $"Week {normalizedWeekStart:yyyy-MM-dd} is not covered by any closing period in fiscal year '{fiscalYear.Name}'.");
            }

            mapped.Add(record with
            {
                WeekStartMon = normalizedWeekStart,
                FiscalYearId = fiscalYear.Id,
                ClosingPeriodId = closingPeriod.Id
            });
        }

        var mappedRecords = new ReadOnlyCollection<StaffAllocationTemporaryRecord>(mapped);
        ValidateMappingCompleteness(mappedRecords);
        return mappedRecords;
    }

    private static void ValidateMappingCompleteness(IReadOnlyCollection<StaffAllocationTemporaryRecord> mappedRecords)
    {
        if (mappedRecords.Count == 0)
        {
            return;
        }

        var unmapped = mappedRecords
            .Where(record => record.FiscalYearId is null || record.ClosingPeriodId is null)
            .ToList();

        if (unmapped.Count == 0)
        {
            return;
        }

        var sample = string.Join(", ", unmapped
            .Take(3)
            .Select(record => $"{record.EngagementCode} @ {record.WeekStartMon:yyyy-MM-dd}"));

        throw new InvalidDataException($"{unmapped.Count} staff allocation(s) are missing fiscal calendar mapping. Sample: {sample}.");
    }
}

public sealed class StaffAllocationProcessingResult
{
    public StaffAllocationProcessingResult(
        StaffAllocationParseResult parseResult,
        StaffAllocationProcessingSummary summary,
        IReadOnlyList<StaffAllocationTemporaryRecord> mappedRecords,
        StaffAllocationForecastUpdateResult? forecastSummary = null)
    {
        ParseResult = parseResult ?? throw new ArgumentNullException(nameof(parseResult));
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        MappedRecords = mappedRecords ?? throw new ArgumentNullException(nameof(mappedRecords));
        ForecastSummary = forecastSummary;
    }

    public StaffAllocationParseResult ParseResult { get; }

    public StaffAllocationProcessingSummary Summary { get; }

    public IReadOnlyList<StaffAllocationTemporaryRecord> MappedRecords { get; }

    public StaffAllocationForecastUpdateResult? ForecastSummary { get; }
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
