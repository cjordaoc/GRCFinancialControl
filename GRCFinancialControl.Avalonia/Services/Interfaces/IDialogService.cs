using System.Threading.Tasks;
using GRCFinancialControl.Avalonia.ViewModels;

namespace GRCFinancialControl.Avalonia.Services.Interfaces
{
    public interface IDialogService
    {
        Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null);
        Task<bool> ShowConfirmationAsync(string title, string message);
    }
}
