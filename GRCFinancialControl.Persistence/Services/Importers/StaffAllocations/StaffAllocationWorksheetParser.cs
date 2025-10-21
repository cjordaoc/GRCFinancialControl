using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using GRCFinancialControl.Core.Models;
using Microsoft.Extensions.Logging;

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

    public StaffAllocationParseResult Parse(DataTable worksheet, IReadOnlyDictionary<string, Employee> employees)
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

        var records = new List<StaffAllocationTemporaryRecord>();
        var unknownAffiliations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skippedInactive = new List<StaffAllocationSkippedEntry>();
        var processedRows = 0;

        for (var rowIndex = schema.HeaderRowIndex + 1; rowIndex < worksheet.Rows.Count; rowIndex++)
        {
            var row = worksheet.Rows[rowIndex];
            if (IsDataRowEmpty(row, schema))
            {
                continue;
            }

            var gpn = GetTrimmedValue(row, schema.FixedColumns[StaffAllocationFixedColumn.Gpn].ColumnIndex);
            if (string.IsNullOrEmpty(gpn))
            {
                continue;
            }

            processedRows++;

            var employeeName = GetTrimmedValue(row, schema.FixedColumns[StaffAllocationFixedColumn.ResourceName].ColumnIndex);
            var rank = GetTrimmedValue(row, schema.FixedColumns[StaffAllocationFixedColumn.Rank].ColumnIndex);
            var office = GetTrimmedValue(row, schema.FixedColumns[StaffAllocationFixedColumn.Office].ColumnIndex);
            var subdomain = GetTrimmedValue(row, schema.FixedColumns[StaffAllocationFixedColumn.Subdomain].ColumnIndex);

            foreach (var weekColumn in schema.WeekColumns)
            {
                var cellValue = row[weekColumn.ColumnIndex];
                foreach (var engagementCode in ExtractEngagementCodes(cellValue))
                {
                    var resolvedEmployeeName = employeeName;
                    var isUnknownAffiliation = true;

                    if (employeeLookup.TryGetValue(gpn, out var employee))
                    {
                        if (IsEmployeeInactiveForWeek(employee, weekColumn.WeekStartMon))
                        {
                            skippedInactive.Add(new StaffAllocationSkippedEntry(
                                gpn,
                                employee.EmployeeName,
                                weekColumn.WeekStartMon));

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
                        weekColumn.WeekStartMon,
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

    private static bool IsDataRowEmpty(DataRow row, StaffAllocationSchemaAnalysis schema)
    {
        var hasFixedValues = schema.FixedColumns.Values
            .Select(column => row[column.ColumnIndex])
            .All(StaffAllocationCellHelper.IsBlank);

        if (!hasFixedValues)
        {
            return false;
        }

        return schema.WeekColumns
            .Select(column => row[column.ColumnIndex])
            .All(StaffAllocationCellHelper.IsBlank);
    }

    private static string GetTrimmedValue(DataRow row, int columnIndex)
    {
        return StaffAllocationCellHelper.GetDisplayText(row[columnIndex]).Trim();
    }

    private static IEnumerable<string> ExtractEngagementCodes(object? cellValue)
    {
        var rawText = StaffAllocationCellHelper.GetDisplayText(cellValue);
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
    bool IsUnknownAffiliation);

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
