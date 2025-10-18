using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace App.Presentation.Services;

public class FilePickerService : IFilePickerService
{
    private readonly Window _target;

    public FilePickerService(Window target)
    {
        _target = target;
    }

    public async Task<string?> OpenFileAsync(string title = "Open File", string? defaultExtension = ".xlsx", string[]? allowedPatterns = null)
    {
        var extension = NormalizeExtension(defaultExtension);
        var patterns = NormalizePatterns(extension, allowedPatterns);

        var files = await _target.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Files")
                {
                    Patterns = patterns
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
        var fileExtension = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            fileExtension = extension;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"grcfc_{Guid.NewGuid():N}{fileExtension}");
        await using var destination = File.Create(tempPath);
        await stream.CopyToAsync(destination);

        return tempPath;
    }

    public async Task<string?> SaveFileAsync(
        string defaultFileName,
        string title = "Save File",
        string defaultExtension = ".xlsx",
        string[]? allowedPatterns = null)
    {
        var extension = NormalizeExtension(defaultExtension);
        var patterns = NormalizePatterns(extension, allowedPatterns);

        var file = await _target.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = defaultFileName,
            Title = title,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Files")
                {
                    Patterns = patterns
                }
            }
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return EnsureExtension(path!, extension);
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim();
        if (!trimmed.StartsWith('.'))
        {
            trimmed = "." + trimmed.TrimStart('*', '.');
        }

        return trimmed;
    }

    private static string[] NormalizePatterns(string extension, string[]? allowedPatterns)
    {
        if (allowedPatterns is { Length: > 0 })
        {
            return allowedPatterns.Select(p => p.StartsWith("*", StringComparison.Ordinal) ? p : "*" + p.TrimStart('.')).ToArray();
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            return new[] { "*.*" };
        }

        return new[] { "*" + extension };
    }

    private static string EnsureExtension(string path, string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return path;
        }

        var currentExtension = Path.GetExtension(path);
        if (string.Equals(currentExtension, extension, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return path + extension;
    }
}