using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace GRCFinancialControl.Parsing
{
    public sealed class EtcExcelParser : ExcelParserBase<EtcRow>
    {
        private static readonly Regex EngagementIdPattern = new("E-\\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly HeaderSchema Schema = FileFieldUploadMapProvider.Instance.BuildSchema(
            new Dictionary<string, string[]>
            {
                ["ENGAGEMENT"] = new[] { "ENGAGEMENT", "ENGAGEMENTID", "ENGAGEMENTNAMEID", "PROJECT", "PROJECTID", "ENGAGEMENTNO", "ACTIVITY" },
                ["EMPLOYEE"] = new[] { "EMPLOYEE", "EMPRESOURCENAME", "EMPLOYEENAME", "RESOURCE", "PROFISSIONAL" },
                ["EMPLOYEE_ID"] = new[] { "EMPRESOURCEGPN", "RESOURCEGPN", "GPN", "EMPLOYEEID", "RESOURCEID", "ID" },
                ["LEVEL"] = new[] { "LEVEL", "RANK", "GRADE", "FUNCAO", "FUNÇÃO", "CARGO", "NIVEL", "NÍVEL" },
                ["HOURS_INCURRED"] = new[] { "ACTUALSHOURSINCURREDTHROUGHLASTWEEK", "HOURSINCURRED", "ACTUALHOURS", "HORASINCORRIDAS" },
                ["ETC"] = new[] { "ETCREMAINING", "ETC", "REMAINING", "HORASETC" },
                ["PROJECTED_MARGIN"] = new[] { "PROJECTEDMARGIN", "PROJECTEDMARGIN%", "PROJECTEDMARGINPERCENT", "MARGINPROJECTED" },
                ["STATUS"] = new[] { "STATUS", "SITUATION", "SITUAÇÃO" },
                ["ETC_AGE"] = new[] { "ETCAGE", "ETCAGEDAYS", "ETCAGEDIAS", "ETCAGEDAY", "ETC-AGEDAYS" },
                ["REMAINING_WEEKS"] = new[] { "REMAININGWEEKS", "WEEKSREMAINING", "SEMANASRESTANTES" }
            },
            "ENGAGEMENT",
            "EMPLOYEE",
            "HOURS_INCURRED",
            "ETC");

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
            var worksheet = ExcelWorksheetHelper.SelectWorksheet(
                workbook.Worksheets,
                "RESOURCING",
                "ETC INFO",
                "Detail",
                "Export");
            var fallbackEngagementId = ResolveEngagementIdFromWorkbook(workbook);
            var (headers, headerRow) = ValidateHeaders(worksheet, Schema);

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var rowNumber = row.RowNumber();
                var engagementDescriptor = TrimToNull(GetCellString(row.Cell(headers["ENGAGEMENT"])));

                if (IsSubtotalRow(engagementDescriptor))
                {
                    continue;
                }

                var engagementId = ExtractEngagementId(engagementDescriptor) ?? fallbackEngagementId;
                if (engagementId == null)
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing engagement identifier.");
                    continue;
                }

                var employeeNameRaw = TrimToNull(GetCellString(row.Cell(headers["EMPLOYEE"])));
                if (employeeNameRaw == null)
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing employee name.");
                    continue;
                }

                var employeeName = NormalizeName(employeeNameRaw);

                var resourceId = headers.TryGetValue("EMPLOYEE_ID", out var idColumn)
                    ? TrimToNull(GetCellString(row.Cell(idColumn)))
                    : null;

                var rawLevel = headers.TryGetValue("LEVEL", out var levelColumn)
                    ? TrimToNull(GetCellString(row.Cell(levelColumn))) ?? string.Empty
                    : string.Empty;
                var normalizedLevel = LevelNormalizer.Normalize(rawLevel);

                if (!TryGetDecimal(row.Cell(headers["HOURS_INCURRED"]), out var hoursIncurred))
                {
                    result.IncrementSkipped($"Row {rowNumber}: Invalid hours incurred.");
                    continue;
                }

                if (!TryGetDecimal(row.Cell(headers["ETC"]), out var etcRemaining))
                {
                    result.IncrementSkipped($"Row {rowNumber}: Invalid ETC remaining.");
                    continue;
                }

                decimal? projectedMargin = null;
                if (headers.TryGetValue("PROJECTED_MARGIN", out var projectedMarginColumn))
                {
                    if (!ExcelParsingUtilities.TryGetPercentage(row.Cell(projectedMarginColumn), out projectedMargin))
                    {
                        result.AddWarning($"Row {rowNumber}: Unable to parse projected margin '{row.Cell(projectedMarginColumn).GetValue<string>()}'.");
                    }
                }

                int? etcAge = null;
                if (headers.TryGetValue("ETC_AGE", out var etcAgeColumn))
                {
                    if (!ExcelParsingUtilities.TryGetNullableInt(row.Cell(etcAgeColumn), out etcAge))
                    {
                        result.AddWarning($"Row {rowNumber}: Invalid ETC age '{row.Cell(etcAgeColumn).GetValue<string>()}'.");
                    }
                }

                int? remainingWeeks = null;
                if (headers.TryGetValue("REMAINING_WEEKS", out var weeksColumn))
                {
                    if (!ExcelParsingUtilities.TryGetNullableInt(row.Cell(weeksColumn), out remainingWeeks))
                    {
                        result.AddWarning($"Row {rowNumber}: Invalid remaining weeks '{row.Cell(weeksColumn).GetValue<string>()}'.");
                    }
                }

                var status = headers.TryGetValue("STATUS", out var statusColumn)
                    ? TrimToNull(GetCellString(row.Cell(statusColumn)))
                    : null;

                result.AddRow(new EtcRow
                {
                    ExcelRowNumber = rowNumber,
                    EngagementId = engagementId,
                    EmployeeName = employeeName,
                    EmployeeId = resourceId ?? string.Empty,
                    RawLevel = rawLevel,
                    NormalizedLevel = normalizedLevel,
                    HoursIncurred = hoursIncurred,
                    EtcRemaining = etcRemaining,
                    ProjectedMarginPercent = projectedMargin,
                    EtcAgeDays = etcAge,
                    RemainingWeeks = remainingWeeks,
                    Status = status ?? string.Empty
                });
            }

            return result;
        }

        private static bool IsSubtotalRow(string? descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return false;
            }

            return descriptor.Equals("RESULT", StringComparison.OrdinalIgnoreCase) ||
                   descriptor.Equals("TOTAL", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ExtractEngagementId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var match = EngagementIdPattern.Match(value);
            if (!match.Success)
            {
                return null;
            }

            return match.Value.ToUpperInvariant();
        }

        private static string? ResolveEngagementIdFromWorkbook(XLWorkbook workbook)
        {
            if (workbook == null)
            {
                return null;
            }

            var infoSheet = workbook.Worksheets
                .FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible &&
                                     string.Equals(w.Name.Trim(), "ETC INFO", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets
                    .FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible &&
                                         w.Name.Contains("ETC", StringComparison.OrdinalIgnoreCase) &&
                                         w.Name.Contains("INFO", StringComparison.OrdinalIgnoreCase));

            if (infoSheet == null)
            {
                return null;
            }

            foreach (var cell in infoSheet.CellsUsed().Take(200))
            {
                var normalized = ExcelParsingUtilities.NormalizeHeader(cell.GetValue<string>() ?? string.Empty);
                if (!normalized.Contains("ENGAGEMENTID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var candidate in EnumerateInfoCells(cell))
                {
                    var resolved = ExtractEngagementId(TrimToNull(candidate?.GetValue<string>()));
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
            }

            foreach (var cell in infoSheet.CellsUsed())
            {
                var resolved = ExtractEngagementId(TrimToNull(cell.GetValue<string>()));
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return null;
        }

        private static IEnumerable<IXLCell> EnumerateInfoCells(IXLCell anchor)
        {
            if (anchor == null)
            {
                yield break;
            }

            yield return anchor;

            var right = anchor.CellRight();
            if (right != null)
            {
                yield return right;
            }

            var below = anchor.CellBelow();
            if (below != null)
            {
                yield return below;

                var diagonal = below.CellRight();
                if (diagonal != null)
                {
                    yield return diagonal;
                }
            }
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
