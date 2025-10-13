using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CsvHelper;
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

        public async Task ExportToCsvAsync<T>(IEnumerable<T> data, string fileName)
        {
            var filePath = await _filePickerService.SaveFileAsync(fileName);
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            using (var writer = new StreamWriter(filePath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csv.WriteRecordsAsync(data);
            }
        }
    }
}