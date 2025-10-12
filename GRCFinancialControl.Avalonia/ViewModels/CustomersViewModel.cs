using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class CustomersViewModel : ViewModelBase
    {
        private readonly ICustomerService _customerService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        [ObservableProperty]
        private Customer? _selectedCustomer;

        public CustomersViewModel(ICustomerService customerService, IMessenger messenger)
        {
            _customerService = customerService;
            _messenger = messenger;

            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit, () => SelectedCustomer != null);
            DeleteCommand = new AsyncRelayCommand(Delete, () => SelectedCustomer != null);
        }

        public IRelayCommand AddCommand { get; }
        public IRelayCommand EditCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }

        public override async Task LoadDataAsync()
        {
            Customers = new ObservableCollection<Customer>(await _customerService.GetAllAsync());
        }

        private void Add()
        {
            var editorViewModel = new CustomerEditorViewModel(new Customer(), _customerService, _messenger);
            _messenger.Send(new OpenDialogMessage(editorViewModel));
        }

        private void Edit()
        {
            if (SelectedCustomer == null) return;
            var editorViewModel = new CustomerEditorViewModel(SelectedCustomer, _customerService, _messenger);
            _messenger.Send(new OpenDialogMessage(editorViewModel));
        }

        private async Task Delete()
        {
            if (SelectedCustomer == null) return;
            await _customerService.DeleteAsync(SelectedCustomer.Id);
            await LoadDataAsync();
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (EditCommand is RelayCommand editCommand)
            {
                editCommand.NotifyCanExecuteChanged();
            }

            if (DeleteCommand is AsyncRelayCommand deleteCommand)
            {
                deleteCommand.NotifyCanExecuteChanged();
            }
        }
    }
}
