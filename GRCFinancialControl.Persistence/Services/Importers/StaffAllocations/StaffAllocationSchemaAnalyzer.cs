using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;

public sealed class StaffAllocationSchemaAnalyzer
{
    private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly IReadOnlyDictionary<string, StaffAllocationFixedColumn> FixedColumnLookup =
        new Dictionary<string, StaffAllocationFixedColumn>(StringComparer.OrdinalIgnoreCase)
        {
            ["GPN"] = StaffAllocationFixedColumn.Gpn,
            ["% UTILIZACAO FYTD"] = StaffAllocationFixedColumn.UtilizationFytd,
            ["% UTILIZACAO FY"] = StaffAllocationFixedColumn.UtilizationFytd,
            ["UTILIZACAO FYTD"] = StaffAllocationFixedColumn.UtilizationFytd,
            ["RANK"] = StaffAllocationFixedColumn.Rank,
            ["RECURSOS"] = StaffAllocationFixedColumn.ResourceName,
            ["RECURSO"] = StaffAllocationFixedColumn.ResourceName,
            ["ESCRITORIO"] = StaffAllocationFixedColumn.Office,
            ["OFFICE"] = StaffAllocationFixedColumn.Office,
            ["SUBDOMINIO"] = StaffAllocationFixedColumn.Subdomain,
            ["SUB DOMINIO"] = StaffAllocationFixedColumn.Subdomain
        };

    private static readonly IReadOnlyList<StaffAllocationFixedColumnMatcher> FixedColumnMatchers =
        new[]
        {
            new StaffAllocationFixedColumnMatcher(
                StaffAllocationFixedColumn.Gpn,
                normalized => ContainsToken(normalized, "GPN")),
            new StaffAllocationFixedColumnMatcher(
                StaffAllocationFixedColumn.UtilizationFytd,
                normalized => ContainsToken(normalized, "UTILIZACAO") &&
                              (ContainsToken(normalized, "FYTD") || ContainsToken(normalized, "FY"))),
            new StaffAllocationFixedColumnMatcher(
                StaffAllocationFixedColumn.Rank,
                normalized => ContainsToken(normalized, "RANK") || ContainsToken(normalized, "SENIORIDADE")),
            new StaffAllocationFixedColumnMatcher(
                StaffAllocationFixedColumn.ResourceName,
                normalized => ContainsToken(normalized, "RECURSO") ||
                              ContainsToken(normalized, "RECURSOS")),
            new StaffAllocationFixedColumnMatcher(
                StaffAllocationFixedColumn.Office,
                normalized => ContainsToken(normalized, "ESCRITORIO") || ContainsToken(normalized, "OFFICE")),
            new StaffAllocationFixedColumnMatcher(
                StaffAllocationFixedColumn.Subdomain,
                normalized => ContainsToken(normalized, "SUBDOMINIO") ||
                              ContainsToken(normalized, "SUBDOM") ||
                              ContainsToken(normalized, "SUBD") ||
                              ContainsToken(normalized, "SUB AREA"))
        };

    private static readonly HashSet<string> IgnoredHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "OBSERVACAO",
        "OBSERVACOES",
        "OBS",
        "STATUS",
        "JUSTIFICATIVA",
        "PENDENCIA",
        "PENDENCIAS",
        "RESPONSAVEL"
    };

    private const int RequiredFixedColumnsCount = 6;

    public StaffAllocationSchemaAnalysis Analyze(DataTable worksheet)
    {
        if (worksheet == null)
        {
            throw new ArgumentNullException(nameof(worksheet));
        }

        if (worksheet.Rows.Count == 0)
        {
            throw new InvalidDataException("The staff allocation worksheet does not contain any rows.");
        }

        for (var rowIndex = 0; rowIndex < worksheet.Rows.Count; rowIndex++)
        {
            var row = worksheet.Rows[rowIndex];
            var analysis = TryAnalyzeHeaderRow(row, rowIndex);
            if (analysis != null)
            {
                return analysis;
            }
        }

        throw new InvalidDataException("Unable to locate the header row in the staff allocation worksheet.");
    }

    private StaffAllocationSchemaAnalysis? TryAnalyzeHeaderRow(DataRow row, int rowIndex)
    {
        var fixedColumns = new Dictionary<StaffAllocationFixedColumn, StaffAllocationColumnDefinition>();
        var weekColumns = new List<StaffAllocationWeekColumn>();
        var invalidWeekHeaders = new List<string>();
        var hasAnyValue = false;

        for (var columnIndex = 0; columnIndex < row.Table.Columns.Count; columnIndex++)
        {
            var cellValue = row[columnIndex];
            if (StaffAllocationCellHelper.IsBlank(cellValue))
            {
                continue;
            }

            hasAnyValue = true;

            if (TryGetWeekDate(cellValue, out var weekDate, out var weekDisplay))
            {
                if (weekDate.DayOfWeek != DayOfWeek.Monday)
                {
                    invalidWeekHeaders.Add(weekDisplay);
                    continue;
                }

                weekColumns.Add(new StaffAllocationWeekColumn(columnIndex, weekDate, weekDisplay));
                continue;
            }

            var normalizedHeader = NormalizeHeader(cellValue);
            if (string.IsNullOrEmpty(normalizedHeader))
            {
                continue;
            }

            if (IgnoredHeaders.Contains(normalizedHeader))
            {
                continue;
            }

            if (!TryResolveFixedColumn(normalizedHeader, out var fixedColumn))
            {
                continue;
            }

            if (!fixedColumns.ContainsKey(fixedColumn))
            {
                fixedColumns[fixedColumn] = new StaffAllocationColumnDefinition(
                    columnIndex,
                    StaffAllocationCellHelper.GetDisplayText(cellValue));
            }
        }

        if (!hasAnyValue)
        {
            return null;
        }

        if (fixedColumns.Count != RequiredFixedColumnsCount)
        {
            return null;
        }

        if (invalidWeekHeaders.Count > 0)
        {
            throw new InvalidDataException(
                $"The staff allocation worksheet has week columns that are not Mondays: {string.Join(", ", invalidWeekHeaders)}.");
        }

        if (weekColumns.Count == 0)
        {
            return null;
        }

        weekColumns.Sort((left, right) => left.ColumnIndex.CompareTo(right.ColumnIndex));

        return new StaffAllocationSchemaAnalysis(
            rowIndex,
            new ReadOnlyDictionary<StaffAllocationFixedColumn, StaffAllocationColumnDefinition>(fixedColumns),
            new ReadOnlyCollection<StaffAllocationWeekColumn>(weekColumns));
    }

    private static bool TryGetWeekDate(object value, out DateTime weekDate, out string displayText)
    {
        switch (value)
        {
            case DateTime dateTime:
                weekDate = dateTime.Date;
                displayText = dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;
            case double oaDate:
                {
                    try
                    {
                        weekDate = DateTime.FromOADate(oaDate).Date;
                        displayText = weekDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch (ArgumentException)
                    {
                        break;
                    }
                }
            case float oaFloat:
                {
                    try
                    {
                        weekDate = DateTime.FromOADate(oaFloat).Date;
                        displayText = weekDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch (ArgumentException)
                    {
                        break;
                    }
                }
            case string text:
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        break;
                    }

                    var trimmed = text.Trim();

                    if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var oaTextDate))
                    {
                        try
                        {
                            weekDate = DateTime.FromOADate(oaTextDate).Date;
                            displayText = weekDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            return true;
                        }
                        catch (ArgumentException)
                        {
                            // ignored
                        }
                    }

                    if (!ContainsDateTextHint(trimmed))
                    {
                        break;
                    }

                    if (TryParseDateString(trimmed, out weekDate))
                    {
                        displayText = weekDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        return true;
                    }

                    break;
                }
            default:
                {
                    if (value is IFormattable formattable)
                    {
                        var formatted = formattable.ToString(null, CultureInfo.InvariantCulture);
                        if (string.IsNullOrWhiteSpace(formatted))
                        {
                            break;
                        }

                        var trimmedFormatted = formatted.Trim();

                        if (double.TryParse(trimmedFormatted, NumberStyles.Float, CultureInfo.InvariantCulture, out var oaFormatted))
                        {
                            try
                            {
                                weekDate = DateTime.FromOADate(oaFormatted).Date;
                                displayText = weekDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                                return true;
                            }
                            catch (ArgumentException)
                            {
                                // ignored
                            }
                        }

                        if (!ContainsDateTextHint(trimmedFormatted))
                        {
                            break;
                        }

                        if (TryParseDateString(trimmedFormatted, out weekDate))
                        {
                            displayText = weekDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            return true;
                        }
                    }

                    break;
                }
        }

        weekDate = default;
        displayText = StaffAllocationCellHelper.GetDisplayText(value);
        return false;
    }

    private static bool TryParseDateString(string? text, out DateTime weekDate)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            weekDate = default;
            return false;
        }

        text = text.Trim();

        if (DateTime.TryParse(text, PtBrCulture, DateTimeStyles.None, out var parsed))
        {
            weekDate = parsed.Date;
            return true;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            weekDate = parsed.Date;
            return true;
        }

        weekDate = default;
        return false;
    }

    private static string NormalizeHeader(object value)
    {
        var text = StaffAllocationCellHelper.GetDisplayText(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = RemoveDiacritics(text).ToUpperInvariant();

        var builder = new StringBuilder(text.Length);
        var previousWasSpace = false;
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool TryResolveFixedColumn(string normalizedHeader, out StaffAllocationFixedColumn column)
    {
        if (FixedColumnLookup.TryGetValue(normalizedHeader, out column))
        {
            return true;
        }

        foreach (var matcher in FixedColumnMatchers)
        {
            if (matcher.IsMatch(normalizedHeader))
            {
                column = matcher.Column;
                return true;
            }
        }

        column = default;
        return false;
    }

    private static bool ContainsToken(string normalizedHeader, string token)
    {
        if (string.IsNullOrEmpty(normalizedHeader) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        if (normalizedHeader.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var compactHeader = normalizedHeader.Replace(" ", string.Empty, StringComparison.Ordinal);
        var compactToken = token.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (compactHeader.Contains(compactToken, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedHeader
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(part => string.Equals(part, token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsDateTextHint(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (!char.IsDigit(ch))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record StaffAllocationFixedColumnMatcher(
        StaffAllocationFixedColumn Column,
        Func<string, bool> Predicate)
    {
        public bool IsMatch(string normalizedHeader)
        {
            return Predicate(normalizedHeader);
        }
    }
}
