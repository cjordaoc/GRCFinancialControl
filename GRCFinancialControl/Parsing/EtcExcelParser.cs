using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace GRCFinancialControl.Parsing
{
    public sealed class EtcExcelParser : ExcelParserBase<EtcRow>
    {
        private static readonly HeaderSchema Schema = new(new Dictionary<string, string[]>
        {
            ["ENGAGEMENT"] = new[] { "ENGAGEMENT", "ENGAGEMENT ID", "ENG_ID", "PROJECT" },
            ["EMPLOYEE"] = new[] { "EMPLOYEE", "EMPLOYEE NAME", "NAME", "RESOURCE", "PROFISSIONAL" },
            ["LEVEL"] = new[] { "LEVEL", "RANK", "GRADE", "FUNCAO", "CARGO" },
            ["HOURS_INCURRED"] = new[] { "HOURS INCURRED", "HOURS IN CURRED", "HOURS ACTUAL", "ACTUAL HOURS", "HORAS INCORRIDAS" },
            ["ETC"] = new[] { "ETC REMAINING", "ETC", "REMAINING", "HORAS ETC" }
        }, "ENGAGEMENT", "EMPLOYEE", "HOURS_INCURRED", "ETC");

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
            var (headers, headerRow) = ValidateHeaders(worksheet, Schema);

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var engagementId = GetCellString(row.Cell(headers["ENGAGEMENT"]));
                if (string.IsNullOrWhiteSpace(engagementId))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Missing engagement id.");
                    continue;
                }

                var employeeName = GetCellString(row.Cell(headers["EMPLOYEE"]));
                if (string.IsNullOrWhiteSpace(employeeName))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Missing employee name.");
                    continue;
                }

                var rawLevel = headers.TryGetValue("LEVEL", out var levelColumn)
                    ? GetCellString(row.Cell(levelColumn))
                    : string.Empty;

                if (!TryGetDecimal(row.Cell(headers["HOURS_INCURRED"]), out var hoursIncurred))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid hours incurred.");
                    continue;
                }

                if (!TryGetDecimal(row.Cell(headers["ETC"]), out var etcRemaining))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid ETC remaining.");
                    continue;
                }

                result.AddRow(new EtcRow
                {
                    EngagementId = engagementId,
                    EmployeeName = employeeName,
                    RawLevel = rawLevel,
                    HoursIncurred = hoursIncurred,
                    EtcRemaining = etcRemaining
                });
            }

            return result;
        }
    }
}
