using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Importers;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;

public sealed class SimplifiedStaffAllocationParser
{
    private const decimal HoursPerEngagementWeek = 40m;

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

        var employees = IdentifyEmployees(worksheet);
        if (employees.Rows.Count == 0)
        {
            _logger.LogWarning("No staff allocation rows found in the worksheet.");
            return Array.Empty<AggregatedAllocation>();
        }

        var weekColumns = IdentifyWeekColumns(worksheet, closingPeriod);
        if (weekColumns.Active.Count == 0)
        {
            _logger.LogWarning(
                "No weekly allocation columns matched closing period {ClosingPeriod} ({Start:yyyy-MM-dd} - {End:yyyy-MM-dd}). {TotalColumns} weekly columns were detected overall.",
                closingPeriod.Name,
                closingPeriod.PeriodStart,
                closingPeriod.PeriodEnd,
                weekColumns.All.Count);
            return Array.Empty<AggregatedAllocation>();
        }

        var rankLookup = BuildRankLookup(rankMappings);
        if (rankLookup.Count == 0)
        {
            _logger.LogWarning("Rank mappings are empty. Unable to map ranks to rank codes.");
            return Array.Empty<AggregatedAllocation>();
        }

        var records = ExtractAllocations(worksheet, employees.Rows, weekColumns.Active);
        if (records.Count == 0)
        {
            return Array.Empty<AggregatedAllocation>();
        }

        var aggregated = Aggregate(records, closingPeriod.FiscalYearId, rankLookup);
        if (aggregated.Count == 0)
        {
            return Array.Empty<AggregatedAllocation>();
        }

        return aggregated;
    }

    public IReadOnlyList<AggregatedAllocation> ParseWithFiscalYearMapping(
        ImportService.IWorksheet worksheet,
        IReadOnlyList<FiscalYear> fiscalYears,
        IReadOnlyList<RankMapping> rankMappings)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(fiscalYears);
        ArgumentNullException.ThrowIfNull(rankMappings);

        if (worksheet.RowCount == 0 || worksheet.ColumnCount == 0)
        {
            return Array.Empty<AggregatedAllocation>();
        }

        var employees = IdentifyEmployees(worksheet);
        if (employees.Rows.Count == 0)
        {
            _logger.LogWarning("No staff allocation rows found in the worksheet.");
            return Array.Empty<AggregatedAllocation>();
        }

        var weekColumns = IdentifyWeekColumnsWithFiscalYearMapping(worksheet, fiscalYears);
        if (weekColumns.Active.Count == 0)
        {
            _logger.LogWarning("No weekly allocation columns found in the worksheet.");
            return Array.Empty<AggregatedAllocation>();
        }

        var rankLookup = BuildRankLookup(rankMappings);
        if (rankLookup.Count == 0)
        {
            _logger.LogWarning("Rank mappings are empty. Unable to map ranks to rank codes.");
            return Array.Empty<AggregatedAllocation>();
        }

        var records = ExtractAllocations(worksheet, employees.Rows, weekColumns.Active);
        if (records.Count == 0)
        {
            return Array.Empty<AggregatedAllocation>();
        }

        // Aggregate by engagement, rank, and fiscal year (mapping each date to its fiscal year)
        var mapped = new List<MappedAllocation>(records.Count);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Rank))
            {
                _logger.LogWarning(
                    "Skipping allocation for employee {Gpn} on {WeekDate:yyyy-MM-dd} because the rank is empty.",
                    record.Gpn,
                    record.WeekDate);
                continue;
            }

            var normalizedRank = NormalizeRank(record.Rank);
            if (!rankLookup.TryGetValue(normalizedRank, out var rankCode))
            {
                _logger.LogWarning(
                    "No mapping found for rank '{Rank}'. Skipping allocation for employee {Gpn} on {WeekDate:yyyy-MM-dd}.",
                    record.Rank,
                    record.Gpn,
                    record.WeekDate);
                continue;
            }

            var normalizedEngagement = NormalizeCode(record.EngagementCode);
            if (string.IsNullOrEmpty(normalizedEngagement))
            {
                continue;
            }

            var normalizedRankCode = NormalizeCode(rankCode);
            if (string.IsNullOrEmpty(normalizedRankCode))
            {
                continue;
            }

            // Map week date to fiscal year
            var fiscalYear = fiscalYears.FirstOrDefault(fy =>
                record.WeekDate >= fy.StartDate.Date && record.WeekDate <= fy.EndDate.Date);

            if (fiscalYear is null)
            {
                _logger.LogWarning(
                    "Week date {WeekDate:yyyy-MM-dd} does not fall within any fiscal year. Skipping allocation for engagement {Engagement}.",
                    record.WeekDate,
                    normalizedEngagement);
                continue;
            }

            var key = new AllocationKey(normalizedEngagement, normalizedRankCode, fiscalYear.Id);
            mapped.Add(new MappedAllocation(key, record.AmountOfHours));
        }

        if (mapped.Count == 0)
        {
            return new List<AggregatedAllocation>();
        }

        return mapped
            .GroupBy(allocation => allocation.Key, AllocationKeyComparer.Instance)
            .Select(group => new AggregatedAllocation(
                group.Key.EngagementCode,
                group.Key.RankCode,
                group.Key.FiscalYearId,
                group.Sum(allocation => allocation.Hours)))
            .ToList();
    }

    private static EmployeeCapture IdentifyEmployees(ImportService.IWorksheet worksheet)
    {
        var rows = new List<EmployeeRow>();
        var lookup = new Dictionary<string, EmployeeRow>(StringComparer.OrdinalIgnoreCase);
        var rowIndices = new List<int>();
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

            var row = new EmployeeRow(
                rowIndex,
                gpn,
                GetString(worksheet.GetValue(rowIndex, 1)),
                GetString(worksheet.GetValue(rowIndex, 2)),
                GetString(worksheet.GetValue(rowIndex, 3)),
                GetString(worksheet.GetValue(rowIndex, 4)),
                GetString(worksheet.GetValue(rowIndex, 5)));

            rows.Add(row);
            lookup[gpn] = row;
            rowIndices.Add(rowIndex);
        }

        return new EmployeeCapture(rows, lookup, rowIndices);
    }

    private static WeekColumnCapture IdentifyWeekColumns(ImportService.IWorksheet worksheet, ClosingPeriod closingPeriod)
    {
        var allColumns = new List<WeekColumn>();
        var activeColumns = new List<WeekColumn>();
        if (worksheet.RowCount == 0)
        {
            return new WeekColumnCapture(allColumns, activeColumns);
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
            var column = new WeekColumn(columnIndex, weekDate);
            allColumns.Add(column);

            if (weekDate < start || weekDate > end)
            {
                continue;
            }

            activeColumns.Add(column);
        }

        return new WeekColumnCapture(allColumns, activeColumns);
    }

    private static WeekColumnCapture IdentifyWeekColumnsWithFiscalYearMapping(
        ImportService.IWorksheet worksheet,
        IReadOnlyList<FiscalYear> fiscalYears)
    {
        var allColumns = new List<WeekColumn>();
        var activeColumns = new List<WeekColumn>();
        if (worksheet.RowCount == 0)
        {
            return new WeekColumnCapture(allColumns, activeColumns);
        }

        var consecutiveBlanks = 0;
        const int maxConsecutiveBlanks = 5;

        for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
        {
            var cellValue = worksheet.GetValue(0, columnIndex);
            var date = TryParseWeekDate(cellValue);

            if (!date.HasValue)
            {
                // Check if cell is blank
                if (IsBlank(cellValue))
                {
                    consecutiveBlanks++;
                    if (consecutiveBlanks >= maxConsecutiveBlanks)
                    {
                        break; // Stop after 5 consecutive blank cells
                    }
                }
                else
                {
                    consecutiveBlanks = 0; // Reset counter if non-blank non-date found
                }
                continue;
            }

            consecutiveBlanks = 0; // Reset counter when date found

            var weekDate = date.Value.Date;
            var column = new WeekColumn(columnIndex, weekDate);
            allColumns.Add(column);
            activeColumns.Add(column); // Include all date columns, not filtered by current date
        }

        return new WeekColumnCapture(allColumns, activeColumns);
    }

    private static bool IsBlank(object? value)
    {
        if (value == null || value == DBNull.Value)
        {
            return true;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        return false;
    }

    private List<StaffAllocationRecord> ExtractAllocations(
        ImportService.IWorksheet worksheet,
        IReadOnlyList<EmployeeRow> employees,
        IReadOnlyList<WeekColumn> weekColumns)
    {
        var records = new List<StaffAllocationRecord>(employees.Count * weekColumns.Count);

        foreach (var employee in employees)
        {
            foreach (var week in weekColumns)
            {
                var engagementCode = TryExtractEngagementCode(worksheet.GetValue(employee.RowIndex, week.ColumnIndex));
                if (string.IsNullOrEmpty(engagementCode))
                {
                    continue;
                }

                records.Add(new StaffAllocationRecord(
                    employee.Gpn,
                    employee.Rank,
                    employee.EmployeeName,
                    employee.Office,
                    employee.Subdomain,
                    engagementCode,
                    week.WeekDate,
                    HoursPerEngagementWeek));
            }
        }

        return records;
    }

    private List<AggregatedAllocation> Aggregate(
        IReadOnlyList<StaffAllocationRecord> records,
        int fiscalYearId,
        IReadOnlyDictionary<string, string> rankLookup)
    {
        var mapped = new List<MappedAllocation>(records.Count);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Rank))
            {
                _logger.LogWarning(
                    "Skipping allocation for employee {Gpn} on {WeekDate:yyyy-MM-dd} because the rank is empty.",
                    record.Gpn,
                    record.WeekDate);
                continue;
            }

            var normalizedRank = NormalizeRank(record.Rank);
            if (!rankLookup.TryGetValue(normalizedRank, out var rankCode))
            {
                _logger.LogWarning(
                    "No mapping found for rank '{Rank}'. Skipping allocation for employee {Gpn} on {WeekDate:yyyy-MM-dd}.",
                    record.Rank,
                    record.Gpn,
                    record.WeekDate);
                continue;
            }

            var normalizedEngagement = NormalizeCode(record.EngagementCode);
            if (string.IsNullOrEmpty(normalizedEngagement))
            {
                continue;
            }

            var normalizedRankCode = NormalizeCode(rankCode);
            if (string.IsNullOrEmpty(normalizedRankCode))
            {
                continue;
            }

            var key = new AllocationKey(normalizedEngagement, normalizedRankCode, fiscalYearId);
            mapped.Add(new MappedAllocation(key, record.AmountOfHours));
        }

        if (mapped.Count == 0)
        {
            return new List<AggregatedAllocation>();
        }

        return mapped
            .GroupBy(allocation => allocation.Key, AllocationKeyComparer.Instance)
            .Select(group => new AggregatedAllocation(
                group.Key.EngagementCode,
                group.Key.RankCode,
                group.Key.FiscalYearId,
                group.Sum(allocation => allocation.Hours)))
            .ToList();
    }

    private static Dictionary<string, string> BuildRankLookup(IReadOnlyList<RankMapping> rankMappings)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in rankMappings.Where(m => m.IsActive))
        {
            var rankCode = NormalizeCode(mapping.RawRank);
            if (string.IsNullOrEmpty(rankCode))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(mapping.SpreadsheetRank))
            {
                lookup[NormalizeRank(mapping.SpreadsheetRank)] = rankCode;
            }

            if (!string.IsNullOrWhiteSpace(mapping.RawRank) && !lookup.ContainsKey(NormalizeRank(mapping.RawRank)))
            {
                lookup[NormalizeRank(mapping.RawRank)] = rankCode;
            }

            if (!string.IsNullOrWhiteSpace(mapping.NormalizedRank) && !lookup.ContainsKey(NormalizeRank(mapping.NormalizedRank)))
            {
                lookup[NormalizeRank(mapping.NormalizedRank)] = rankCode;
            }
        }

        return lookup;
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

    private static string? TryExtractEngagementCode(object? value)
    {
        return WorksheetValueHelper.TryExtractEngagementCode(value);
    }

    private static string GetString(object? value)
    {
        return WorksheetValueHelper.GetString(value);
    }

    private static string NormalizeRank(string? value)
    {
        return WorksheetValueHelper.NormalizeToUpperInvariant(value);
    }

    private static string NormalizeCode(string? value)
    {
        return WorksheetValueHelper.NormalizeToUpperInvariant(value);
    }

    private sealed record EmployeeRow(
        int RowIndex,
        string Gpn,
        string Utilization,
        string Rank,
        string EmployeeName,
        string Office,
        string Subdomain);

    private sealed record WeekColumn(int ColumnIndex, DateTime WeekDate);

    private sealed record WeekColumnCapture(
        IReadOnlyList<WeekColumn> All,
        IReadOnlyList<WeekColumn> Active);

    private sealed record StaffAllocationRecord(
        string Gpn,
        string Rank,
        string EmployeeName,
        string Office,
        string Subdomain,
        string EngagementCode,
        DateTime WeekDate,
        decimal AmountOfHours);

    private readonly record struct AllocationKey(string EngagementCode, string RankCode, int FiscalYearId);

    private readonly record struct MappedAllocation(AllocationKey Key, decimal Hours);

    private sealed record EmployeeCapture(
        IReadOnlyList<EmployeeRow> Rows,
        IReadOnlyDictionary<string, EmployeeRow> Lookup,
        IReadOnlyList<int> RowIndices);

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
