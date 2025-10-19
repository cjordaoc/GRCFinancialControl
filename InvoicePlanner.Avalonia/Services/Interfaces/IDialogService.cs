using System.Threading.Tasks;
using InvoicePlanner.Avalonia.ViewModels;

namespace InvoicePlanner.Avalonia.Services.Interfaces
{
    public interface IDialogService
    {
        Task<bool> ShowDialogAsync(ViewModelBase viewModel, string? title = null);
    }
}
