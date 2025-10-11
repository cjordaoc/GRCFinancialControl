using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GRCFinancialControl.Avalonia.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.Services
{
    public class FilePickerService : IFilePickerService
    {
        private readonly Window _target;

        public FilePickerService(Window target)
        {
            _target = target;
        }

        public async Task<string?> OpenFileAsync()
        {
            var files = await _target.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Excel File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Excel Files")
                    {
                        Patterns = new[] { "*.xlsx", "*.xls" }
                    }
                }
            });

            return files.Count >= 1 ? files[0].TryGetLocalPath() : null;
        }
    }
}