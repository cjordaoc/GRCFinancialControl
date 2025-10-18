using System.Threading.Tasks;

namespace App.Presentation.Services;

public interface IFilePickerService
{
    Task<string?> OpenFileAsync(string title = "Open File", string? defaultExtension = ".xlsx", string[]? allowedPatterns = null);
    Task<string?> SaveFileAsync(
        string defaultFileName,
        string title = "Save File",
        string defaultExtension = ".xlsx",
        string[]? allowedPatterns = null);
}