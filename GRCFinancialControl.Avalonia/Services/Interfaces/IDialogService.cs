using System.Threading.Tasks;
using App.Presentation.Controls;
using GRCFinancialControl.Avalonia.ViewModels;

namespace GRCFinancialControl.Avalonia.Services.Interfaces
{
    public interface IDialogService
    {
        Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null, bool canClose = true);
        Task<bool> ShowConfirmationAsync(string title, string message);
        void AttachHost(IModalOverlayHost host);
    }
}
