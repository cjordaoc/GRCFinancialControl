using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using GRCFinancialControl.Common;

namespace GRCFinancialControl.Parsing
{
    public sealed class PlanExcelParser : ExcelParserBase<PlanRow>
    {
        private static readonly HeaderSchema Schema = FileFieldUploadMapProvider.Instance.BuildSchema(
            new Dictionary<string, string[]>
            {
                ["ENGAGEMENT"] = new[] { "ENGAGEMENT", "ENGAGEMENTID", "ENGAGEMENTNUMBER", "PROJECTID", "PROJECT", "ENGAGEMENTNO" },
                ["EMPLOYEE"] = new[] { "EMPLOYEE", "EMPRESOURCENAME", "RESOURCE", "EMPLOYEENAME", "RECURSO", "PROFISSIONAL" },
                ["LEVEL"] = new[] { "LEVEL", "RANK", "GRADE", "FUNCAO", "FUNÇÃO", "CARGO", "NIVEL", "NÍVEL" },
                ["RESOURCE_ID"] = new[] { "EMPRESOURCEGPN", "RESOURCEGPN", "GPN", "EMPLOYEEID", "EMPLOYEEGPN", "RECURSOGPN" },
                ["CUSTOMER"] = new[] { "CUSTOMER", "CLIENT", "CLIENTE", "CLIENTNAME" },
                ["RATE"] = new[] { "PLANNEDRATE", "RATE", "RATEHOUR", "BILLRATE", "COSTRATE", "COST/HR" },
                ["TOTAL_HOURS"] = new[] { "PLANNEDHOURS", "TOTALHOURS", "BUDGETHOURS", "HOURS", "TOTAL" }
            },
            "LEVEL");

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
            var engagementIdFromPlanInfo = ExtractEngagementId(workbook);

            var worksheet = ExcelWorksheetHelper.SelectWorksheet(
                workbook.Worksheets,
                "RESOURCING",
                "Planilha1",
                "Alocações_Staff",
                "Alocacoes_Staff",
                "Export");

            var (headers, headerRow) = ValidateHeaders(worksheet, Schema);
            var metadataColumns = new HashSet<int>(headers.Values);
            var weeklyColumns = DetectWeeklyColumns(worksheet, headerRow, metadataColumns);

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var rowNumber = row.RowNumber();
                var engagementId = ResolveEngagementId(row, headers, engagementIdFromPlanInfo);
                if (engagementId == null)
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing engagement identifier.");
                    continue;
                }

                var levelRaw = TrimToNull(GetCellString(row.Cell(headers["LEVEL"])));
                if (levelRaw == null)
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing level.");
                    continue;
                }

                var normalizedLevel = LevelNormalizer.Normalize(levelRaw);

                var normalizedEmployeeName = ResolveEmployeeName(row, headers);

                var resourceId = headers.TryGetValue("RESOURCE_ID", out var resourceColumn)
                    ? TrimToNull(GetCellString(row.Cell(resourceColumn)))
                    : null;

                var customerName = headers.TryGetValue("CUSTOMER", out var customerColumn)
                    ? TrimToNull(GetCellString(row.Cell(customerColumn)))
                    : null;

                decimal? plannedRate = null;
                if (headers.TryGetValue("RATE", out var rateColumn))
                {
                    if (TryGetNullableDecimal(row.Cell(rateColumn), out var rateCandidate))
                    {
                        plannedRate = rateCandidate;
                    }
                    else if (!row.Cell(rateColumn).IsEmpty())
                    {
                        result.AddWarning($"Row {rowNumber}: Unable to parse rate '{row.Cell(rateColumn).GetValue<string>()}'.");
                    }
                }

                var weeklyHours = ExtractWeeklyHours(row, weeklyColumns, result);
                var plannedHoursResolved = false;
                decimal plannedHours = 0m;

                if (headers.TryGetValue("TOTAL_HOURS", out var totalColumn))
                {
                    var totalCell = row.Cell(totalColumn);
                    if (TryGetDecimal(totalCell, out var totalValue))
                    {
                        plannedHours = totalValue;
                        plannedHoursResolved = true;
                    }
                    else if (!totalCell.IsEmpty())
                    {
                        result.IncrementSkipped($"Row {rowNumber}: Invalid total hours '{totalCell.GetValue<string>()}' in Hours column.");
                        continue;
                    }
                }

                if (!plannedHoursResolved && weeklyHours.Count > 0)
                {
                    plannedHours = weeklyHours.Values.Sum();
                    plannedHoursResolved = true;
                }

                if (!plannedHoursResolved)
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing planned hours total.");
                    continue;
                }

                var planRow = new PlanRow
                {
                    EngagementId = engagementId,
                    EmployeeName = normalizedEmployeeName,
                    ResourceId = resourceId ?? string.Empty,
                    CustomerName = customerName ?? string.Empty,
                    RawLevel = levelRaw,
                    NormalizedLevel = normalizedLevel,
                    PlannedHours = plannedHours,
                    PlannedRate = plannedRate
                };

                foreach (var entry in weeklyHours)
                {
                    planRow.WeeklyHours[entry.Key] = entry.Value;
                }

                result.AddRow(planRow);
            }

            return result;
        }

        private static string? ResolveEngagementId(IXLRow row, IReadOnlyDictionary<string, int> headers, string? defaultEngagementId)
        {
            if (headers.TryGetValue("ENGAGEMENT", out var engagementColumn))
            {
                var candidate = TrimToNull(GetCellString(row.Cell(engagementColumn)));
                if (candidate != null)
                {
                    if (EngagementIdExtractor.TryExtract(candidate, out var extracted))
                    {
                        return extracted;
                    }

                    return candidate;
                }
            }

            return defaultEngagementId;
        }

        private static string ResolveEmployeeName(IXLRow row, IReadOnlyDictionary<string, int> headers)
        {
            if (!headers.TryGetValue("EMPLOYEE", out var employeeColumn))
            {
                return string.Empty;
            }

            var employeeNameRaw = TrimToNull(GetCellString(row.Cell(employeeColumn)));
            return employeeNameRaw == null ? string.Empty : NormalizeName(employeeNameRaw);
        }

        private static string? ExtractEngagementId(XLWorkbook workbook)
        {
            var planInfo = ExcelWorksheetHelper.SelectWorksheet(workbook.Worksheets, "PLAN INFO", "PLANINFO", "PLAN_INFO");
            var candidate = TrimToNull(ExcelParsingUtilities.GetCellString(planInfo.Cell("B5")));
            if (candidate != null && EngagementIdExtractor.TryExtract(candidate, out var extracted))
            {
                return extracted;
            }

            return candidate;
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

        private static Dictionary<DateOnly, decimal> ExtractWeeklyHours(IXLRow row, IReadOnlyDictionary<int, DateOnly> weeklyColumns, ExcelParseResult<PlanRow> result)
        {
            var values = new Dictionary<DateOnly, decimal>();

            foreach (var entry in weeklyColumns)
            {
                var cell = row.Cell(entry.Key);
                if (TryGetDecimal(cell, out var hours))
                {
                    values[entry.Value] = hours;
                }
                else if (!cell.IsEmpty())
                {
                    result.AddWarning($"Row {row.RowNumber()}: Unable to parse planned hours '{cell.GetValue<string>()}' for {entry.Value:yyyy-MM-dd}.");
                }
            }

            return values;
        }

        private static string NormalizeName(string name)
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
