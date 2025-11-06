using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Invoices.Core.Enums;
using Invoices.Core.Payments;
using App.Presentation.Services;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class InvoicePlanLineViewModel : ObservableObject
{
    private bool _suppressNotifications;
    private PlanEditorViewModel? _owner;

    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private int sequence;

    [ObservableProperty]
    private DateTime? emissionDate = DateTime.Today;

    [ObservableProperty]
    private string? deliveryDescription;

    [ObservableProperty]
    private string payerCnpj = string.Empty;

    [ObservableProperty]
    private string? poNumber;

    [ObservableProperty]
    private string? frsNumber;

    [ObservableProperty]
    private string? customerTicket;

    [ObservableProperty]
    private DateTime? dueDate;

    [ObservableProperty]
    private decimal percentage;

    [ObservableProperty]
    private decimal amount;

    [ObservableProperty]
    private string paymentTypeCode = PaymentTypeCatalog.TransferenciaBancariaCode;

    [ObservableProperty]
    private bool canEditEmissionDate = true;

    [ObservableProperty]
    private bool showDeliveryDescription;

    [ObservableProperty]
    private InvoiceItemStatus status = InvoiceItemStatus.Planned;

    [ObservableProperty]
    private string? additionalDetails;

    public bool IsEditable => Status == InvoiceItemStatus.Planned;

    public bool ShowReadOnlyAmount => !IsEditable;

    public PlanEditorViewModel? Owner => _owner;

    public string CurrencySymbol => _owner?.CurrencySymbol ?? string.Empty;

    public bool HasCurrencySymbol => _owner?.HasCurrencySymbol ?? false;

    public string AmountDisplay => _owner?.FormatAmount(Amount) ?? CurrencyDisplayHelper.Format(Amount, null);

    public PaymentTypeOption SelectedPaymentTypeOption
    {
        get => PaymentTypeCatalog.GetByCode(PaymentTypeCode);
        set
        {
            var resolvedCode = value is null
                ? PaymentTypeCatalog.TransferenciaBancariaCode
                : PaymentTypeCatalog.NormalizeCode(value.Code);

            if (!string.Equals(PaymentTypeCode, resolvedCode, StringComparison.Ordinal))
            {
                PaymentTypeCode = resolvedCode;
            }
        }
    }

    internal void Attach(PlanEditorViewModel owner)
    {
        if (_owner is not null)
        {
            _owner.PropertyChanged -= OnOwnerPropertyChanged;
        }

        _owner = owner;
        _owner.PropertyChanged += OnOwnerPropertyChanged;
        OnPropertyChanged(nameof(Owner));
        OnPropertyChanged(nameof(CurrencySymbol));
        OnPropertyChanged(nameof(HasCurrencySymbol));
        OnPropertyChanged(nameof(AmountDisplay));
    }

    internal void Detach()
    {
        if (_owner is not null)
        {
            _owner.PropertyChanged -= OnOwnerPropertyChanged;
        }

        _owner = null;
        OnPropertyChanged(nameof(Owner));
        OnPropertyChanged(nameof(CurrencySymbol));
        OnPropertyChanged(nameof(HasCurrencySymbol));
        OnPropertyChanged(nameof(AmountDisplay));
    }

    partial void OnPercentageChanged(decimal value)
    {
        if (_suppressNotifications)
        {
            return;
        }

        _owner?.HandleLinePercentageChanged(this);
    }

    partial void OnAmountChanged(decimal value)
    {
        if (_suppressNotifications)
        {
            return;
        }

        _owner?.HandleLineAmountChanged(this);
        OnPropertyChanged(nameof(AmountDisplay));
    }

    partial void OnEmissionDateChanged(DateTime? value)
    {
        if (_suppressNotifications)
        {
            return;
        }

        _owner?.HandleLineEmissionChanged(this);
    }

    internal void SetPercentage(decimal value)
    {
        _suppressNotifications = true;
        Percentage = Math.Round(value, 4, MidpointRounding.AwayFromZero);
        _suppressNotifications = false;
    }

    internal void SetAmount(decimal value)
    {
        _suppressNotifications = true;
        Amount = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        _suppressNotifications = false;
    }

    internal void SetPaymentType(string? code)
    {
        _suppressNotifications = true;
        PaymentTypeCode = PaymentTypeCatalog.NormalizeCode(code);
        _suppressNotifications = false;
    }

    internal void SetEmissionDate(DateTime value)
    {
        _suppressNotifications = true;
        EmissionDate = value;
        _suppressNotifications = false;
    }

    internal void SetDueDate(DateTime? value)
    {
        _suppressNotifications = true;
        DueDate = value;
        _suppressNotifications = false;
    }

    partial void OnStatusChanged(InvoiceItemStatus value)
    {
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(ShowReadOnlyAmount));
    }

    partial void OnPaymentTypeCodeChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedPaymentTypeOption));
    }

    private void OnOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(PlanEditorViewModel.CurrencySymbol))
        {
            OnPropertyChanged(nameof(CurrencySymbol));
            OnPropertyChanged(nameof(HasCurrencySymbol));
            OnPropertyChanged(nameof(AmountDisplay));
        }
    }

    internal void NotifyCurrencyChanged()
    {
        OnPropertyChanged(nameof(CurrencySymbol));
        OnPropertyChanged(nameof(HasCurrencySymbol));
        OnPropertyChanged(nameof(AmountDisplay));
    }
}
