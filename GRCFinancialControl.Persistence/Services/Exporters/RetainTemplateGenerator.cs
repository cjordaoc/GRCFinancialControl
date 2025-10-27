using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Exporters;

public sealed class RetainTemplateGenerator : IRetainTemplateGenerator
{
    private const string TemplatesDirectoryName = "Templates";
    private const string TemplateFileName = "RetainTemplate.xlsx.b64";

    private readonly ILogger<RetainTemplateGenerator> _logger;

    public RetainTemplateGenerator(ILogger<RetainTemplateGenerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string> GenerateRetainTemplateAsync(string allocationFilePath, string destinationFilePath)
    {
        if (string.IsNullOrWhiteSpace(allocationFilePath))
        {
            throw new ArgumentException("The allocation planning file path must be provided.", nameof(allocationFilePath));
        }

        if (!File.Exists(allocationFilePath))
        {
            throw new FileNotFoundException("Allocation planning workbook could not be found.", allocationFilePath);
        }

        if (string.IsNullOrWhiteSpace(destinationFilePath))
        {
            throw new ArgumentException("The output file path must be provided.", nameof(destinationFilePath));
        }

        var templateBytes = LoadTemplateBytes();

        var referenceDate = DateTime.Today;
        var planningSnapshot = RetainTemplatePlanningWorkbook.Load(allocationFilePath, referenceDate);
        var saturdayHeaders = planningSnapshot.BuildSaturdayHeaders(referenceDate);

        if (planningSnapshot.LastWeekStart is null)
        {
            _logger.LogWarning(
                "No allocation records from week starting {ReferenceWeekStart:yyyy-MM-dd} onward were found in {AllocationFilePath}.",
                planningSnapshot.ReferenceWeekStart,
                allocationFilePath);
        }
        else
        {
            _logger.LogInformation(
                "Parsed {EntryCount} allocation entries covering weeks {StartWeek:yyyy-MM-dd} through {EndWeek:yyyy-MM-dd}.",
                planningSnapshot.Entries.Count,
                planningSnapshot.ReferenceWeekStart,
                planningSnapshot.LastWeekStart.Value);
        }

        if (saturdayHeaders.Count > 0)
        {
            _logger.LogInformation(
                "Prepared {SaturdayCount} Saturday headers ranging {FirstSaturday:yyyy-MM-dd} to {LastSaturday:yyyy-MM-dd}.",
                saturdayHeaders.Count,
                saturdayHeaders[0],
                saturdayHeaders[^1]);
        }

        var outputFilePath = PrepareTemplateCopy(destinationFilePath, templateBytes);
        PopulateTemplate(outputFilePath, planningSnapshot, saturdayHeaders);

        _logger.LogInformation("Retain template generated at {OutputFilePath}", outputFilePath);

        return Task.FromResult(outputFilePath);
    }

    private static byte[] LoadTemplateBytes()
    {
        var baseDirectory = AppContext.BaseDirectory ?? string.Empty;
        var templatePath = Path.Combine(baseDirectory, TemplatesDirectoryName, TemplateFileName);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Retain template asset could not be found.", templatePath);
        }

        var base64Content = File.ReadAllText(templatePath);

        try
        {
            return Convert.FromBase64String(base64Content);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Retain template asset is not a valid Base64 string.", exception);
        }
    }

    private static string PrepareTemplateCopy(string destinationFilePath, byte[] templateBytes)
    {
        var outputDirectory = Path.GetDirectoryName(destinationFilePath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException("A valid directory must be provided for the generated template.");
        }

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllBytes(destinationFilePath, templateBytes);
        return destinationFilePath;
    }

    private static void PopulateTemplate(
        string outputFilePath,
        RetainTemplatePlanningSnapshot planningSnapshot,
        IReadOnlyList<DateTime> saturdayHeaders)
    {
        using var workbook = new XLWorkbook(outputFilePath);
        var worksheet = workbook.Worksheet("Data Entry")
                         ?? throw new InvalidDataException("Sheet 'Data Entry' was not found in the Retain template.");

        const int saturdayHeaderRowIndex = 1;
        const int firstDataRowIndex = 4;
        const int firstWeekColumnIndex = 5;

        var lastColumnToClear = Math.Max(
            worksheet.LastColumnUsed()?.ColumnNumber() ?? firstWeekColumnIndex - 1,
            firstWeekColumnIndex + Math.Max(saturdayHeaders.Count, 1) - 1);

        var saturdayHeaderCell = worksheet.Cell(saturdayHeaderRowIndex, firstWeekColumnIndex);
        if (saturdayHeaders.Count > 0)
        {
            saturdayHeaderCell.Value = saturdayHeaders[0];
            saturdayHeaderCell.Style.DateFormat.Format = saturdayHeaderCell.Style.DateFormat.Format switch
            {
                null or "" => "yyyy-MM-dd",
                var existing => existing
            };
        }
        else
        {
            saturdayHeaderCell.Value = string.Empty;
        }

        var lastRowUsed = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRowUsed >= firstDataRowIndex)
        {
            worksheet.Range(firstDataRowIndex, 1, lastRowUsed, lastColumnToClear)
                .Clear(XLClearOptions.Contents);
        }

        var rows = BuildTemplateRows(planningSnapshot);
        if (rows.Count == 0)
        {
            workbook.Save();
            return;
        }

        var rowNumber = firstDataRowIndex;
        var sequentialNumber = 1;

        foreach (var row in rows)
        {
            worksheet.Cell(rowNumber, 1).Value = sequentialNumber;
            worksheet.Cell(rowNumber, 2).Value = row.JobName;
            worksheet.Cell(rowNumber, 3).Value = row.ResourceId ?? string.Empty;
            worksheet.Cell(rowNumber, 4).Value = row.ResourceName ?? string.Empty;

            for (var headerIndex = 0; headerIndex < saturdayHeaders.Count; headerIndex++)
            {
                var columnIndex = firstWeekColumnIndex + headerIndex;
                var saturday = saturdayHeaders[headerIndex];
                var monday = saturday.AddDays(2).Date;

                if (!row.HoursByWeek.TryGetValue(monday, out var hours))
                {
                    continue;
                }

                var rounded = Math.Round(hours, 2, MidpointRounding.AwayFromZero);
                if (rounded == 0m)
                {
                    continue;
                }

                worksheet.Cell(rowNumber, columnIndex).Value = rounded;
            }

            rowNumber++;
            sequentialNumber++;
        }

        workbook.Save();
    }

    private static List<RetainTemplateRow> BuildTemplateRows(RetainTemplatePlanningSnapshot snapshot)
    {
        var rows = snapshot.Entries
            .GroupBy(entry => new RetainTemplateRowKey(
                entry.ResourceId,
                entry.ResourceName,
                entry.EngagementCode,
                entry.EngagementName),
                RetainTemplateRowKeyComparer.Instance)
            .OrderBy(group => group.Key, RetainTemplateRowKeyComparer.Instance)
            .Select(group =>
            {
                var hoursByWeek = group
                    .GroupBy(entry => entry.WeekStartDate.Date)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(entry => entry.Hours));

                return new RetainTemplateRow(
                    ComposeJobName(group.Key.EngagementName, group.Key.EngagementCode),
                    group.Key.ResourceName,
                    group.Key.ResourceId,
                    hoursByWeek);
            })
            .ToList();

        return rows;
    }

    private static string ComposeJobName(string engagementName, string engagementCode)
    {
        var code = engagementCode?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(code))
        {
            return code;
        }

        return engagementName?.Trim() ?? string.Empty;
    }

    private sealed record RetainTemplateRow(
        string JobName,
        string ResourceName,
        string ResourceId,
        IReadOnlyDictionary<DateTime, decimal> HoursByWeek);

    private sealed record RetainTemplateRowKey(
        string ResourceId,
        string ResourceName,
        string EngagementCode,
        string EngagementName);

    private sealed class RetainTemplateRowKeyComparer : IEqualityComparer<RetainTemplateRowKey>, IComparer<RetainTemplateRowKey>
    {
        public static RetainTemplateRowKeyComparer Instance { get; } = new();

        public bool Equals(RetainTemplateRowKey? x, RetainTemplateRowKey? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.ResourceId, y.ResourceId, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.ResourceName, y.ResourceName, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.EngagementCode, y.EngagementCode, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.EngagementName, y.EngagementName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(RetainTemplateRowKey obj)
        {
            return HashCode.Combine(
                Normalize(obj.ResourceId),
                Normalize(obj.ResourceName),
                Normalize(obj.EngagementCode),
                Normalize(obj.EngagementName));
        }

        public int Compare(RetainTemplateRowKey? x, RetainTemplateRowKey? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var engagementComparison = string.Compare(x.EngagementName, y.EngagementName, StringComparison.OrdinalIgnoreCase);
            if (engagementComparison != 0)
            {
                return engagementComparison;
            }

            var engagementCodeComparison = string.Compare(x.EngagementCode, y.EngagementCode, StringComparison.OrdinalIgnoreCase);
            if (engagementCodeComparison != 0)
            {
                return engagementCodeComparison;
            }

            var resourceComparison = string.Compare(x.ResourceName, y.ResourceName, StringComparison.OrdinalIgnoreCase);
            if (resourceComparison != 0)
            {
                return resourceComparison;
            }

            return string.Compare(x.ResourceId, y.ResourceId, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string? value)
        {
            return value?.Trim().ToUpperInvariant() ?? string.Empty;
        }
    }
}
