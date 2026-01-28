using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.UI.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;
using GRC.Shared.Core.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class CustomersViewModel : ViewModelBase
    {
        private readonly ICustomerService _customerService;
        private readonly DialogService _dialogService;
        private ObservableCollection<Customer> _allCustomers = new();

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private string _filterText = string.Empty;

        public CustomersViewModel(ICustomerService customerService, DialogService dialogService, IMessenger messenger)
            : base(messenger)
        {
            _customerService = customerService;
            _dialogService = dialogService;
        }

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        public bool HasCustomers => Customers.Count > 0;

        public override async Task LoadDataAsync()
        {
            _allCustomers = new ObservableCollection<Customer>(await _customerService.GetAllAsync());
            ApplyFilter();
        }

        partial void OnFilterTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                Customers = new ObservableCollection<Customer>(_allCustomers);
            }
            else
            {
                var filtered = _allCustomers
                    .Where(c => c.CustomerCode.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                             || (c.Name?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
                Customers = new ObservableCollection<Customer>(filtered);
            }
            OnPropertyChanged(nameof(HasCustomers));
        }

        [RelayCommand]
        private async Task Add()
        {
            var editorViewModel = new CustomerEditorViewModel(new Customer(), _customerService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task Edit(Customer customer)
        {
            if (customer == null)
            {
                return;
            }

            var editorViewModel = new CustomerEditorViewModel(customer, _customerService, Messenger);
            await _dialogService.ShowDialogAsync(editorViewModel);
            Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private async Task View(Customer customer)
        {
            if (customer == null)
            {
                return;
            }

            var editorViewModel = new CustomerEditorViewModel(customer, _customerService, Messenger, isReadOnlyMode: true);
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete(Customer customer)
        {
            if (customer == null)
            {
                return;
            }
            try
            {
                await _customerService.DeleteAsync(customer.Id);
                ToastService.ShowSuccess("FINC_Customers_Toast_DeleteSuccess", customer.Name);
                Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
            }
            catch (InvalidOperationException ex)
            {
                ToastService.ShowWarning("FINC_Customers_Toast_OperationFailed", ex.Message);
            }
            catch (Exception ex)
            {
                ToastService.ShowError("FINC_Customers_Toast_OperationFailed", ex.Message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteData))]
        private async Task DeleteData(Customer customer)
        {
            if (customer is null)
            {
                return;
            }

            var result = await _dialogService.ShowConfirmationAsync(
                LocalizationRegistry.Get("FINC_Dialog_DeleteData_Title"),
                LocalizationRegistry.Format("FINC_Dialog_DeleteData_Message", customer.Name));
            if (result)
            {
                try
                {
                    await _customerService.DeleteDataAsync(customer.Id);
                    ToastService.ShowSuccess("FINC_Customers_Toast_ReverseSuccess", customer.Name);
                    Messenger.Send(new RefreshViewMessage(RefreshTargets.FinancialData));
                }
                catch (InvalidOperationException ex)
                {
                    ToastService.ShowWarning("FINC_Customers_Toast_OperationFailed", ex.Message);
                }
                catch (Exception ex)
                {
                    ToastService.ShowError("FINC_Customers_Toast_OperationFailed", ex.Message);
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
