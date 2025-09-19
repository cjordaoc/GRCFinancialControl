using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using GRCFinancialControl.Common;

namespace GRCFinancialControl.Parsing
{
    public sealed class MarginDataExcelParser : ExcelParserBase<MarginDataRow>
    {
        private static readonly HeaderSchema Schema = new(new Dictionary<string, string[]>
        {
            ["ENGAGEMENT"] = new[] { "ENGAGEMENTNAME(IDCURRENCY)", "ENGAGEMENTNAMEID", "ENGAGEMENTNAME", "ENGAGEMENT" },
            ["CLIENT"] = new[] { "CLIENTNAME(ID)", "CLIENTNAME", "CLIENT" },
            ["ETCP_INDICATOR"] = new[] { "ETCPINDICATORTOOLTIP", "ETCINDICATORTOOLTIP", "ETCPINDICATOR" },
            ["MARGIN_PCT_BUD"] = new[] { "MARGINPCTBUD", "MARGINPERCENTBUD", "MARGINPCTBUDGET" },
            ["MARGIN_PCT_ETCP"] = new[] { "MARGINPCTETCP", "MARGINPCTETC", "MARGINPCTETC-P" },
            ["MARGIN_PCT_MERCURY"] = new[] { "MARGINPCTMERCURYPROJECTED", "MARGINPCTMERCURY" },
            ["MARGIN_BUD"] = new[] { "MARGINBUD" },
            ["MARGIN_ETCP"] = new[] { "MARGINETCP" },
            ["MARGIN_MERCURY"] = new[] { "MARGINMERCURYPROJECTED", "MARGINMERCURY" },
            ["MARGIN_PCT_LOSS_GAIN"] = new[] { "MARGINPCTPLOSSGAIN", "MARGINPCTLOSSGAIN" },
            ["MARGIN_LOSS_GAIN"] = new[] { "MARGINLOSSGAIN" },
            ["BILLING_OVERRUN"] = new[] { "BILLINGOVERRUN" },
            ["EXPENSES_OVERRUN"] = new[] { "EXPENSESOVERRUN" },
            ["MARGIN_COST_OVERRUN"] = new[] { "MARGINCOSTOVERRUN" },
            ["MARGIN_PCT_ACT"] = new[] { "MARGINPCTACT", "MARGINPCTACTUAL" },
            ["ETCP_AGE_DAYS"] = new[] { "ETCPAGEDAYS", "ETCAGEDAYS" },
            ["REMAINING_WEEKS"] = new[] { "REMAININGWEEKS", "WEEKSREMAINING" },
            ["STATUS"] = new[] { "STATUS" },
            ["ENGAGEMENT_COUNT"] = new[] { "ENGAGEMENTCOUNT" }
        }, "ENGAGEMENT");

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
            var worksheet = ExcelWorksheetHelper.SelectWorksheet(workbook.Worksheets, "Export", "RESOURCING");
            var (headers, headerRow) = ValidateHeaders(worksheet, Schema);

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow))
            {
                if (IsRowEmpty(row))
                {
                    continue;
                }

                var rowNumber = row.RowNumber();
                var descriptor = GetCellString(row.Cell(headers["ENGAGEMENT"]));
                if (string.IsNullOrWhiteSpace(descriptor))
                {
                    result.IncrementSkipped($"Row {rowNumber}: Missing engagement descriptor.");
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
                    EngagementTitle = engagementTitle,
                    ClientName = headers.TryGetValue("CLIENT", out var clientColumn) ? ExtractName(GetCellString(row.Cell(clientColumn))) : null,
                    EtcIndicatorTooltip = headers.TryGetValue("ETCP_INDICATOR", out var indicatorColumn) ? TrimToNull(GetCellString(row.Cell(indicatorColumn))) : null
                };

                marginRow.BudgetMarginPercent = ReadPercentage(row, headers, "MARGIN_PCT_BUD", "Margin % Bud", result);
                marginRow.EtcMarginPercent = ReadPercentage(row, headers, "MARGIN_PCT_ETCP", "Margin % ETC-P", result);
                marginRow.MercuryProjectedMarginPercent = ReadPercentage(row, headers, "MARGIN_PCT_MERCURY", "Margin % Mercury Projected", result);
                marginRow.BudgetMarginValue = ReadDecimal(row, headers, "MARGIN_BUD", "Margin Bud", result);
                marginRow.EtcMarginValue = ReadDecimal(row, headers, "MARGIN_ETCP", "Margin ETC-P", result);
                marginRow.MercuryProjectedMarginValue = ReadDecimal(row, headers, "MARGIN_MERCURY", "Margin Mercury Projected", result);
                marginRow.MarginLossGainPercent = ReadPercentage(row, headers, "MARGIN_PCT_LOSS_GAIN", "Margin %p Loss/Gain", result);
                marginRow.MarginLossGainValue = ReadDecimal(row, headers, "MARGIN_LOSS_GAIN", "Margin Loss/Gain", result);
                marginRow.BillingOverrun = ReadDecimal(row, headers, "BILLING_OVERRUN", "Billing Overrun", result);
                marginRow.ExpensesOverrun = ReadDecimal(row, headers, "EXPENSES_OVERRUN", "Expenses Overrun", result);
                marginRow.MarginCostOverrun = ReadDecimal(row, headers, "MARGIN_COST_OVERRUN", "Margin Cost Overrun", result);
                marginRow.ActualMarginPercent = ReadPercentage(row, headers, "MARGIN_PCT_ACT", "Margin % Act", result);
                marginRow.EtcAgeDays = ReadInt(row, headers, "ETCP_AGE_DAYS", "ETC-P Age (Days)", result);
                marginRow.RemainingWeeks = ReadInt(row, headers, "REMAINING_WEEKS", "Remaining Weeks", result);
                marginRow.Status = headers.TryGetValue("STATUS", out var statusColumn) ? TrimToNull(GetCellString(row.Cell(statusColumn))) : null;
                marginRow.EngagementCount = ReadInt(row, headers, "ENGAGEMENT_COUNT", "Engagement Count", result);

                marginRow.OpeningMargin = marginRow.BudgetMarginPercent;
                marginRow.CurrentMargin = marginRow.EtcMarginPercent;
                marginRow.MarginValue = marginRow.ActualMarginPercent;

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

        private static string? ExtractName(string descriptor)
        {
            var trimmed = StringNormalizer.TrimToNull(descriptor);
            if (trimmed == null)
            {
                return null;
            }

            var openIndex = trimmed.LastIndexOf('(');
            if (openIndex > 0)
            {
                return StringNormalizer.TrimToNull(trimmed[..openIndex]);
            }

            return trimmed;
        }

        private static decimal? ReadPercentage(IXLRow row, IDictionary<string, int> headers, string key, string label, MarginDataParseResult result)
        {
            if (!headers.TryGetValue(key, out var column))
            {
                return null;
            }

            var cell = row.Cell(column);
            if (ExcelParsingUtilities.TryGetPercentage(cell, out var value))
            {
                return value;
            }

            var raw = ExcelParsingUtilities.GetCellString(cell);
            if (!string.IsNullOrEmpty(raw))
            {
                result.AddWarning($"Row {row.RowNumber()}: Invalid {label} '{raw}'.");
            }

            return null;
        }

        private static decimal? ReadDecimal(IXLRow row, IDictionary<string, int> headers, string key, string label, MarginDataParseResult result)
        {
            if (!headers.TryGetValue(key, out var column))
            {
                return null;
            }

            var cell = row.Cell(column);
            if (ExcelParsingUtilities.TryGetNullableDecimal(cell, out var value))
            {
                return value;
            }

            var raw = ExcelParsingUtilities.GetCellString(cell);
            if (!string.IsNullOrEmpty(raw))
            {
                result.AddWarning($"Row {row.RowNumber()}: Invalid {label} '{raw}'.");
            }

            return null;
        }

        private static int? ReadInt(IXLRow row, IDictionary<string, int> headers, string key, string label, MarginDataParseResult result)
        {
            if (!headers.TryGetValue(key, out var column))
            {
                return null;
            }

            var cell = row.Cell(column);
            if (ExcelParsingUtilities.TryGetNullableInt(cell, out var value))
            {
                return value;
            }

            var raw = ExcelParsingUtilities.GetCellString(cell);
            if (!string.IsNullOrEmpty(raw))
            {
                result.AddWarning($"Row {row.RowNumber()}: Invalid {label} '{raw}'.");
            }

            return null;
        }
    }
}
