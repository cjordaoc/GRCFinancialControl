using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class CustomersViewModel : ViewModelBase
    {
        private readonly ICustomerService _customerService;
        private readonly DialogService _dialogService;

        [ObservableProperty]
        private Customer? _selectedCustomer;

        public CustomersViewModel(ICustomerService customerService, DialogService dialogService, IMessenger messenger)
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

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task Edit(Customer customer)
        {
            if (customer == null) return;
            var editorViewModel = new CustomerEditorViewModel(customer, _customerService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task View(Customer customer)
        {
            if (customer == null) return;
            var editorViewModel = new CustomerEditorViewModel(customer, _customerService, Messenger, isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete(Customer customer)
        {
            if (customer == null) return;
            try
            {
                await _customerService.DeleteAsync(customer.Id);
                ToastService.ShowSuccess("Customers.Toast.DeleteSuccess", customer.Name);
                Messenger.Send(new RefreshDataMessage());
            }
            catch (InvalidOperationException ex)
            {
                ToastService.ShowWarning("Customers.Toast.OperationFailed", ex.Message);
            }
            catch (Exception ex)
            {
                ToastService.ShowError("Customers.Toast.OperationFailed", ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(Customer customer)
        {
            if (customer is null) return;

            var result = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("Common.Dialog.DeleteData.Title"),
                LocalizationRegistry.Format("Common.Dialog.DeleteData.Message", customer.Name));
            if (result)
            {
                try
                {
                    await _customerService.DeleteDataAsync(customer.Id);
                    ToastService.ShowSuccess("Customers.Toast.ReverseSuccess", customer.Name);
                    Messenger.Send(new RefreshDataMessage());
                }
                catch (InvalidOperationException ex)
                {
                    ToastService.ShowWarning("Customers.Toast.OperationFailed", ex.Message);
                }
                catch (Exception ex)
                {
                    ToastService.ShowError("Customers.Toast.OperationFailed", ex.Message);
                }
            }
        }

        private static bool CanEdit(Customer customer) => customer is not null;

        private static bool CanDelete(Customer customer) => customer is not null;

        private static bool CanDeleteData(Customer customer) => customer is not null;

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DeleteDataCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
        }

    }
}
