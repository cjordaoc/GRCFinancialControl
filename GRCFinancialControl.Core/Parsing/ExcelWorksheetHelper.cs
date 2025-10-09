using System;
using System.IO;
using System.Linq;
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

        public static IXLWorksheet SelectWorksheet(IXLWorksheets worksheets, params string[]? preferredNames)
        {
            ArgumentNullException.ThrowIfNull(worksheets);

            if (preferredNames != null && preferredNames.Length > 0)
            {
                var orderedNames = preferredNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .ToArray();

                if (orderedNames.Length > 0)
                {
                    foreach (var candidate in orderedNames)
                    {
                        var worksheet = worksheets
                            .FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible &&
                                                 string.Equals(w.Name.Trim(), candidate, StringComparison.OrdinalIgnoreCase));
                        if (worksheet != null)
                        {
                            return worksheet;
                        }
                    }

                    foreach (var candidate in orderedNames)
                    {
                        var worksheet = worksheets
                            .FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible &&
                                                 w.Name.Contains(candidate, StringComparison.OrdinalIgnoreCase));
                        if (worksheet != null)
                        {
                            return worksheet;
                        }
                    }
                }
            }

            return FirstVisible(worksheets);
        }
    }
}
