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
    public partial class CustomersViewModel : ViewModelBase, IRecipient<RefreshDataMessage>
    {
        private readonly ICustomerService _customerService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private Customer? _selectedCustomer;

        public CustomersViewModel(ICustomerService customerService, IDialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _customerService = customerService;
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

        [RelayCommand]
        private async Task Edit(Customer customer)
        {
            if (customer == null) return;
            var editorViewModel = new CustomerEditorViewModel(customer, _customerService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand]
        private async Task Delete(Customer customer)
        {
            if (customer == null) return;
            await _customerService.DeleteAsync(customer.Id);
            Messenger.Send(new RefreshDataMessage());
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        public void Receive(RefreshDataMessage message)
        {
            _ = LoadDataAsync();
        }
    }
}
