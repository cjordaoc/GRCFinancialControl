using System.Threading.Tasks;

namespace GRCFinancialControl.Avalonia.Services.Interfaces
{
    public interface IFilePickerService
    {
        Task<string?> OpenFileAsync();
        Task<string?> SaveFileAsync(string defaultFileName);
    }
}