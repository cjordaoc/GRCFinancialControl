using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class CustomersViewModel : ViewModelBase
    {
        private readonly ICustomerService _customerService;
        private readonly IEngagementService _engagementService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private Customer? _selectedCustomer;

        public CustomersViewModel(ICustomerService customerService, IEngagementService engagementService, IDialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _customerService = customerService;
            _engagementService = engagementService;
            _dialogService = dialogService;
        }

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        public override async Task LoadDataAsync()
        {
            Customers = new ObservableCollection<Customer>(await _customerService.GetAllAsync());
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new CustomerEditorViewModel(new Customer(), _customerService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task Edit(Customer customer)
        {
            if (customer == null) return;
            var editorViewModel = new CustomerEditorViewModel(customer, _customerService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete(Customer customer)
        {
            if (customer == null) return;
            await _customerService.DeleteAsync(customer.Id);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(Customer customer)
        {
            if (customer is null) return;

            var result = await _dialogService.ShowConfirmationAsync("Delete Data", $"Are you sure you want to delete all data for {customer.Name}? This action cannot be undone.");
            if (result)
            {
                await _customerService.DeleteDataAsync(customer.Id);
                Messenger.Send(new RefreshDataMessage());
            }
        }

        [RelayCommand]
        private async Task ReassignEngagementAsync()
        {
            var reassignmentViewModel = new EngagementReassignmentViewModel(_engagementService, _customerService, Messenger);
            await reassignmentViewModel.LoadDataAsync();

            if (SelectedCustomer is not null)
            {
                reassignmentViewModel.SelectedCustomer = reassignmentViewModel.Customers.FirstOrDefault(c => c.Id == SelectedCustomer.Id)
                                                        ?? reassignmentViewModel.Customers.FirstOrDefault();
                if (reassignmentViewModel.SelectedCustomer is not null)
                {
                    reassignmentViewModel.SelectedEngagement = reassignmentViewModel.Engagements.FirstOrDefault(e => e.CustomerId == reassignmentViewModel.SelectedCustomer.Id)
                                                              ?? reassignmentViewModel.Engagements.FirstOrDefault();
                }
            }

            await _dialogService.ShowDialogAsync(reassignmentViewModel);
        }

        private static bool CanEdit(Customer customer) => customer is not null;

        private static bool CanDelete(Customer customer) => customer is not null;

        private static bool CanDeleteData(Customer customer) => customer is not null;

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
        }

    }
}
