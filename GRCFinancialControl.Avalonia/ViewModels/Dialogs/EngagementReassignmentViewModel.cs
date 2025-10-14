using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class EngagementReassignmentViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly ICustomerService _customerService;

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private string? _statusMessage;

        public EngagementReassignmentViewModel(IEngagementService engagementService, ICustomerService customerService, IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService;
            _customerService = customerService;
        }

        public override async Task LoadDataAsync()
        {
            var engagements = await _engagementService.GetAllAsync();
            Engagements = new ObservableCollection<Engagement>(engagements.OrderBy(e => e.EngagementId));

            var customers = await _customerService.GetAllAsync();
            Customers = new ObservableCollection<Customer>(customers.OrderBy(c => c.Name));
        }

        [RelayCommand(CanExecute = nameof(CanReassign))]
        private async Task ReassignAsync()
        {
            StatusMessage = null;

            if (SelectedEngagement is null || SelectedCustomer is null)
            {
                StatusMessage = "Select both an engagement and a customer.";
                return;
            }

            var engagement = await _engagementService.GetByIdAsync(SelectedEngagement.Id);
            if (engagement is null)
            {
                StatusMessage = "The selected engagement could not be loaded.";
                return;
            }

            engagement.CustomerId = SelectedCustomer.Id;
            engagement.CustomerKey = SelectedCustomer.Name;

            await _engagementService.UpdateAsync(engagement);

            Messenger.Send(new RefreshDataMessage());
            Messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanReassign() => SelectedEngagement is not null && SelectedCustomer is not null;

        partial void OnSelectedEngagementChanged(Engagement? value)
        {
            ReassignCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            ReassignCommand.NotifyCanExecuteChanged();
        }
    }
}
