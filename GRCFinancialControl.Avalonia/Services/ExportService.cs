using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using ClosedXML.Excel;
using GRCFinancialControl.Avalonia.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.Services
{
    public class ExportService : IExportService
    {
        private readonly IFilePickerService _filePickerService;

        public ExportService(IFilePickerService filePickerService)
        {
            _filePickerService = filePickerService;
        }

        public async Task ExportToExcelAsync<T>(IEnumerable<T> data, string entityName)
        {
            var exportItems = data?.ToList() ?? new List<T>();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var fileName = $"Export_{entityName}_{timestamp}.xlsx";

            var filePath = await _filePickerService.SaveFileAsync(fileName);
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet(LocalizationRegistry.Get("Exports.Worksheet.Report"));

            if (exportItems.Count == 0)
            {
                worksheet.Cell(1, 1).Value = XLCellValue.FromObject(LocalizationRegistry.Get("Exports.Status.NoData"));
                workbook.SaveAs(filePath);
                return;
            }

            var itemType = typeof(T);
            if (itemType == typeof(object))
            {
                var firstItem = exportItems.FirstOrDefault(i => i is not null);
                itemType = firstItem?.GetType() ?? typeof(object);
            }

            var properties = itemType.GetProperties()
                .Where(p => p.CanRead)
                .ToArray();

            if (properties.Length == 0)
            {
                worksheet.Cell(1, 1).Value = XLCellValue.FromObject(LocalizationRegistry.Get("Exports.Header.Value"));
                for (var row = 0; row < exportItems.Count; row++)
                {
                    worksheet.Cell(row + 2, 1).Value = XLCellValue.FromObject(exportItems[row]);
                }
            }
            else
            {
                for (var column = 0; column < properties.Length; column++)
                {
                    worksheet.Cell(1, column + 1).Value = XLCellValue.FromObject(properties[column].Name);
                }

                for (var row = 0; row < exportItems.Count; row++)
                {
                    var item = exportItems[row];
                    for (var column = 0; column < properties.Length; column++)
                    {
                        worksheet.Cell(row + 2, column + 1).Value = XLCellValue.FromObject(properties[column].GetValue(item));
                    }
                }
            }

            worksheet.RangeUsed()?.SetAutoFilter();
            worksheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
        }
    }
}