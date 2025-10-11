using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ExceptionsViewModel : ViewModelBase
    {
        private readonly IExceptionService _exceptionService;

        [ObservableProperty]
        private ObservableCollection<ExceptionEntry> _exceptions = new();

        public ExceptionsViewModel(IExceptionService exceptionService)
        {
            _exceptionService = exceptionService;
            LoadExceptionsCommand = new AsyncRelayCommand(LoadExceptionsAsync);
        }

        public IAsyncRelayCommand LoadExceptionsCommand { get; }

        private async Task LoadExceptionsAsync()
        {
            Exceptions = new ObservableCollection<ExceptionEntry>(await _exceptionService.GetAllAsync());
        }
    }
}