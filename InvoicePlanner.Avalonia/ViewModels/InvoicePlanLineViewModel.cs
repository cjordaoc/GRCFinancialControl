using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Invoices.Core.Enums;

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
    private decimal percentage;

    [ObservableProperty]
    private decimal amount;

    [ObservableProperty]
    private bool canEditEmissionDate = true;

    [ObservableProperty]
    private bool showDeliveryDescription;

    [ObservableProperty]
    private InvoiceItemStatus status = InvoiceItemStatus.Planned;

    public bool IsEditable => Status == InvoiceItemStatus.Planned;

    internal void Attach(PlanEditorViewModel owner)
    {
        _owner = owner;
    }

    internal void Detach()
    {
        _owner = null;
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
    }

    internal void SetPercentage(decimal value)
    {
        _suppressNotifications = true;
        Percentage = value;
        _suppressNotifications = false;
    }

    internal void SetAmount(decimal value)
    {
        _suppressNotifications = true;
        Amount = value;
        _suppressNotifications = false;
    }

    internal void SetEmissionDate(DateTime value)
    {
        _suppressNotifications = true;
        EmissionDate = value;
        _suppressNotifications = false;
    }

    partial void OnStatusChanged(InvoiceItemStatus value)
    {
        OnPropertyChanged(nameof(IsEditable));
    }
}
