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
        private readonly IClosingPeriodService _closingPeriodService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        public EngagementsViewModel(IEngagementService engagementService, IPapdService papdService, ICustomerService customerService, IClosingPeriodService closingPeriodService, IDialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService;
            _papdService = papdService;
            _customerService = customerService;
            _closingPeriodService = closingPeriodService;
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
            var editorViewModel = new EngagementEditorViewModel(new Engagement(), _engagementService, _papdService, _customerService, _closingPeriodService, _dialogService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task Edit(Engagement engagement)
        {
            if (engagement == null) return;
            var editorViewModel = new EngagementEditorViewModel(engagement, _engagementService, _papdService, _customerService, _closingPeriodService, _dialogService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete(Engagement engagement)
        {
            if (engagement == null) return;
            await _engagementService.DeleteAsync(engagement.Id);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(Engagement engagement)
        {
            if (engagement is null) return;

            var result = await _dialogService.ShowConfirmationAsync("Delete Data", $"Are you sure you want to delete all data for {engagement.EngagementId}? This action cannot be undone.");
            if (result)
            {
                await _engagementService.DeleteDataAsync(engagement.Id);
                Messenger.Send(new RefreshDataMessage());
            }
        }

        private static bool CanEdit(Engagement engagement) => engagement is not null;

        private static bool CanDelete(Engagement engagement) => engagement is not null;

        private static bool CanDeleteData(Engagement engagement) => engagement is not null;

        partial void OnSelectedEngagementChanged(Engagement? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
        }

    }
}
