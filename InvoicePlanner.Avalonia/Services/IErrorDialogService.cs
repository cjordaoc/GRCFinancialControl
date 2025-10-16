using System.Threading.Tasks;
using Avalonia.Controls;

namespace InvoicePlanner.Avalonia.Services;

public interface IErrorDialogService
{
    Task ShowErrorAsync(Window? owner, string details, string? message = null);
}
