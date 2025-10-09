using System.Collections.Generic;
using System.Threading.Tasks;

namespace GRCFinancialControl.Core.Services
{
    public sealed class FileDialogRequest
    {
        public string Title { get; init; } = string.Empty;
        public bool AllowMultiple { get; init; }
        public IReadOnlyList<FileFilter> Filters { get; init; } = [];
    }

    public sealed class SaveFileDialogRequest
    {
        public string Title { get; init; } = string.Empty;
        public string? SuggestedFileName { get; init; }
        public IReadOnlyList<FileFilter> Filters { get; init; } = [];
    }

    public sealed class FileFilter
    {
        public string Name { get; init; } = string.Empty;
        public IReadOnlyList<string> Extensions { get; init; } = [];
    }

    public interface IFileDialogService
    {
        Task<IReadOnlyList<string>> GetFiles(FileDialogRequest request);
        Task<string?> SaveFile(SaveFileDialogRequest request);
    }
}