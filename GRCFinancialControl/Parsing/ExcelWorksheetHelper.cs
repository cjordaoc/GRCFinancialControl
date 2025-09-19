using System;
using System.IO;
using ClosedXML.Excel;

namespace GRCFinancialControl.Parsing
{
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
}
