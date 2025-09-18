using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;


namespace GRCFinancialControl.Services
{
    public class ExcelParseResult<TRow>

    {
        private readonly List<TRow> _rows = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        public ExcelParseResult(string operationName)
        {
            OperationName = operationName;
        }

        public string OperationName { get; }
        public IReadOnlyList<TRow> Rows => _rows;
        public IReadOnlyList<string> Errors => _errors;
        public IReadOnlyList<string> Warnings => _warnings;
        public int Skipped { get; private set; }

        public void AddRow(TRow row) => _rows.Add(row);

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _errors.Add(message);
            }
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _warnings.Add(message);
            }
        }

        public void IncrementSkipped(string message)
        {
            Skipped++;
            AddWarning(message);
        }

        public string BuildSummary()
        {
            return $"{OperationName}: {Rows.Count} rows parsed, {Skipped} skipped, {Warnings.Count} warnings, {Errors.Count} errors.";
        }
    }

    public sealed class EtcParseResult : ExcelParseResult<EtcRow>
    {
        public EtcParseResult() : base("ETC Upload")
        {
        }
    }

    public sealed class PlanRow
    {
        public string RawLevel { get; set; } = string.Empty;
        public decimal PlannedHours { get; set; }
        public decimal? PlannedRate { get; set; }
    }

    public sealed class EtcRow
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string RawLevel { get; set; } = string.Empty;
        public decimal HoursIncurred { get; set; }
        public decimal EtcRemaining { get; set; }
    }

    public sealed class MarginDataRow
    {
        public int ExcelRowNumber { get; set; }
        public string EngagementId { get; set; } = string.Empty;
        public string? EngagementTitle { get; set; }
        public decimal? OpeningMargin { get; set; }
        public decimal? CurrentMargin { get; set; }
        public decimal? MarginValue { get; set; }
    }

    public sealed class MarginDataParseResult : ExcelParseResult<MarginDataRow>
    {
        public MarginDataParseResult() : base("Margin Data")
        {
        }
    }

    public sealed class WeeklyDeclarationRow
    {
        public DateOnly WeekStart { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal DeclaredHours { get; set; }
    }

    public sealed class ChargeRow
    {
        public DateOnly ChargeDate { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal Hours { get; set; }
        public decimal? CostAmount { get; set; }
    }

    internal static class ExcelParsingUtilities
    {
        private static readonly CultureInfo PtBr = new("pt-BR");

        public static string NormalizeHeader(string header)
        {
            var normalized = StringNormalizer.NormalizeName(header ?? string.Empty);
            var cleaned = new string(normalized.Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').ToArray());
            return cleaned;
        }

        public static bool TryGetDecimal(IXLCell cell, out decimal value)
        {
            if (cell == null)
            {
                value = 0m;
                return false;
            }

            if (cell.DataType == XLDataType.Number || cell.DataType == XLDataType.DateTime)
            {
                value = Convert.ToDecimal(cell.GetDouble());
                return true;
            }

            var raw = cell.GetValue<string>()?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                value = 0m;
                return false;
            }

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (decimal.TryParse(raw, NumberStyles.Any, PtBr, out value))
            {
                return true;
            }

            value = 0m;
            return false;
        }

        public static bool TryGetNullableDecimal(IXLCell cell, out decimal? value)
        {
            if (cell == null)
            {
                value = null;
                return false;
            }

            if (cell.IsEmpty())
            {
                value = null;
                return true;
            }

            if (TryGetDecimal(cell, out var parsed))
            {
                value = parsed;
                return true;
            }

            value = null;
            return false;
        }

        public static bool TryGetDate(IXLCell cell, out DateOnly date)
        {
            if (cell == null)
            {
                date = default;
                return false;
            }

            if (cell.DataType == XLDataType.DateTime)
            {
                var dt = cell.GetDateTime();
                date = DateOnly.FromDateTime(dt);
                return true;
            }

            if (cell.DataType == XLDataType.Number)
            {
                var dt = DateTime.FromOADate(cell.GetDouble());
                date = DateOnly.FromDateTime(dt);
                return true;
            }

            var raw = cell.GetValue<string>()?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                date = default;
                return false;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                date = DateOnly.FromDateTime(parsed);
                return true;
            }

            if (DateTime.TryParse(raw, PtBr, DateTimeStyles.AssumeLocal, out parsed))
            {
                date = DateOnly.FromDateTime(parsed);
                return true;
            }

            date = default;
            return false;
        }

        public static string GetCellString(IXLCell cell)
        {
            if (cell == null)
            {
                return string.Empty;
            }

            return cell.GetValue<string>()?.Trim() ?? string.Empty;
        }

        public static bool IsRowEmpty(IXLRow row)
        {
            return !row.CellsUsed().Any(c => !string.IsNullOrWhiteSpace(c.GetValue<string>()));
        }
    }

    public sealed class PlanExcelParser
    {
        private static readonly Dictionary<string, string[]> HeaderMap = new()
        {
            ["LEVEL"] = new[] { "LEVEL", "RANK", "GRADE", "FUNCAO", "CARGO" },
            ["HOURS"] = new[] { "PLANNED HOURS", "HOURS", "TOTAL HOURS", "BUDGET HOURS" },
            ["RATE"] = new[] { "PLANNED RATE", "RATE", "RATE HOUR", "BILL RATE" }
        };

        public ExcelParseResult<PlanRow> Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Excel file not found.", filePath);
            }

            var result = new ExcelParseResult<PlanRow>("Initial Plan");

            using var workbook = new XLWorkbook(filePath);
            var worksheet = ExcelWorksheetHelper.FirstVisible(workbook.Worksheets);

            var (headers, headerRow) = ExcelHeaderDetector.DetectHeaders(worksheet, HeaderMap, new[] { "LEVEL", "HOURS" });

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (ExcelParsingUtilities.IsRowEmpty(row))
                {
                    continue;
                }

                var level = ExcelParsingUtilities.GetCellString(row.Cell(headers["LEVEL"]));
                if (string.IsNullOrWhiteSpace(level))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Missing level.");
                    continue;
                }

                if (!ExcelParsingUtilities.TryGetDecimal(row.Cell(headers["HOURS"]), out var hours))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid planned hours.");
                    continue;
                }

                decimal? plannedRate = null;
                if (headers.TryGetValue("RATE", out var rateColumn))
                {
                    if (ExcelParsingUtilities.TryGetNullableDecimal(row.Cell(rateColumn), out var rateCandidate))
                    {
                        plannedRate = rateCandidate;
                    }
                    else if (!row.Cell(rateColumn).IsEmpty())
                    {
                        result.AddWarning($"Row {row.RowNumber()}: Unable to parse rate '{row.Cell(rateColumn).GetValue<string>()}'.");
                    }
                }

                result.AddRow(new PlanRow
                {
                    RawLevel = level,
                    PlannedHours = hours,
                    PlannedRate = plannedRate
                });
            }

            return result;
        }
    }

    public sealed class EtcExcelParser
    {
        private static readonly Dictionary<string, string[]> HeaderMap = new()
        {
            ["EMPLOYEE"] = new[] { "EMPLOYEE", "EMPLOYEE NAME", "NAME", "RESOURCE", "PROFISSIONAL" },
            ["LEVEL"] = new[] { "LEVEL", "RANK", "GRADE", "FUNCAO", "CARGO" },
            ["HOURS_INCURRED"] = new[] { "HOURS INCURRED", "HOURS IN CURRED", "HOURS ACTUAL", "ACTUAL HOURS", "HORAS INCORRIDAS" },
            ["ETC"] = new[] { "ETC REMAINING", "ETC", "REMAINING", "HORAS ETC" }
        };

        public EtcParseResult Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Excel file not found.", filePath);
            }

            var result = new EtcParseResult();

            using var workbook = new XLWorkbook(filePath);
            var worksheet = ExcelWorksheetHelper.FirstVisible(workbook.Worksheets);
            var (headers, headerRow) = ExcelHeaderDetector.DetectHeaders(worksheet, HeaderMap, new[] { "EMPLOYEE", "HOURS_INCURRED", "ETC" });

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (ExcelParsingUtilities.IsRowEmpty(row))
                {
                    continue;
                }

                var employeeName = ExcelParsingUtilities.GetCellString(row.Cell(headers["EMPLOYEE"]));
                if (string.IsNullOrWhiteSpace(employeeName))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Missing employee name.");
                    continue;
                }


                var rawLevel = string.Empty;
                if (headers.TryGetValue("LEVEL", out var levelColumn))
                {
                    rawLevel = ExcelParsingUtilities.GetCellString(row.Cell(levelColumn));
                }

                if (!ExcelParsingUtilities.TryGetDecimal(row.Cell(headers["HOURS_INCURRED"]), out var hoursIncurred))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid hours incurred.");
                    continue;
                }

                if (!ExcelParsingUtilities.TryGetDecimal(row.Cell(headers["ETC"]), out var etcRemaining))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid ETC remaining.");
                    continue;
                }

                result.AddRow(new EtcRow
                {
                    EmployeeName = employeeName,
                    RawLevel = rawLevel,
                    HoursIncurred = hoursIncurred,
                    EtcRemaining = etcRemaining
                });
            }

            return result;
        }
    }

    public sealed class MarginDataExcelParser
    {
        private static readonly CultureInfo[] SupportedCultures =
        {
            CultureInfo.InvariantCulture,
            new CultureInfo("pt-BR")
        };

        public MarginDataParseResult Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Excel file not found.", filePath);
            }

            var result = new MarginDataParseResult();

            using var workbook = new XLWorkbook(filePath);
            var worksheet = ExcelWorksheetHelper.FirstVisible(workbook.Worksheets);

            foreach (var row in worksheet.RowsUsed())
            {
                if (ExcelParsingUtilities.IsRowEmpty(row))
                {
                    continue;
                }

                var rowNumber = row.RowNumber();
                var descriptor = ExcelParsingUtilities.GetCellString(row.Cell(1));
                if (string.IsNullOrWhiteSpace(descriptor))
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing engagement descriptor in column A.");
                    continue;
                }

                if (!TryExtractEngagement(descriptor, out var engagementId, out var engagementTitle))
                {
                    result.IncrementSkipped($"Row {rowNumber}: Unable to extract engagement ID from '{descriptor}'.");
                    continue;
                }

                var marginRow = new MarginDataRow
                {
                    ExcelRowNumber = rowNumber,
                    EngagementId = engagementId,
                    EngagementTitle = engagementTitle
                };

                if (!TryReadMarginCell(row.Cell(4), out var openingMargin))
                {
                    var openingText = ExcelParsingUtilities.GetCellString(row.Cell(4));
                    if (!string.IsNullOrEmpty(openingText))
                    {
                        result.AddWarning($"Row {rowNumber}: Invalid opening margin '{openingText}'.");
                    }
                }
                else
                {
                    marginRow.OpeningMargin = openingMargin;
                }

                if (!TryReadMarginCell(row.Cell(5), out var currentMargin))
                {
                    var currentText = ExcelParsingUtilities.GetCellString(row.Cell(5));
                    if (!string.IsNullOrEmpty(currentText))
                    {
                        result.AddWarning($"Row {rowNumber}: Invalid current margin '{currentText}'.");
                    }
                }
                else
                {
                    marginRow.CurrentMargin = currentMargin;
                }

                if (!TryReadMarginCell(row.Cell(15), out var marginValue))
                {
                    var marginText = ExcelParsingUtilities.GetCellString(row.Cell(15));
                    if (!string.IsNullOrEmpty(marginText))
                    {
                        result.AddWarning($"Row {rowNumber}: Invalid margin value '{marginText}'.");
                    }
                }
                else
                {
                    marginRow.MarginValue = marginValue;
                }

                result.AddRow(marginRow);
            }

            return result;
        }

        private static bool TryExtractEngagement(string descriptor, out string engagementId, out string? engagementTitle)
        {
            engagementId = string.Empty;
            engagementTitle = null;

            var trimmed = descriptor.Trim();
            var openIndex = trimmed.LastIndexOf('(');
            var closeIndex = trimmed.LastIndexOf(')');

            if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex + 1)
            {
                return false;
            }

            var candidate = trimmed[(openIndex + 1)..closeIndex].Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            engagementId = candidate;

            var titleCandidate = trimmed[..openIndex].Trim();
            engagementTitle = StringNormalizer.TrimToNull(titleCandidate);
            return true;
        }

        private static bool TryReadMarginCell(IXLCell cell, out decimal? normalized)
        {
            normalized = null;
            if (cell == null)
            {
                return true;
            }

            if (ExcelParsingUtilities.TryGetNullableDecimal(cell, out var parsed))
            {
                if (parsed.HasValue)
                {
                    normalized = NormalizeMargin(parsed.Value);
                }

                return true;
            }

            var rawText = ExcelParsingUtilities.GetCellString(cell);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return true;
            }

            var sanitized = rawText.Replace("%", string.Empty, StringComparison.Ordinal).Trim();
            foreach (var culture in SupportedCultures)
            {
                if (decimal.TryParse(sanitized, NumberStyles.Any, culture, out var parsedValue))
                {
                    normalized = NormalizeMargin(parsedValue);
                    return true;
                }
            }

            return false;
        }

        private static decimal NormalizeMargin(decimal value)
        {
            if (value > 1m)
            {
                value /= 100m;
            }

            return Math.Round(value, 6, MidpointRounding.AwayFromZero);
        }
    }

    public sealed class WeeklyDeclarationExcelParser
    {
        private static readonly Dictionary<string, string[]> HeaderMap = new()
        {
            ["EMPLOYEE"] = new[] { "EMPLOYEE", "EMPLOYEE NAME", "RESOURCE", "NOME" },
            ["WEEK"] = new[] { "WEEK START", "WEEK OF", "WEEK", "DATA INICIO", "INICIO" },
            ["HOURS"] = new[] { "DECLARED HOURS", "HOURS", "ALLOCATED HOURS", "HORAS" }
        };

        public ExcelParseResult<WeeklyDeclarationRow> Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Excel file not found.", filePath);
            }

            var result = new ExcelParseResult<WeeklyDeclarationRow>("Weekly Declaration");
            using var workbook = new XLWorkbook(filePath);
            var worksheet = ExcelWorksheetHelper.FirstVisible(workbook.Worksheets);

            var (headers, headerRow) = ExcelHeaderDetector.DetectHeaders(worksheet, HeaderMap, new[] { "EMPLOYEE", "WEEK", "HOURS" });

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (ExcelParsingUtilities.IsRowEmpty(row))
                {
                    continue;
                }

                var employeeName = ExcelParsingUtilities.GetCellString(row.Cell(headers["EMPLOYEE"]));
                if (string.IsNullOrWhiteSpace(employeeName))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Missing employee name.");
                    continue;
                }

                if (!ExcelParsingUtilities.TryGetDate(row.Cell(headers["WEEK"]), out var weekStart))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid week start date.");
                    continue;
                }

                if (!ExcelParsingUtilities.TryGetDecimal(row.Cell(headers["HOURS"]), out var hours))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid declared hours.");
                    continue;
                }

                var normalizedWeek = WeekHelper.ToWeekStart(weekStart);

                result.AddRow(new WeeklyDeclarationRow
                {
                    EmployeeName = employeeName,
                    WeekStart = normalizedWeek,
                    DeclaredHours = hours
                });
            }

            return result;
        }
    }

    public sealed class ChargesExcelParser
    {
        private static readonly Dictionary<string, string[]> HeaderMap = new()
        {
            ["EMPLOYEE"] = new[] { "EMPLOYEE", "EMPLOYEE NAME", "RESOURCE", "NOME" },
            ["DATE"] = new[] { "DATE", "CHARGE DATE", "DATA", "DAY" },
            ["HOURS"] = new[] { "HOURS", "HOURS CHARGED", "HORAS", "QTD HORAS" },
            ["COST"] = new[] { "COST", "AMOUNT", "COST AMOUNT", "VALOR" }
        };

        public ExcelParseResult<ChargeRow> Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Excel file not found.", filePath);
            }

            var result = new ExcelParseResult<ChargeRow>("Charges");


            using var workbook = new XLWorkbook(filePath);
            var worksheet = ExcelWorksheetHelper.FirstVisible(workbook.Worksheets);
            var (headers, headerRow) = ExcelHeaderDetector.DetectHeaders(worksheet, HeaderMap, new[] { "EMPLOYEE", "DATE", "HOURS" });

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (ExcelParsingUtilities.IsRowEmpty(row))
                {
                    continue;
                }

                var employeeName = ExcelParsingUtilities.GetCellString(row.Cell(headers["EMPLOYEE"]));
                if (string.IsNullOrWhiteSpace(employeeName))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Missing employee name.");
                    continue;
                }

                if (!ExcelParsingUtilities.TryGetDate(row.Cell(headers["DATE"]), out var chargeDate))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid charge date.");
                    continue;
                }

                if (!ExcelParsingUtilities.TryGetDecimal(row.Cell(headers["HOURS"]), out var hours))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid hours value.");
                    continue;
                }

                decimal? costAmount = null;
                if (headers.TryGetValue("COST", out var costColumn))
                {
                    if (ExcelParsingUtilities.TryGetNullableDecimal(row.Cell(costColumn), out var costCandidate))
                    {
                        costAmount = costCandidate;
                    }
                    else if (!row.Cell(costColumn).IsEmpty())
                    {
                        result.AddWarning($"Row {row.RowNumber()}: Unable to parse cost '{row.Cell(costColumn).GetValue<string>()}'.");
                    }
                }

                result.AddRow(new ChargeRow
                {
                    EmployeeName = employeeName,
                    ChargeDate = chargeDate,
                    Hours = hours,
                    CostAmount = costAmount
                });
            }

            return result;
        }
    }


    internal static class ExcelWorksheetHelper
    {
        public static IXLWorksheet FirstVisible(IXLWorksheets worksheets)
        {
            ArgumentNullException.ThrowIfNull(worksheets);

            foreach (var worksheet in worksheets)
            {
                if (worksheet.Visibility == XLWorksheetVisibility.Visible)
                {
                    return worksheet;
                }
            }

            throw new InvalidDataException("No visible worksheets were found in the workbook.");
        }
    }

    internal static class ExcelHeaderDetector
    {
        public static (Dictionary<string, int> Headers, int HeaderRow) DetectHeaders(IXLWorksheet worksheet, IReadOnlyDictionary<string, string[]> headerSynonyms, IEnumerable<string> requiredKeys)
        {
            if (worksheet == null)
            {
                throw new ArgumentNullException(nameof(worksheet));
            }

            var normalizedSynonyms = headerSynonyms.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(ExcelParsingUtilities.NormalizeHeader).ToHashSet());

            foreach (var row in worksheet.RowsUsed())
            {
                var cells = row.CellsUsed().Select(c => new
                {
                    Column = c.Address.ColumnNumber,
                    Value = ExcelParsingUtilities.NormalizeHeader(c.GetValue<string>() ?? string.Empty)
                }).ToList();

                var matches = new Dictionary<string, int>();

                foreach (var entry in normalizedSynonyms)
                {
                    var match = cells.FirstOrDefault(c => entry.Value.Contains(c.Value));
                    if (match != null)
                    {
                        matches[entry.Key] = match.Column;
                    }
                }

                if (requiredKeys.All(matches.ContainsKey))
                {
                    return (matches, row.RowNumber());
                }
            }

            throw new InvalidDataException($"Could not locate required headers: {string.Join(", ", requiredKeys)}");
        }
    }
}
