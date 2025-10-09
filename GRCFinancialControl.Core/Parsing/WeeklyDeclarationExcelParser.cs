using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using GRCFinancialControl.Common;

namespace GRCFinancialControl.Parsing
{
    public sealed class WeeklyDeclarationExcelParser : ExcelParserBase<WeeklyDeclarationRow>
    {
        private static readonly HeaderSchema TabularSchema = FileFieldUploadMapProvider.Instance.BuildSchema(
            new Dictionary<string, string[]>
            {
                ["ENGAGEMENT"] = new[] { "ENGAGEMENT", "ENGAGEMENTID", "ENGAGEMENT ID", "ENG_ID", "PROJECT" },
                ["EMPLOYEE"] = new[] { "EMPLOYEE", "EMPLOYEENAME", "EMPLOYEE NAME", "RESOURCE", "NOME" },
                ["WEEK"] = new[] { "WEEK START", "WEEK OF", "WEEK", "DATA INICIO", "INICIO" },
                ["HOURS"] = new[] { "DECLARED HOURS", "HOURS", "ALLOCATED HOURS", "HORAS" }
            },
            "ENGAGEMENT",
            "EMPLOYEE",
            "WEEK",
            "HOURS");

        private static readonly HeaderSchema RetainSchema = FileFieldUploadMapProvider.Instance.BuildSchema(
            new Dictionary<string, string[]>
            {
                ["EMPLOYEE"] = new[] { "EMPRESOURCENAME", "EMP RESOURCE  NAME", "EMPLOYEE", "NOME", "RESOURCE" },
                ["EMPLOYEE_ID"] = new[] { "EMPRESOURCEGPN", "EMP RESOURCE  GPN", "GPN", "RESOURCEID" },
                ["ENGAGEMENT_ID"] = new[] { "ENGAGEMENTNUMBER", "ENGAGEMENT  NUMBER", "PROJECTID", "ENGAGEMENTID", "ENGAGEMENTNO" },
                ["ENGAGEMENT"] = new[] { "ENGAGEMENT", "ENGAGEMENT NAME", "PROJETO" },
                ["LEVEL"] = new[] { "EMP GRADE", "GRADE", "LEVEL", "NIVEL", "NÍVEL" },
                ["CUSTOMER"] = new[] { "CUSTOMER", "CLIENT", "CLIENTE" }
            },
            "EMPLOYEE",
            "ENGAGEMENT_ID");

        private static readonly HeaderSchema ErpSchema = FileFieldUploadMapProvider.Instance.BuildSchema(
            new Dictionary<string, string[]>
            {
                ["EMPLOYEE"] = new[] { "RECURSOS", "RECURSO", "EMPLOYEE", "STAFF" }
            },
            "EMPLOYEE");

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

            using var workbook = new XLWorkbook(filePath);
            var visibleWorksheets = workbook.Worksheets.Where(w => w.Visibility == XLWorksheetVisibility.Visible).ToList();

            foreach (var worksheet in visibleWorksheets)
            {
                if (IsRetainWorksheet(worksheet))
                {
                    return ParseRetain(worksheet);
                }
            }

            foreach (var worksheet in visibleWorksheets)
            {
                if (IsErpWorksheet(worksheet))
                {
                    return ParseErp(worksheet);
                }
            }

            var fallbackWorksheet = ExcelWorksheetHelper.FirstVisible(workbook.Worksheets);
            return ParseTabular(fallbackWorksheet);
        }

        private ExcelParseResult<WeeklyDeclarationRow> ParseRetain(IXLWorksheet worksheet)
        {
            var result = new ExcelParseResult<WeeklyDeclarationRow>("Weekly Declaration");
            var (headers, headerRow) = ValidateHeaders(worksheet, RetainSchema);
            var metadataColumns = new HashSet<int>(headers.Values);
            var weeklyColumns = DetectWeeklyColumns(worksheet, headerRow, metadataColumns);

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var rowNumber = row.RowNumber();
                var employeeNameRaw = TrimToNull(GetCellString(row.Cell(headers["EMPLOYEE"])));
                if (employeeNameRaw == null)
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing employee name.");
                    continue;
                }

                var engagementDescriptor = TrimToNull(GetCellString(row.Cell(headers["ENGAGEMENT_ID"])));
                if (engagementDescriptor == null)
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing engagement identifier.");
                    continue;
                }

                var engagementId = EngagementIdExtractor.TryExtract(engagementDescriptor, out var extractedId)
                    ? extractedId
                    : engagementDescriptor;

                var normalizedEmployeeName = NormalizeEmployeeName(employeeNameRaw);

                foreach (var weekly in weeklyColumns)
                {
                    var cell = row.Cell(weekly.Key);
                    if (TryGetDecimal(cell, out var hours))
                    {
                        if (hours == 0)
                        {
                            continue;
                        }

                        var weekStart = WeekHelper.ToWeekStart(weekly.Value);
                        result.AddRow(new WeeklyDeclarationRow
                        {
                            EngagementId = engagementId,
                            EmployeeName = normalizedEmployeeName,
                            WeekStart = weekStart,
                            DeclaredHours = hours
                        });
                    }
                    else if (!cell.IsEmpty())
                    {
                        result.AddWarning($"Row {rowNumber}: Invalid hours '{cell.GetValue<string>()}' for {weekly.Value:yyyy-MM-dd}.");
                    }
                }
            }

            return result;
        }

        private ExcelParseResult<WeeklyDeclarationRow> ParseErp(IXLWorksheet worksheet)
        {
            var result = new ExcelParseResult<WeeklyDeclarationRow>("Weekly Declaration");
            var (headers, headerRow) = ValidateHeaders(worksheet, ErpSchema);
            var metadataColumns = new HashSet<int>(headers.Values);
            var weeklyColumns = DetectWeeklyColumns(worksheet, headerRow, metadataColumns);

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var rowNumber = row.RowNumber();
                var employeeNameRaw = TrimToNull(GetCellString(row.Cell(headers["EMPLOYEE"])));
                if (employeeNameRaw == null)
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing employee name.");
                    continue;
                }

                var normalizedEmployeeName = NormalizeEmployeeName(employeeNameRaw);

                foreach (var weekly in weeklyColumns)
                {
                    var cellValue = TrimToNull(GetCellString(row.Cell(weekly.Key)));
                    if (cellValue == null)
                    {
                        continue;
                    }

                    if (!EngagementIdExtractor.TryExtract(cellValue, out var engagementId))
                    {
                        continue;
                    }

                    var weekStart = WeekHelper.ToWeekStart(weekly.Value);
                    result.AddRow(new WeeklyDeclarationRow
                    {
                        EngagementId = engagementId,
                        EmployeeName = normalizedEmployeeName,
                        WeekStart = weekStart,
                        DeclaredHours = 40m
                    });
                }
            }

            return result;
        }

        private ExcelParseResult<WeeklyDeclarationRow> ParseTabular(IXLWorksheet worksheet)
        {
            var result = new ExcelParseResult<WeeklyDeclarationRow>("Weekly Declaration");
            var (headers, headerRow) = ValidateHeaders(worksheet, TabularSchema);

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var engagementValue = TrimToNull(GetCellString(row.Cell(headers["ENGAGEMENT"])));
                if (engagementValue == null)
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Missing engagement id.");
                    continue;
                }

                var engagementId = EngagementIdExtractor.TryExtract(engagementValue, out var extracted)
                    ? extracted
                    : engagementValue;

                var employeeNameRaw = TrimToNull(GetCellString(row.Cell(headers["EMPLOYEE"])));
                if (employeeNameRaw == null)
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Missing employee name.");
                    continue;
                }

                if (!TryGetDate(row.Cell(headers["WEEK"]), out var weekStartRaw))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid week start date.");
                    continue;
                }

                if (!TryGetDecimal(row.Cell(headers["HOURS"]), out var hours))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid declared hours.");
                    continue;
                }

                var normalizedEmployeeName = NormalizeEmployeeName(employeeNameRaw);
                var normalizedWeek = WeekHelper.ToWeekStart(weekStartRaw);

                result.AddRow(new WeeklyDeclarationRow
                {
                    EngagementId = engagementId,
                    EmployeeName = normalizedEmployeeName,
                    WeekStart = normalizedWeek,
                    DeclaredHours = hours
                });
            }

            return result;
        }

        private static Dictionary<int, DateOnly> DetectWeeklyColumns(IXLWorksheet worksheet, int headerRow, HashSet<int> metadataColumns)
        {
            var detected = new Dictionary<int, DateOnly>();
            var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var maxScanRow = Math.Max(headerRow + 6, headerRow);

            for (var column = 1; column <= lastColumn; column++)
            {
                if (metadataColumns.Contains(column))
                {
                    continue;
                }

                var headerText = ExcelParsingUtilities.GetCombinedHeaderText(worksheet, column, Math.Max(headerRow, 1));
                if (ExcelParsingUtilities.TryParseDateFromText(headerText, out var parsedDate))
                {
                    detected[column] = parsedDate;
                    continue;
                }

                for (var row = Math.Max(1, headerRow - 2); row <= maxScanRow; row++)
                {
                    if (ExcelParsingUtilities.TryGetDate(worksheet.Cell(row, column), out parsedDate))
                    {
                        detected[column] = parsedDate;
                        break;
                    }
                }
            }

            return detected;
        }

        private static bool IsRetainWorksheet(IXLWorksheet worksheet)
        {
            return WorksheetHasHeader(worksheet, "EMPRESOURCEGPN") &&
                   WorksheetHasHeader(worksheet, "EMPRESOURCENAME") &&
                   WorksheetHasHeader(worksheet, "ENGAGEMENTNUMBER");
        }

        private static bool IsErpWorksheet(IXLWorksheet worksheet)
        {
            return WorksheetHasHeader(worksheet, "RECURSOS") || WorksheetHasHeader(worksheet, "RECURSO");
        }

        private static bool WorksheetHasHeader(IXLWorksheet worksheet, string normalizedHeader)
        {
            var lastColumn = Math.Min(worksheet.LastColumnUsed()?.ColumnNumber() ?? 0, 50);
            var maxRow = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 0, 20);

            for (var row = 1; row <= maxRow; row++)
            {
                for (var column = 1; column <= lastColumn; column++)
                {
                    var candidate = ExcelParsingUtilities.NormalizeHeader(worksheet.Cell(row, column).GetValue<string>() ?? string.Empty);
                    if (string.Equals(candidate, normalizedHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeEmployeeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var cleaned = name.Trim();
            var lower = cleaned.ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lower);
        }
    }
}
