using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GRCFinancialControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;

public sealed class SimplifiedStaffAllocationParser
{
    private static readonly string[] DateFormats =
    {
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd/MM/yy",
        "d/M/yy"
    };

    private static readonly CultureInfo[] DateCultures =
    {
        CultureInfo.InvariantCulture,
        new("pt-BR")
    };

    private readonly ILogger<SimplifiedStaffAllocationParser> _logger;

    public SimplifiedStaffAllocationParser(ILogger<SimplifiedStaffAllocationParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<AggregatedAllocation> Parse(
        ImportService.IWorksheet worksheet,
        ClosingPeriod closingPeriod,
        IReadOnlyList<RankMapping> rankMappings)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(closingPeriod);
        ArgumentNullException.ThrowIfNull(rankMappings);

        if (worksheet.RowCount == 0 || worksheet.ColumnCount == 0)
        {
            return Array.Empty<AggregatedAllocation>();
        }

        var employees = CaptureEmployees(worksheet);
        if (employees.Count == 0)
        {
            _logger.LogWarning("No staff allocation rows found in the worksheet.");
            return Array.Empty<AggregatedAllocation>();
        }

        var weekColumns = CaptureWeekColumns(worksheet, closingPeriod);
        if (weekColumns.Count == 0)
        {
            _logger.LogWarning(
                "No weekly allocation columns matched closing period {ClosingPeriod} ({Start:yyyy-MM-dd} - {End:yyyy-MM-dd}).",
                closingPeriod.Name,
                closingPeriod.PeriodStart,
                closingPeriod.PeriodEnd);
            return Array.Empty<AggregatedAllocation>();
        }

        var rankLookup = BuildRankLookup(rankMappings);
        if (rankLookup.Count == 0)
        {
            _logger.LogWarning("Rank mappings are empty. Unable to map ranks to rank codes.");
            return Array.Empty<AggregatedAllocation>();
        }

        var records = new List<IntermediateRecord>(employees.Count * weekColumns.Count);

        foreach (var employee in employees.Values)
        {
            foreach (var week in weekColumns)
            {
                var cellValue = worksheet.GetValue(employee.RowIndex, week.ColumnIndex);
                var engagementCode = ExtractEngagementCode(cellValue);
                if (string.IsNullOrEmpty(engagementCode))
                {
                    continue;
                }

                records.Add(new IntermediateRecord(
                    employee.Gpn,
                    employee.Rank,
                    employee.EmployeeName,
                    employee.Office,
                    employee.Subdomain,
                    engagementCode,
                    week.WeekDate,
                    40m));
            }
        }

        if (records.Count == 0)
        {
            return Array.Empty<AggregatedAllocation>();
        }

        var totals = new Dictionary<AllocationKey, AggregatedAccumulator>(AllocationKeyComparer.Instance);
        foreach (var mapped in records.Select(record => MapRank(record, rankLookup)))
        {
            if (mapped is null)
            {
                continue;
            }

            var key = new AllocationKey(
                NormalizeCode(mapped.EngagementCode),
                NormalizeRank(mapped.RankCode),
                closingPeriod.FiscalYearId);

            if (totals.TryGetValue(key, out var accumulator))
            {
                totals[key] = accumulator with { Hours = accumulator.Hours + mapped.AmountOfHours };
            }
            else
            {
                totals[key] = new AggregatedAccumulator(
                    NormalizeCode(mapped.EngagementCode),
                    mapped.RankCode.Trim(),
                    closingPeriod.FiscalYearId,
                    mapped.AmountOfHours);
            }
        }

        if (totals.Count == 0)
        {
            return Array.Empty<AggregatedAllocation>();
        }

        return totals.Values
            .Select(value => new AggregatedAllocation(value.EngagementCode, value.RankCode, value.FiscalYearId, value.Hours))
            .ToList();
    }

    private static Dictionary<string, EmployeeRow> CaptureEmployees(ImportService.IWorksheet worksheet)
    {
        var rows = new Dictionary<string, EmployeeRow>(StringComparer.OrdinalIgnoreCase);
        var consecutiveBlanks = 0;

        for (var rowIndex = 1; rowIndex < worksheet.RowCount; rowIndex++)
        {
            var gpn = GetString(worksheet.GetValue(rowIndex, 0));
            if (string.IsNullOrWhiteSpace(gpn))
            {
                consecutiveBlanks++;
                if (consecutiveBlanks >= 3)
                {
                    break;
                }

                continue;
            }

            consecutiveBlanks = 0;

            rows[gpn] = new EmployeeRow(
                rowIndex,
                gpn,
                GetString(worksheet.GetValue(rowIndex, 2)),
                GetString(worksheet.GetValue(rowIndex, 3)),
                GetString(worksheet.GetValue(rowIndex, 4)),
                GetString(worksheet.GetValue(rowIndex, 5)));
        }

        return rows;
    }

    private static List<WeekColumn> CaptureWeekColumns(ImportService.IWorksheet worksheet, ClosingPeriod closingPeriod)
    {
        var result = new List<WeekColumn>();
        if (worksheet.RowCount == 0)
        {
            return result;
        }

        var start = closingPeriod.PeriodStart.Date;
        var end = closingPeriod.PeriodEnd.Date;

        for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
        {
            var date = TryParseWeekDate(worksheet.GetValue(0, columnIndex));
            if (!date.HasValue)
            {
                continue;
            }

            var weekDate = date.Value.Date;
            if (weekDate < start || weekDate > end)
            {
                continue;
            }

            result.Add(new WeekColumn(columnIndex, weekDate));
        }

        return result;
    }

    private static Dictionary<string, string> BuildRankLookup(IReadOnlyList<RankMapping> rankMappings)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in rankMappings.Where(m => m.IsActive))
        {
            if (!string.IsNullOrWhiteSpace(mapping.SpreadsheetRank))
            {
                lookup[NormalizeRank(mapping.SpreadsheetRank)] = mapping.RawRank.Trim();
            }

            if (!string.IsNullOrWhiteSpace(mapping.RawRank) && !lookup.ContainsKey(NormalizeRank(mapping.RawRank)))
            {
                lookup[NormalizeRank(mapping.RawRank)] = mapping.RawRank.Trim();
            }

            if (!string.IsNullOrWhiteSpace(mapping.NormalizedRank) && !lookup.ContainsKey(NormalizeRank(mapping.NormalizedRank)))
            {
                lookup[NormalizeRank(mapping.NormalizedRank)] = mapping.RawRank.Trim();
            }
        }

        return lookup;
    }

    private MappedRecord? MapRank(IntermediateRecord record, IReadOnlyDictionary<string, string> rankLookup)
    {
        var normalizedRank = NormalizeRank(record.Rank);
        if (string.IsNullOrEmpty(normalizedRank))
        {
            _logger.LogWarning(
                "Skipping allocation for employee {Gpn} on {WeekDate:yyyy-MM-dd} because the rank is empty.",
                record.Gpn,
                record.WeekDate);
            return null;
        }

        if (!rankLookup.TryGetValue(normalizedRank, out var rankCode))
        {
            _logger.LogWarning(
                "No mapping found for rank '{Rank}'. Skipping allocation for employee {Gpn} on {WeekDate:yyyy-MM-dd}.",
                record.Rank,
                record.Gpn,
                record.WeekDate);
            return null;
        }

        return new MappedRecord(
            record.Gpn,
            record.EmployeeName,
            record.Office,
            record.Subdomain,
            record.EngagementCode,
            record.WeekDate,
            record.AmountOfHours,
            rankCode);
    }

    private static DateTime? TryParseWeekDate(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is DateTime directDate)
        {
            return directDate.Date;
        }

        var text = GetString(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (var culture in DateCultures)
        {
            if (DateTime.TryParseExact(text, DateFormats, culture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                return parsed.Date;
            }
        }

        return null;
    }

    private static string? ExtractEngagementCode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string s)
        {
            var match = Regex.Match(s, "E-\\d+");
            return match.Success ? match.Value.ToUpperInvariant() : null;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var regexMatch = Regex.Match(text, "E-\\d+");
        return regexMatch.Success ? regexMatch.Value.ToUpperInvariant() : null;
    }

    private static string GetString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s.Trim(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture).Trim(),
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }

    private static string NormalizeRank(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string NormalizeCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private sealed record EmployeeRow(
        int RowIndex,
        string Gpn,
        string Rank,
        string EmployeeName,
        string Office,
        string Subdomain);

    private sealed record WeekColumn(int ColumnIndex, DateTime WeekDate);

    private sealed record IntermediateRecord(
        string Gpn,
        string Rank,
        string EmployeeName,
        string Office,
        string Subdomain,
        string EngagementCode,
        DateTime WeekDate,
        decimal AmountOfHours);

    private sealed record MappedRecord(
        string Gpn,
        string EmployeeName,
        string Office,
        string Subdomain,
        string EngagementCode,
        DateTime WeekDate,
        decimal AmountOfHours,
        string RankCode);

    private readonly record struct AllocationKey(string EngagementCode, string RankCode, int FiscalYearId);

    private readonly record struct AggregatedAccumulator(string EngagementCode, string RankCode, int FiscalYearId, decimal Hours);

    private sealed class AllocationKeyComparer : IEqualityComparer<AllocationKey>
    {
        public static AllocationKeyComparer Instance { get; } = new();

        public bool Equals(AllocationKey x, AllocationKey y)
        {
            return string.Equals(x.EngagementCode, y.EngagementCode, StringComparison.Ordinal) &&
                   string.Equals(x.RankCode, y.RankCode, StringComparison.Ordinal) &&
                   x.FiscalYearId == y.FiscalYearId;
        }

        public int GetHashCode(AllocationKey obj)
        {
            return HashCode.Combine(obj.EngagementCode, obj.RankCode, obj.FiscalYearId);
        }
    }

    public sealed record AggregatedAllocation(
        string EngagementCode,
        string RankCode,
        int FiscalYearId,
        decimal Hours);
}
