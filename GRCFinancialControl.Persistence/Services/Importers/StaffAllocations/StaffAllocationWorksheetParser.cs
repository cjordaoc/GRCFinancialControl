using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using GRCFinancialControl.Core.Models;
using Microsoft.Extensions.Logging;
using GRCFinancialControl.Persistence.Services;
using static GRCFinancialControl.Persistence.Services.Importers.WorksheetValueHelper;

namespace GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;

public sealed class StaffAllocationWorksheetParser
{
    private static readonly char[] EngagementSeparators = ['\n', '\r', ';', ',', '|', ' '];
    private const decimal DefaultWeekHours = 40m;

    private readonly StaffAllocationSchemaAnalyzer _schemaAnalyzer;
    private readonly ILogger<StaffAllocationWorksheetParser> _logger;

    public StaffAllocationWorksheetParser(
        StaffAllocationSchemaAnalyzer schemaAnalyzer,
        ILogger<StaffAllocationWorksheetParser> logger)
    {
        _schemaAnalyzer = schemaAnalyzer ?? throw new ArgumentNullException(nameof(schemaAnalyzer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public StaffAllocationParseResult Parse(
        ImportService.IWorksheet worksheet,
        IReadOnlyDictionary<string, Employee> employees,
        DateTime uploadTimestamp)
    {
        if (worksheet == null)
        {
            throw new ArgumentNullException(nameof(worksheet));
        }

        if (employees == null)
        {
            throw new ArgumentNullException(nameof(employees));
        }

        var employeeLookup = BuildEmployeeLookup(employees);
        var schema = _schemaAnalyzer.Analyze(worksheet);
        var minimumWeekStart = NormalizeWeekStart(uploadTimestamp);

        var estimatedRowCount = Math.Max(0, worksheet.RowCount - (schema.HeaderRowIndex + 1));
        var estimatedAllocationCount = Math.Max(1, schema.WeekColumns.Count) * Math.Max(1, estimatedRowCount);
        var records = new List<StaffAllocationTemporaryRecord>(estimatedAllocationCount);
        var unknownAffiliations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        unknownAffiliations.EnsureCapacity(Math.Max(4, estimatedRowCount));
        var skippedInactive = new List<StaffAllocationSkippedEntry>(estimatedRowCount);
        var processedRows = 0;

        for (var rowIndex = schema.HeaderRowIndex + 1; rowIndex < worksheet.RowCount; rowIndex++)
        {
            if (IsRowEmpty(worksheet, rowIndex, schema))
            {
                continue;
            }

            var gpn = GetTrimmedValue(worksheet, rowIndex, schema.FixedColumns[StaffAllocationFixedColumn.Gpn].ColumnIndex);
            if (string.IsNullOrEmpty(gpn))
            {
                continue;
            }

            processedRows++;

            var employeeName = GetTrimmedValue(worksheet, rowIndex, schema.FixedColumns[StaffAllocationFixedColumn.ResourceName].ColumnIndex);
            var rank = GetTrimmedValue(worksheet, rowIndex, schema.FixedColumns[StaffAllocationFixedColumn.Rank].ColumnIndex);
            var office = GetTrimmedValue(worksheet, rowIndex, schema.FixedColumns[StaffAllocationFixedColumn.Office].ColumnIndex);
            var subdomain = GetTrimmedValue(worksheet, rowIndex, schema.FixedColumns[StaffAllocationFixedColumn.Subdomain].ColumnIndex);

            foreach (var weekColumn in schema.WeekColumns)
            {
                var normalizedWeekStart = NormalizeWeekStart(weekColumn.WeekStartMon);

                if (normalizedWeekStart < minimumWeekStart)
                {
                    continue;
                }

                var cellValue = worksheet.GetValue(rowIndex, weekColumn.ColumnIndex);
                foreach (var engagementCode in ExtractEngagementCodes(cellValue))
                {
                    var resolvedEmployeeName = employeeName;
                    var isUnknownAffiliation = true;

                    if (employeeLookup.TryGetValue(gpn, out var employee))
                    {
                        if (IsEmployeeInactiveForWeek(employee, normalizedWeekStart))
                        {
                            skippedInactive.Add(new StaffAllocationSkippedEntry(
                                gpn,
                                employee.EmployeeName,
                                normalizedWeekStart));

                            _logger.LogInformation(
                                "Skipping staff allocation for inactive employee {Gpn} ({EmployeeName}) on week {WeekStart}.",
                                gpn,
                                employee.EmployeeName,
                                weekColumn.WeekStartMon.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(employeeName))
                        {
                            employeeName = employee.EmployeeName;
                        }

                        resolvedEmployeeName = employeeName;
                        isUnknownAffiliation = false;
                    }
                    else
                    {
                        unknownAffiliations.Add(gpn);
                    }

                    records.Add(new StaffAllocationTemporaryRecord(
                        gpn,
                        resolvedEmployeeName,
                        rank,
                        engagementCode,
                        normalizedWeekStart,
                        DefaultWeekHours,
                        office,
                        subdomain,
                        isUnknownAffiliation));
                }
            }
        }

        return new StaffAllocationParseResult(
            new ReadOnlyCollection<StaffAllocationTemporaryRecord>(records),
            new ReadOnlyCollection<string>(unknownAffiliations.ToList()),
            new ReadOnlyCollection<StaffAllocationSkippedEntry>(skippedInactive),
            processedRows);
    }

    private static Dictionary<string, Employee> BuildEmployeeLookup(IReadOnlyDictionary<string, Employee> employees)
    {
        var lookup = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
        lookup.EnsureCapacity(employees.Count);
        foreach (var (key, value) in employees)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            lookup[key.Trim()] = value;
        }

        return lookup;
    }

    private static bool IsRowEmpty(ImportService.IWorksheet worksheet, int rowIndex, StaffAllocationSchemaAnalysis schema)
    {
        foreach (var column in schema.FixedColumns.Values)
        {
            if (!IsBlank(worksheet.GetValue(rowIndex, column.ColumnIndex)))
            {
                return false;
            }
        }

        foreach (var column in schema.WeekColumns)
        {
            if (!IsBlank(worksheet.GetValue(rowIndex, column.ColumnIndex)))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetTrimmedValue(ImportService.IWorksheet worksheet, int rowIndex, int columnIndex)
    {
        return GetDisplayText(worksheet.GetValue(rowIndex, columnIndex)).Trim();
    }

    private static IEnumerable<string> ExtractEngagementCodes(object? cellValue)
    {
        var rawText = GetDisplayText(cellValue);
        if (string.IsNullOrWhiteSpace(rawText))
        {
            yield break;
        }

        var trimmed = rawText.Trim();
        if (!trimmed.StartsWith("E-", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var segments = trimmed.Split(EngagementSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            segments = new[] { trimmed };
        }

        foreach (var segment in segments)
        {
            var candidate = segment.Trim();
            if (!candidate.StartsWith("E-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var code = ExtractToken(candidate);
            if (code.Length > 0)
            {
                yield return code;
            }
        }
    }

    private static string ExtractToken(string text)
    {
        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch) || ch == '/' || ch == '\\' || ch == '(')
            {
                break;
            }

            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            {
                builder.Append(char.ToUpperInvariant(ch));
                continue;
            }

            break;
        }

        return builder.ToString();
    }

    private static bool IsEmployeeInactiveForWeek(Employee employee, DateTime weekStart)
    {
        if (employee.StartDate.HasValue && employee.StartDate.Value.Date > weekStart)
        {
            return true;
        }

        if (employee.EndDate.HasValue && employee.EndDate.Value.Date < weekStart)
        {
            return true;
        }

        return false;
    }
}

public sealed record StaffAllocationTemporaryRecord(
    string Gpn,
    string EmployeeName,
    string Rank,
    string EngagementCode,
    DateTime WeekStartMon,
    decimal Hours,
    string Office,
    string Subdomain,
    bool IsUnknownAffiliation,
    int? FiscalYearId = null,
    int? ClosingPeriodId = null);

public sealed class StaffAllocationParseResult
{
    public StaffAllocationParseResult(
        IReadOnlyList<StaffAllocationTemporaryRecord> records,
        IReadOnlyCollection<string> unknownAffiliations,
        IReadOnlyCollection<StaffAllocationSkippedEntry> skippedInactiveEmployees,
        int processedRowCount)
    {
        Records = records ?? throw new ArgumentNullException(nameof(records));
        UnknownAffiliations = unknownAffiliations ?? throw new ArgumentNullException(nameof(unknownAffiliations));
        SkippedInactiveEmployees = skippedInactiveEmployees ?? throw new ArgumentNullException(nameof(skippedInactiveEmployees));
        ProcessedRowCount = processedRowCount;
    }

    public IReadOnlyList<StaffAllocationTemporaryRecord> Records { get; }

    public IReadOnlyCollection<string> UnknownAffiliations { get; }

    public IReadOnlyCollection<StaffAllocationSkippedEntry> SkippedInactiveEmployees { get; }

    public int ProcessedRowCount { get; }
}

public sealed record StaffAllocationSkippedEntry(string Gpn, string EmployeeName, DateTime WeekStartMon);
