using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using App.Presentation.Services;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class CustomerEditorViewModel : DialogEditorViewModel<Customer>
    {
        private readonly ICustomerService _customerService;

        private const string NameRequiredMessage = "Name is required.";
        private const string CustomerCodeRequiredMessage = "Customer Code is required.";

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = NameRequiredMessage)]
        private string _name = string.Empty;

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = CustomerCodeRequiredMessage)]
        private string _customerCode = string.Empty;

        public Customer Customer { get; }

        public CustomerEditorViewModel(
            Customer customer,
            ICustomerService customerService,
            IMessenger messenger,
            bool isReadOnlyMode = false)
            : base(messenger ?? throw new ArgumentNullException(nameof(messenger)), isReadOnlyMode)
        {
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));

            ErrorsChanged += OnValidationErrorsChanged;

            Name = customer.Name;
            CustomerCode = customer.CustomerCode;

            OnPropertyChanged(nameof(NameError));
            OnPropertyChanged(nameof(CustomerCodeError));
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

        protected override void OnSaveSucceeded()
        {
            ToastService.ShowSuccess("FINC_Customers_Toast_SaveSuccess", Customer.Name);
        }

        protected override void OnSaveFailed(Exception exception)
        {
            ToastService.ShowError("FINC_Customers_Toast_OperationFailed", exception.Message);
        }

        public string? NameError => GetErrors(nameof(Name)).FirstOrDefault()?.ErrorMessage;

        public string? CustomerCodeError => GetErrors(nameof(CustomerCode)).FirstOrDefault()?.ErrorMessage;

        private void OnValidationErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(Name))
            {
                OnPropertyChanged(nameof(NameError));
            }

            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(CustomerCode))
            {
                OnPropertyChanged(nameof(CustomerCodeError));
            }
        }
    }
}
