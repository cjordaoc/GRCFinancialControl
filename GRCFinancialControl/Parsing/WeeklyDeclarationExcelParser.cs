using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using GRCFinancialControl.Common;

namespace GRCFinancialControl.Parsing
{
    public sealed class WeeklyDeclarationExcelParser : ExcelParserBase<WeeklyDeclarationRow>
    {
        private static readonly HeaderSchema Schema = new(new Dictionary<string, string[]>
        {
            ["EMPLOYEE"] = new[] { "EMPLOYEE", "EMPLOYEE NAME", "RESOURCE", "NOME" },
            ["WEEK"] = new[] { "WEEK START", "WEEK OF", "WEEK", "DATA INICIO", "INICIO" },
            ["HOURS"] = new[] { "DECLARED HOURS", "HOURS", "ALLOCATED HOURS", "HORAS" }
        }, "EMPLOYEE", "WEEK", "HOURS");

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

            var (headers, headerRow) = ValidateHeaders(worksheet, Schema);

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var employeeName = GetCellString(row.Cell(headers["EMPLOYEE"]));
                if (string.IsNullOrWhiteSpace(employeeName))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Missing employee name.");
                    continue;
                }

                if (!TryGetDate(row.Cell(headers["WEEK"]), out var weekStart))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid week start date.");
                    continue;
                }

                if (!TryGetDecimal(row.Cell(headers["HOURS"]), out var hours))
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
}
