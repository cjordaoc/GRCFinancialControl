using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementsViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;
        private readonly ICustomerService _customerService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        public EngagementsViewModel(IEngagementService engagementService, IPapdService papdService, ICustomerService customerService, IDialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService;
            _papdService = papdService;
            _customerService = customerService;
            _dialogService = dialogService;
        }

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        public override async Task LoadDataAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementService.GetAllAsync());
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new EngagementEditorViewModel(new Engagement(), _engagementService, _papdService, _customerService, _dialogService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand]
        private async Task Edit(Engagement engagement)
        {
            if (engagement == null) return;
            var editorViewModel = new EngagementEditorViewModel(engagement, _engagementService, _papdService, _customerService, _dialogService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand]
        private async Task Delete(Engagement engagement)
        {
            if (engagement == null) return;
            await _engagementService.DeleteAsync(engagement.Id);
            Messenger.Send(new RefreshDataMessage());
        }

        partial void OnSelectedEngagementChanged(Engagement? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

    }
}
