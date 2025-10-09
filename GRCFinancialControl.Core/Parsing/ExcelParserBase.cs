using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using GRCFinancialControl.Common;

namespace GRCFinancialControl.Parsing
{
    public abstract class ExcelParserBase<TRow>
    {
        protected (Dictionary<string, int> Headers, int HeaderRow) ValidateHeaders(IXLWorksheet worksheet, HeaderSchema schema)
        {
            return ExcelHeaderDetector.DetectHeaders(worksheet, schema);
        }

        protected static bool TryGetDecimal(IXLCell cell, out decimal value) => ExcelParsingUtilities.TryGetDecimal(cell, out value);
        protected static bool TryGetNullableDecimal(IXLCell cell, out decimal? value) => ExcelParsingUtilities.TryGetNullableDecimal(cell, out value);
        protected static bool TryGetDate(IXLCell cell, out DateOnly date) => ExcelParsingUtilities.TryGetDate(cell, out date);
        protected static string GetCellString(IXLCell cell) => ExcelParsingUtilities.GetCellString(cell);
        protected static bool IsRowEmpty(IXLRow row) => ExcelParsingUtilities.IsRowEmpty(row);
        protected static string? TrimToNull(string? value) => StringNormalizer.TrimToNull(value);
    }
}
