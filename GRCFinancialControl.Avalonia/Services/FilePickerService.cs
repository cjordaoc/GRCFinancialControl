using System;
using System.IO;
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
                        Patterns = new[] { "*.xlsx" }
                    }
                }
            });

            if (files.Count == 0)
            {
                return null;
            }

            var file = files[0];
            var localPath = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                return localPath;
            }

            await using var stream = await file.OpenReadAsync();
            var extension = Path.GetExtension(file.Name);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".xlsx";
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"grcfc_{Guid.NewGuid():N}{extension}");
            await using var destination = File.Create(tempPath);
            await stream.CopyToAsync(destination);

            return tempPath;
        }

        public async Task<string?> SaveFileAsync(string defaultFileName)
        {
            var file = await _target.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = defaultFileName,
                Title = "Save Export",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Excel Files")
                    {
                        Patterns = new[] { "*.xlsx" }
                    }
                }
            });

            var path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)
                ? path
                : path + ".xlsx";
        }
    }
}