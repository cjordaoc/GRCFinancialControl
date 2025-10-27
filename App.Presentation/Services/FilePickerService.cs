using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace App.Presentation.Services;

/// <summary>
/// Provides file picker helpers backed by an Avalonia <see cref="Window"/>.
/// </summary>
public sealed class FilePickerService
{
    private const string DefaultFilterLabel = "Files";

    private readonly Window _target;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePickerService"/> class.
    /// </summary>
    /// <param name="target">The window that owns the picker dialogs.</param>
    public FilePickerService(Window target)
    {
        _target = target;
    }

    /// <summary>
    /// Displays an open file dialog and returns the selected file path, if any.
    /// </summary>
    /// <param name="title">Dialog title shown to the user.</param>
    /// <param name="defaultExtension">Default file extension to use when the picker does not provide one.</param>
    /// <param name="allowedPatterns">Optional filter patterns to restrict selectable files.</param>
    /// <returns>The absolute path to the selected file, or <c>null</c> when the dialog is cancelled.</returns>
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
                new FilePickerFileType(DefaultFilterLabel)
                {
                    Patterns = patterns
                }
            }
        }).ConfigureAwait(false);

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

        await using var stream = await file.OpenReadAsync().ConfigureAwait(false);
        var fileExtension = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            fileExtension = extension;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"grcfc_{Guid.NewGuid():N}{fileExtension}");
        await using var destination = File.Create(tempPath);
        await stream.CopyToAsync(destination).ConfigureAwait(false);

        return tempPath;
    }

    /// <summary>
    /// Displays a save file dialog and returns the chosen destination path.
    /// </summary>
    /// <param name="defaultFileName">Suggested file name shown to the user.</param>
    /// <param name="title">Dialog title shown to the user.</param>
    /// <param name="defaultExtension">Default file extension to append when the user omits one.</param>
    /// <param name="allowedPatterns">Optional filter patterns to restrict selectable files.</param>
    /// <param name="initialDirectory">Optional initial directory to open when the dialog starts.</param>
    /// <returns>The absolute path selected by the user, or <c>null</c> when the dialog is cancelled.</returns>
    public async Task<string?> SaveFileAsync(
        string defaultFileName,
        string title = "Save File",
        string defaultExtension = ".xlsx",
        string[]? allowedPatterns = null,
        string? initialDirectory = null)
    {
        var extension = NormalizeExtension(defaultExtension);
        var patterns = NormalizePatterns(extension, allowedPatterns);

        IStorageFolder? startLocation = null;
        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            try
            {
                var directoryPath = Path.GetFullPath(initialDirectory);
                if (Directory.Exists(directoryPath))
                {
                    if (!directoryPath.EndsWith(Path.DirectorySeparatorChar))
                    {
                        directoryPath += Path.DirectorySeparatorChar;
                    }

                    var folderUri = new Uri(directoryPath);
                    startLocation = await _target.StorageProvider.TryGetFolderFromPathAsync(folderUri).ConfigureAwait(false);
                }
            }
            catch
            {
                startLocation = null;
            }
        }

        var file = await _target.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = defaultFileName,
            Title = title,
            SuggestedStartLocation = startLocation,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(DefaultFilterLabel)
                {
                    Patterns = patterns
                }
            }
        }).ConfigureAwait(false);

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
        return trimmed.StartsWith('.')
            ? trimmed
            : string.Concat(".", trimmed.TrimStart('*', '.'));
    }

    private static string[] NormalizePatterns(string extension, string[]? allowedPatterns)
    {
        if (allowedPatterns is { Length: > 0 })
        {
            var requiresNormalization = false;
            for (var i = 0; i < allowedPatterns.Length; i++)
            {
                var pattern = allowedPatterns[i];
                if (string.IsNullOrWhiteSpace(pattern) || pattern![0] != '*')
                {
                    requiresNormalization = true;
                    break;
                }
            }

            if (!requiresNormalization)
            {
                return allowedPatterns;
            }

            var normalized = new string[allowedPatterns.Length];
            for (var i = 0; i < allowedPatterns.Length; i++)
            {
                var pattern = allowedPatterns[i] ?? string.Empty;
                normalized[i] = pattern.Length > 0 && pattern[0] == '*'
                    ? pattern
                    : "*" + pattern.TrimStart('.');
            }

            return normalized;
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
        return string.Equals(currentExtension, extension, StringComparison.OrdinalIgnoreCase)
            ? path
            : string.Concat(path, extension);
    }
}
