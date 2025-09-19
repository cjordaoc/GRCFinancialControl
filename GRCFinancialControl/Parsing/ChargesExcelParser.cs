using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace GRCFinancialControl.Parsing
{
    public sealed class ChargesExcelParser : ExcelParserBase<ChargeRow>
    {
        private static readonly HeaderSchema Schema = new(new Dictionary<string, string[]>
        {
            ["EMPLOYEE"] = new[] { "EMPLOYEE", "EMPLOYEE NAME", "RESOURCE", "NOME" },
            ["DATE"] = new[] { "DATE", "CHARGE DATE", "DATA", "DAY" },
            ["HOURS"] = new[] { "HOURS", "HOURS CHARGED", "HORAS", "QTD HORAS" },
            ["COST"] = new[] { "COST", "AMOUNT", "COST AMOUNT", "VALOR" }
        }, "EMPLOYEE", "DATE", "HOURS");

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

                if (!TryGetDate(row.Cell(headers["DATE"]), out var chargeDate))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid charge date.");
                    continue;
                }

                if (!TryGetDecimal(row.Cell(headers["HOURS"]), out var hours))
                {
                    result.IncrementSkipped($"Row {row.RowNumber()}: Invalid hours value.");
                    continue;
                }

                decimal? costAmount = null;
                if (headers.TryGetValue("COST", out var costColumn))
                {
                    if (TryGetNullableDecimal(row.Cell(costColumn), out var costCandidate))
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
}
