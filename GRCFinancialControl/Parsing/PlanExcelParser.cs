using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace GRCFinancialControl.Parsing
{
    public sealed class PlanExcelParser : ExcelParserBase<PlanRow>
    {
        private static readonly HeaderSchema Schema = new(new Dictionary<string, string[]>
        {
            ["LEVEL"] = new[] { "LEVEL", "RANK", "GRADE", "FUNCAO", "CARGO" },
            ["HOURS"] = new[] { "PLANNED HOURS", "HOURS", "TOTAL HOURS", "BUDGET HOURS" },
            ["RATE"] = new[] { "PLANNED RATE", "RATE", "RATE HOUR", "BILL RATE" }
        }, "LEVEL", "HOURS");

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
            var (headers, headerRow) = ValidateHeaders(worksheet, Schema);

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var level = GetCellString(row.Cell(headers["LEVEL"]));
                if (string.IsNullOrWhiteSpace(level))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Missing level.");
                    continue;
                }

                if (!TryGetDecimal(row.Cell(headers["HOURS"]), out var hours))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid planned hours.");
                    continue;
                }

                decimal? plannedRate = null;
                if (headers.TryGetValue("RATE", out var rateColumn))
                {
                    if (TryGetNullableDecimal(row.Cell(rateColumn), out var rateCandidate))
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
}
