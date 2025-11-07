using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using GRCFinancialControl.Persistence.Services.Utilities;
using static GRCFinancialControl.Persistence.Services.Utilities.DataNormalizationService;

namespace GRCFinancialControl.Persistence.Services.Exporters;

/// <summary>
/// Loads and parses planning workbooks for Retain template generation.
/// Refactored to use DataNormalizationService - eliminates duplicate code.
/// </summary>
internal static class RetainTemplatePlanningWorkbook
{
    private const decimal DefaultWeeklyHours = 40m;

    private static readonly string[] ResourceNameHeaders =
    {
        "Recursos",
        "Resource",
        "Resource Name",
        "Emp Resource Name",
        "Emp Resource  Name"
    };

    private static readonly string[] ResourceIdHeaders =
    {
        "GPN",
        "Emp Resource  GPN",
        "Resource Gpn",
        "Resource GPN"
    };

    private static bool _encodingRegistered;
    private static readonly object EncodingLock = new();

    public static RetainTemplatePlanningSnapshot Load(string filePath, DateTime referenceDate)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var worksheet = LoadWorksheet(filePath);
        if (worksheet.RowCount == 0 || worksheet.ColumnCount == 0)
        {
            return new RetainTemplatePlanningSnapshot(StartOfWeek(referenceDate), null, Array.Empty<RetainTemplatePlanningEntry>());
        }

        var headerRowIndex = FindHeaderRowIndex(worksheet);
        if (headerRowIndex < 0)
        {
            return new RetainTemplatePlanningSnapshot(StartOfWeek(referenceDate), null, Array.Empty<RetainTemplatePlanningEntry>());
        }

        var referenceWeekStart = StartOfWeek(referenceDate);
        var resourceIdColumnIndex = FindColumnIndex(worksheet, headerRowIndex, ResourceIdHeaders);
        var resourceNameColumnIndex = FindColumnIndex(worksheet, headerRowIndex, ResourceNameHeaders);
        var weekColumns = IdentifyWeekColumns(worksheet, headerRowIndex);
        if (weekColumns.Count == 0)
        {
            return new RetainTemplatePlanningSnapshot(referenceWeekStart, null, Array.Empty<RetainTemplatePlanningEntry>());
        }

        var futureWeekColumns = weekColumns
            .Where(column => column.WeekStartDate >= referenceWeekStart)
            .ToList();

        if (futureWeekColumns.Count == 0)
        {
            return new RetainTemplatePlanningSnapshot(referenceWeekStart, null, Array.Empty<RetainTemplatePlanningEntry>());
        }

        var entries = ExtractEntries(
            worksheet,
            headerRowIndex + 1,
            resourceIdColumnIndex,
            resourceNameColumnIndex,
            futureWeekColumns);

        var lastWeekStart = entries.Count == 0
            ? (DateTime?)null
            : entries.Max(entry => entry.WeekStartDate);

        return new RetainTemplatePlanningSnapshot(referenceWeekStart, lastWeekStart, entries);
    }

    private static WorksheetData LoadWorksheet(string filePath)
    {
        EnsureEncodingRegistered();

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);
        using var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            UseColumnDataType = true
        });

        WorksheetData? target = null;

        // First: Look for allocation worksheet by name
        foreach (DataTable? table in dataSet.Tables)
        {
            if (table is null || table.Rows.Count == 0 || table.Columns.Count == 0)
            {
                continue;
            }

            var name = table.TableName?.Trim() ?? string.Empty;
            if (IsAllocationWorksheetName(name))
            {
                target = WorksheetData.From(table);
                break;
            }
        }

        // Second: Look for worksheet with GPN header
        if (target is null)
        {
            foreach (DataTable? table in dataSet.Tables)
            {
                if (table is null || table.Rows.Count == 0 || table.Columns.Count == 0)
                {
                    continue;
                }

                var headerRow = table.Rows[0];
                // Use DataNormalizationService.GetString() instead of local GetString()
                if (headerRow.ItemArray.Any(value => string.Equals(GetString(value), "GPN", StringComparison.OrdinalIgnoreCase)))
                {
                    target = WorksheetData.From(table);
                    break;
                }
            }
        }

        if (target is null)
        {
            throw new InvalidDataException("Worksheet 'Alocações_Staff' is missing from the allocation planning workbook.");
        }

        return target;
    }

    private static int FindHeaderRowIndex(WorksheetData worksheet)
    {
        for (var rowIndex = 0; rowIndex < worksheet.RowCount; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
            {
                // Use DataNormalizationService.GetString()
                if (!string.IsNullOrWhiteSpace(GetString(worksheet.GetValue(rowIndex, columnIndex))))
                {
                    return rowIndex;
                }
            }
        }

        return -1;
    }

    private static int FindColumnIndex(WorksheetData worksheet, int headerRowIndex, IReadOnlyList<string> headerCandidates)
    {
        if (headerRowIndex < 0)
        {
            return -1;
        }

        var normalizedCandidates = headerCandidates
            .Select(NormalizeHeader) // Use DataNormalizationService.NormalizeHeader()
            .Where(candidate => !string.IsNullOrEmpty(candidate))
            .ToArray();

        if (normalizedCandidates.Length == 0)
        {
            return -1;
        }

        for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
        {
            // Use DataNormalizationService.NormalizeHeader() and GetString()
            var header = NormalizeHeader(GetString(worksheet.GetValue(headerRowIndex, columnIndex)));
            if (string.IsNullOrEmpty(header))
            {
                continue;
            }

            if (normalizedCandidates.Contains(header, StringComparer.OrdinalIgnoreCase))
            {
                return columnIndex;
            }
        }

        return -1;
    }

    private static List<WeekColumn> IdentifyWeekColumns(WorksheetData worksheet, int headerRowIndex)
    {
        var columns = new List<WeekColumn>();

        for (var columnIndex = 0; columnIndex < worksheet.ColumnCount; columnIndex++)
        {
            // Use DataNormalizationService.TryParseDate()
            var weekDate = TryParseDate(worksheet.GetValue(headerRowIndex, columnIndex));
            if (!weekDate.HasValue)
            {
                continue;
            }

            columns.Add(new WeekColumn(columnIndex, weekDate.Value));
        }

        return columns;
    }

    private static List<RetainTemplatePlanningEntry> ExtractEntries(
        WorksheetData worksheet,
        int firstDataRowIndex,
        int resourceIdColumnIndex,
        int resourceNameColumnIndex,
        IReadOnlyList<WeekColumn> weekColumns)
    {
        var entries = new List<RetainTemplatePlanningEntry>();
        var consecutiveBlankRows = 0;

        for (var rowIndex = firstDataRowIndex; rowIndex < worksheet.RowCount; rowIndex++)
        {
            // Use DataNormalizationService.GetString()
            var resourceId = resourceIdColumnIndex >= 0
                ? GetString(worksheet.GetValue(rowIndex, resourceIdColumnIndex))
                : string.Empty;

            var resourceName = resourceNameColumnIndex >= 0
                ? GetString(worksheet.GetValue(rowIndex, resourceNameColumnIndex))
                : string.Empty;

            var rowHasEntry = false;

            foreach (var week in weekColumns)
            {
                var cellValue = worksheet.GetValue(rowIndex, week.ColumnIndex);
                if (!TryExtractEngagement(cellValue, out var engagementName, out var engagementCode))
                {
                    continue;
                }

                var hours = ResolveHours(cellValue);
                if (hours <= 0)
                {
                    continue;
                }

                entries.Add(new RetainTemplatePlanningEntry(
                    resourceId,
                    resourceName,
                    engagementCode,
                    engagementName,
                    week.WeekStartDate,
                    Math.Round(hours, 2, MidpointRounding.AwayFromZero)));

                rowHasEntry = true;
            }

            if (rowHasEntry)
            {
                consecutiveBlankRows = 0;
                continue;
            }

            if (string.IsNullOrEmpty(resourceId) && string.IsNullOrEmpty(resourceName))
            {
                consecutiveBlankRows++;
                if (consecutiveBlankRows >= 5)
                {
                    break;
                }
            }
            else
            {
                consecutiveBlankRows = 0;
            }
        }

        return entries;
    }

    private static bool TryExtractEngagement(object? value, out string engagementName, out string engagementCode)
    {
        engagementName = string.Empty;
        engagementCode = string.Empty;

        // Use DataNormalizationService.GetString()
        var text = GetString(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Use DataNormalizationService.ExtractEngagementCode()
        engagementCode = ExtractEngagementCode(text);
        if (string.IsNullOrEmpty(engagementCode))
        {
            return false;
        }

        // Extract engagement name by removing code
        var engagementCodeRegex = new Regex(@"\bE-\d+\b", RegexOptions.IgnoreCase);
        var nameWithoutCode = engagementCodeRegex.Replace(text, string.Empty, 1);
        var builder = new StringBuilder(nameWithoutCode.Length);

        foreach (var ch in nameWithoutCode)
        {
            if (ch == '(' || ch == ')' || ch == '[' || ch == ']')
            {
                continue;
            }

            builder.Append(ch);
        }

        engagementName = builder
            .ToString()
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim(' ', '-', '–', '—', '/', '\\', ':');

        if (string.IsNullOrEmpty(engagementName))
        {
            engagementName = engagementCode;
        }

        return true;
    }

    private static decimal ResolveHours(object? value)
    {
        switch (value)
        {
            case null:
                return 0m;
            case decimal decimalValue:
                return decimalValue;
            case double doubleValue:
                return Convert.ToDecimal(doubleValue);
            case float floatValue:
                return Convert.ToDecimal(floatValue);
        }

        // Use DataNormalizationService.GetString()
        var text = GetString(value);
        if (string.IsNullOrEmpty(text))
        {
            return 0m;
        }

        // Remove engagement code pattern from text
        var engagementCodeRegex = new Regex(@"\bE-\d+\b", RegexOptions.IgnoreCase);
        var sanitized = engagementCodeRegex.Replace(text, string.Empty);
        sanitized = sanitized.Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Trim();

        // Use DataNormalizationService.TryParseDecimal()
        var parsed = TryParseDecimal(sanitized);
        if (parsed.HasValue)
        {
            return parsed.Value;
        }

        return DefaultWeeklyHours;
    }

    private static bool IsAllocationWorksheetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return string.Equals(name, "Alocações_Staff", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Alocacoes_Staff", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var monday = date.Date;
        var offset = (int)monday.DayOfWeek - (int)DayOfWeek.Monday;
        if (offset < 0)
        {
            offset += 7;
        }

        return monday.AddDays(-offset);
    }

    private static void EnsureEncodingRegistered()
    {
        if (_encodingRegistered)
        {
            return;
        }

        lock (EncodingLock)
        {
            if (_encodingRegistered)
            {
                return;
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            _encodingRegistered = true;
        }
    }

    private sealed record WeekColumn(int ColumnIndex, DateTime WeekStartDate);

    private sealed class WorksheetData
    {
        private readonly object?[][] _cells;

        private WorksheetData(object?[][] cells, int columnCount)
        {
            _cells = cells;
            ColumnCount = columnCount;
        }

        public int RowCount => _cells.Length;
        public int ColumnCount { get; }

        public object? GetValue(int rowIndex, int columnIndex)
        {
            if ((uint)rowIndex >= (uint)_cells.Length)
            {
                return null;
            }

            var row = _cells[rowIndex];
            if ((uint)columnIndex >= (uint)row.Length)
            {
                return null;
            }

            return row[columnIndex];
        }

        public static WorksheetData From(DataTable table)
        {
            var rowCount = table.Rows.Count;
            var columnCount = table.Columns.Count;
            var cells = new object?[rowCount][];

            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var row = new object?[columnCount];
                var dataRow = table.Rows[rowIndex];

                for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    row[columnIndex] = dataRow.IsNull(columnIndex) ? null : dataRow[columnIndex];
                }

                cells[rowIndex] = row;
            }

            return new WorksheetData(cells, columnCount);
        }
    }
}

internal sealed record RetainTemplatePlanningSnapshot(
    DateTime ReferenceWeekStart,
    DateTime? LastWeekStart,
    IReadOnlyList<RetainTemplatePlanningEntry> Entries)
{
    public IReadOnlyList<DateTime> BuildSaturdayHeaders(DateTime referenceDate)
    {
        var saturdays = new List<DateTime>();

        if (Entries.Count > 0)
        {
            var orderedWeekStarts = Entries
                .Select(entry => entry.WeekStartDate.Date)
                .Distinct()
                .OrderBy(date => date)
                .ToList();

            if (orderedWeekStarts.Count > 0)
            {
                var firstSaturday = PreviousOrSameSaturday(orderedWeekStarts[0]);
                var lastSaturday = PreviousOrSameSaturday(orderedWeekStarts[^1]);

                for (var current = firstSaturday; current <= lastSaturday; current = current.AddDays(7))
                {
                    saturdays.Add(current);
                }
            }
        }

        if (saturdays.Count == 0)
        {
            var baseline = ReferenceWeekStart != default
                ? ReferenceWeekStart
                : referenceDate;

            saturdays.Add(PreviousOrSameSaturday(baseline));
        }

        return saturdays;
    }

    private static DateTime PreviousOrSameSaturday(DateTime date)
    {
        var current = date.Date;
        var daysSinceSaturday = ((int)current.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        return current.AddDays(-daysSinceSaturday);
    }
}

internal sealed record RetainTemplatePlanningEntry(
    string ResourceId,
    string ResourceName,
    string EngagementCode,
    string EngagementName,
    DateTime WeekStartDate,
    decimal Hours);
