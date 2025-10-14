using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class CustomerEditorViewModel : ViewModelBase
    {
        private readonly ICustomerService _customerService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _customerId = string.Empty;

        public Customer Customer { get; }

        public CustomerEditorViewModel(Customer customer, ICustomerService customerService, IMessenger messenger)
        {
            Customer = customer;
            _customerService = customerService;
            _messenger = messenger;

            Name = customer.Name;
            CustomerId = customer.CustomerID;
        }

        [RelayCommand]
        private async Task Save()
        {
            Customer.Name = Name;
            Customer.CustomerID = CustomerId;

            if (Customer.Id == 0)
            {
                await _customerService.AddAsync(Customer);
            }
            else
            {
                await _customerService.UpdateAsync(Customer);
            }

            _messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }
    }
}