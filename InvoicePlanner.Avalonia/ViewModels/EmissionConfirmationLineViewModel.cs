using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Invoices.Core.Enums;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class EmissionConfirmationLineViewModel : ObservableObject
{
    private readonly RelayCommand _closeCommand;
    private readonly RelayCommand _cancelCommand;
    private EmissionConfirmationViewModel? _owner;

    public EmissionConfirmationLineViewModel()
    {
        _closeCommand = new RelayCommand(ExecuteClose, CanExecuteClose);
        _cancelCommand = new RelayCommand(ExecuteCancelAndReissue, CanExecuteCancelAndReissue);
    }

    public IRelayCommand CloseCommand => _closeCommand;

    public IRelayCommand CancelAndReissueCommand => _cancelCommand;

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
    private DateTime? reissueEmissionDate;

    public bool IsRequested => Status == InvoiceItemStatus.Requested;

    public bool IsClosed => Status == InvoiceItemStatus.Closed;

    public bool IsCanceled => Status == InvoiceItemStatus.Canceled;

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

    internal void ApplyClosedState(string bzCodeValue, DateTime emittedDate)
    {
        BzCode = bzCodeValue;
        EmittedAt = emittedDate;
        Status = InvoiceItemStatus.Closed;
    }

    partial void OnStatusChanged(InvoiceItemStatus value)
    {
        NotifyCommandStates();
        OnPropertyChanged(nameof(IsRequested));
        OnPropertyChanged(nameof(IsClosed));
        OnPropertyChanged(nameof(IsCanceled));
        _owner?.RefreshSummaries();
    }

    partial void OnBzCodeChanged(string? value) => NotifyCommandStates();

    partial void OnEmittedAtChanged(DateTime? value) => NotifyCommandStates();

    partial void OnCancelReasonChanged(string? value) => NotifyCommandStates();

    partial void OnReissueEmissionDateChanged(DateTime? value) => NotifyCommandStates();

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

    private bool CanExecuteCancelAndReissue()
    {
        return _owner is not null
            && Status == InvoiceItemStatus.Requested
            && !string.IsNullOrWhiteSpace(CancelReason)
            && ReissueEmissionDate is not null;
    }

    private void ExecuteCancelAndReissue()
    {
        _owner?.HandleCancelAndReissue(this);
    }

    private void NotifyCommandStates()
    {
        _closeCommand.NotifyCanExecuteChanged();
        _cancelCommand.NotifyCanExecuteChanged();
    }
}
