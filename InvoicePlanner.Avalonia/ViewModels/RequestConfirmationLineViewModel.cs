using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Invoices.Core.Enums;
using App.Presentation.Services;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class RequestConfirmationLineViewModel : ObservableObject
{
    private readonly RelayCommand _requestCommand;
    private readonly RelayCommand _undoCommand;
    private RequestConfirmationViewModel? _owner;
    private string? _currencyCode;

    public RequestConfirmationLineViewModel()
    {
        _requestCommand = new RelayCommand(ExecuteRequest, CanExecuteRequest);
        _undoCommand = new RelayCommand(ExecuteUndo, CanExecuteUndo);
    }

    public IRelayCommand RequestCommand => _requestCommand;

    public IRelayCommand UndoCommand => _undoCommand;

    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private int sequence;

    [ObservableProperty]
    private decimal amount;

    [ObservableProperty]
    private DateTime? emissionDate;

    [ObservableProperty]
    private InvoiceItemStatus status;

    [ObservableProperty]
    private string? ritmNumber;

    [ObservableProperty]
    private string? coeResponsible;

    [ObservableProperty]
    private DateTime? requestDate;

    public InvoiceItemStatus[] StatusOptions { get; } =
    {
        InvoiceItemStatus.Planned,
        InvoiceItemStatus.Requested,
        InvoiceItemStatus.Closed,
        InvoiceItemStatus.Canceled
    };

    public bool IsPlanned => Status == InvoiceItemStatus.Planned;

    public bool IsRequested => Status == InvoiceItemStatus.Requested;

    public string AmountDisplay => CurrencyDisplayHelper.Format(Amount, _currencyCode);

    internal void Attach(RequestConfirmationViewModel owner)
    {
        _owner = owner;
        NotifyCommandStates();
    }

    internal void Detach()
    {
        _owner = null;
        NotifyCommandStates();
    }

    internal void ApplyRequestedState(string ritmNumber, string coeResponsible, DateTime requestDate)
    {
        RitmNumber = ritmNumber;
        CoeResponsible = coeResponsible;
        RequestDate = requestDate;
        Status = InvoiceItemStatus.Requested;
    }

    internal void ResetToPlanned(DateTime? suggestedDate)
    {
        RitmNumber = string.Empty;
        CoeResponsible = string.Empty;
        RequestDate = suggestedDate;
        Status = InvoiceItemStatus.Planned;
    }

    partial void OnStatusChanged(InvoiceItemStatus value)
    {
        NotifyCommandStates();
        OnPropertyChanged(nameof(IsPlanned));
        OnPropertyChanged(nameof(IsRequested));
        _owner?.RefreshSummaries();
    }

    partial void OnAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(AmountDisplay));
    }

    partial void OnRitmNumberChanged(string? value) => NotifyCommandStates();

    partial void OnCoeResponsibleChanged(string? value) => NotifyCommandStates();

    partial void OnRequestDateChanged(DateTime? value) => NotifyCommandStates();

    private bool CanExecuteRequest()
    {
        return _owner is not null
            && Status == InvoiceItemStatus.Planned
            && !string.IsNullOrWhiteSpace(RitmNumber)
            && !string.IsNullOrWhiteSpace(CoeResponsible)
            && RequestDate is not null;
    }

    private void ExecuteRequest()
    {
        _owner?.HandleRequest(this);
    }

    private bool CanExecuteUndo()
    {
        return _owner is not null && Status == InvoiceItemStatus.Requested;
    }

    private void ExecuteUndo()
    {
        _owner?.HandleUndo(this);
    }

    private void NotifyCommandStates()
    {
        _requestCommand.NotifyCanExecuteChanged();
        _undoCommand.NotifyCanExecuteChanged();
    }

    internal void SetCurrency(string? currencyCode)
    {
        _currencyCode = string.IsNullOrWhiteSpace(currencyCode) ? null : currencyCode.Trim().ToUpperInvariant();
        OnPropertyChanged(nameof(AmountDisplay));
    }
}
