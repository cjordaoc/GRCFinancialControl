using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Invoices.Core.Enums;
using App.Presentation.Services;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class EmissionConfirmationLineViewModel : ObservableObject
{
    private readonly RelayCommand _closeCommand;
    private readonly RelayCommand _cancelCommand;
    private EmissionConfirmationViewModel? _owner;
    private string? _currencyCode;

    public EmissionConfirmationLineViewModel()
    {
        _closeCommand = new RelayCommand(ExecuteClose, CanExecuteClose);
        _cancelCommand = new RelayCommand(ExecuteCancel, CanExecuteCancel);
    }

    public IRelayCommand CloseCommand => _closeCommand;

    public IRelayCommand CancelCommand => _cancelCommand;

    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private int sequence;

    [ObservableProperty]
    private decimal amount;

    [ObservableProperty]
    private DateTime? emissionDate;

    [ObservableProperty]
    private DateTime? dueDate;

    [ObservableProperty]
    private InvoiceItemStatus status;

    [ObservableProperty]
    private string? ritmNumber;

    [ObservableProperty]
    private string? bzCode;

    [ObservableProperty]
    private DateTime? emittedAt;

    [ObservableProperty]
    private string? cancelReason;

    [ObservableProperty]
    private string? lastCancellationReason;

    public bool IsRequested => Status == InvoiceItemStatus.Requested;

    public bool IsEmitted => Status == InvoiceItemStatus.Emitted;

    public bool IsCanceled => Status == InvoiceItemStatus.Canceled;

    public string AmountDisplay => CurrencyDisplayHelper.Format(Amount, _currencyCode);

    internal void Attach(EmissionConfirmationViewModel owner)
    {
        _owner = owner;
        NotifyCommandStates();
    }

    internal void Detach()
    {
        _owner = null;
        NotifyCommandStates();
    }

    partial void OnStatusChanged(InvoiceItemStatus value)
    {
        NotifyCommandStates();
        OnPropertyChanged(nameof(IsRequested));
        OnPropertyChanged(nameof(IsEmitted));
        OnPropertyChanged(nameof(IsCanceled));
        _owner?.RefreshSummaries();
    }

    partial void OnAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(AmountDisplay));
    }

    partial void OnBzCodeChanged(string? value) => NotifyCommandStates();

    partial void OnEmittedAtChanged(DateTime? value) => NotifyCommandStates();

    partial void OnCancelReasonChanged(string? value) => NotifyCommandStates();

    private bool CanExecuteClose()
    {
        return _owner is not null
            && Status == InvoiceItemStatus.Requested
            && !string.IsNullOrWhiteSpace(BzCode)
            && EmittedAt is not null;
    }

    private void ExecuteClose()
    {
        _owner?.HandleClose(this);
    }

    private bool CanExecuteCancel()
    {
        return _owner is not null
            && Status == InvoiceItemStatus.Emitted
            && !string.IsNullOrWhiteSpace(CancelReason);
    }

    private void ExecuteCancel()
    {
        _owner?.HandleCancel(this);
    }

    private void NotifyCommandStates()
    {
        _closeCommand.NotifyCanExecuteChanged();
        _cancelCommand.NotifyCanExecuteChanged();
    }

    internal void SetCurrency(string? currencyCode)
    {
        _currencyCode = string.IsNullOrWhiteSpace(currencyCode) ? null : currencyCode.Trim().ToUpperInvariant();
        OnPropertyChanged(nameof(AmountDisplay));
    }
}
