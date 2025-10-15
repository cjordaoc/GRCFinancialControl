using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class CustomerEditorViewModel : DialogEditorViewModel<Customer>
    {
        private readonly ICustomerService _customerService;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _customerCode = string.Empty;

        public Customer Customer { get; }

        public CustomerEditorViewModel(Customer customer, ICustomerService customerService, IMessenger messenger)
            : base(messenger ?? throw new ArgumentNullException(nameof(messenger)))
        {
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));

            Name = customer.Name;
            CustomerCode = customer.CustomerCode;
        }

        protected override async Task PersistChangesAsync()
        {
            Customer.Name = Name;
            Customer.CustomerCode = CustomerCode;

            if (Customer.Id == 0)
            {
                await _customerService.AddAsync(Customer);
            }
            else
            {
                await _customerService.UpdateAsync(Customer);
            }
        }
    }
}