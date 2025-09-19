using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GRCFinancialControl.Common
{
    public sealed class FileDialogRequest
    {
        public IWin32Window Owner { get; init; } = null!;
        public string Title { get; init; } = string.Empty;
        public string Filter { get; init; } = "Excel Files (*.xlsx)|*.xlsx";
        public bool AllowMultiple { get; init; }
            = false;
        public string? SuggestedFileName { get; init; }
            = null;
        public bool EnforceExactName { get; init; }
            = true;
    }

    public sealed class FileDialogService
    {
        public IReadOnlyList<string> GetFiles(FileDialogRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Owner);

            using var dialog = new OpenFileDialog
            {
                Title = request.Title,
                Filter = request.Filter,
                Multiselect = request.AllowMultiple,
                CheckFileExists = true,
                CheckPathExists = true,
                RestoreDirectory = true
            };

            if (!string.IsNullOrWhiteSpace(request.SuggestedFileName))
            {
                dialog.FileName = request.SuggestedFileName;
            }

            var result = dialog.ShowDialog(request.Owner);
            if (result != DialogResult.OK)
            {
                return Array.Empty<string>();
            }

            if (!request.AllowMultiple && request.EnforceExactName && !string.IsNullOrWhiteSpace(request.SuggestedFileName))
            {
                var selectedName = Path.GetFileName(dialog.FileName);
                if (!string.Equals(selectedName, request.SuggestedFileName, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        request.Owner,
                        $"Please select the exact file '{request.SuggestedFileName}'.",
                        "Incorrect File",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return Array.Empty<string>();
                }
            }

            var files = request.AllowMultiple ? dialog.FileNames : new[] { dialog.FileName };
            return files
                .Select(Path.GetFullPath)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
