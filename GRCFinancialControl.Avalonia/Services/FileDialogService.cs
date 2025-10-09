using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GRCFinancialControl.Core.Services;

namespace GRCFinancialControl.Avalonia.Services
{
    public class FileDialogService : IFileDialogService
    {
        private readonly Window _target;

        public FileDialogService(Window target)
        {
            _target = target;
        }

        public async Task<IReadOnlyList<string>> GetFiles(FileDialogRequest request)
        {
            var storageProvider = _target.StorageProvider;
            if (!storageProvider.CanOpen)
            {
                return Array.Empty<string>();
            }

            var options = new FilePickerOpenOptions
            {
                Title = request.Title,
                AllowMultiple = request.AllowMultiple,
                FileTypeFilter = request.Filters
                    .Select(f => new FilePickerFileType(f.Name) { Patterns = f.Extensions.Select(e => $"*.{e}").ToList() })
                    .ToList()
            };

            var files = await storageProvider.OpenFilePickerAsync(options);

            return files.Select(f => f.Path.LocalPath).ToList();
        }

        public async Task<string?> SaveFile(SaveFileDialogRequest request)
        {
            var storageProvider = _target.StorageProvider;
            if (!storageProvider.CanSave)
            {
                return null;
            }

            var options = new FilePickerSaveOptions
            {
                Title = request.Title,
                SuggestedFileName = request.SuggestedFileName,
                FileTypeChoices = request.Filters
                    .Select(f => new FilePickerFileType(f.Name) { Patterns = f.Extensions.Select(e => $"*.{e}").ToList() })
                    .ToList(),
                ShowOverwritePrompt = true
            };

            var file = await storageProvider.SaveFilePickerAsync(options);

            return file?.Path.LocalPath;
        }
    }
}