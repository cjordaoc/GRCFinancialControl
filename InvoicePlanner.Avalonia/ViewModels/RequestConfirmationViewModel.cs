using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Messages;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class RequestConfirmationViewModel : ViewModelBase
{
    private readonly IInvoicePlanRepository _repository;
    private readonly ILogger<RequestConfirmationViewModel> _logger;
    private readonly IInvoiceAccessScope _accessScope;
    private readonly RelayCommand _loadPlanCommand;
    private readonly RelayCommand _savePlanDetailsCommand;
    private readonly RelayCommand _reverseSelectedLineCommand;
    private readonly RelayCommand _closePlanDetailsCommand;
    private RequestConfirmationLineViewModel? _selectedLineSubscription;
    private string? _currentCurrencyCode;

    public RequestConfirmationViewModel(
        IInvoicePlanRepository repository,
        ILogger<RequestConfirmationViewModel> logger,
        IInvoiceAccessScope accessScope,
        IMessenger messenger)
        : base(messenger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accessScope = accessScope ?? throw new ArgumentNullException(nameof(accessScope));

        _accessScope.EnsureInitialized();

        Lines.CollectionChanged += OnLinesCollectionChanged;
        AvailablePlans.CollectionChanged += OnAvailablePlansChanged;

        _loadPlanCommand = new RelayCommand(LoadSelectedPlan, () => SelectedPlan is not null);
        _savePlanDetailsCommand = new RelayCommand(SavePlanDetails, CanSaveSelectedLine);
        _reverseSelectedLineCommand = new RelayCommand(ReverseSelectedLine, CanReverseSelectedLine);
        _closePlanDetailsCommand = new RelayCommand(ClosePlanDetails, () => IsPlanDetailsVisible);
        ClosePlanDetailsCommand = _closePlanDetailsCommand;
        RefreshCommand = new RelayCommand(() =>
        {
            ResetMessages();
            LoadAvailablePlans();
        });

        Messenger.Register<ConnectionSettingsImportedMessage>(this, (_, _) => LoadAvailablePlans());

        LoadAvailablePlans();
    }

    public ObservableCollection<RequestConfirmationLineViewModel> Lines { get; } = new();

    public ObservableCollection<InvoicePlanSummaryViewModel> AvailablePlans { get; } = new();

    [ObservableProperty]
    private InvoicePlanSummaryViewModel? selectedPlan;

    [ObservableProperty]
    private int currentPlanId;

    [ObservableProperty]
    private string engagementId = string.Empty;

    [ObservableProperty]
    private bool isPlanDetailsVisible;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string? validationMessage;

    [ObservableProperty]
    private int plannedCount;

    [ObservableProperty]
    private int requestedCount;

    [ObservableProperty]
    private string? planSelectionMessage;

    [ObservableProperty]
    private RequestConfirmationLineViewModel? selectedLine;

    public bool HasLines => Lines.Count > 0;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasAvailablePlans => AvailablePlans.Count > 0;

    public bool HasSelectedLine => SelectedLine is not null;

    public string EngagementDisplay => LocalizationRegistry.Format(
        "Request.Status.EngagementFormat",
        string.IsNullOrWhiteSpace(EngagementId) ? string.Empty : EngagementId);

    public string PlannedCountDisplay => LocalizationRegistry.Format(
        "Request.Status.PlannedFormat",
        PlannedCount);

    public string RequestedCountDisplay => LocalizationRegistry.Format(
        "Request.Status.RequestedFormat",
        RequestedCount);

    public IRelayCommand LoadPlanCommand => _loadPlanCommand;

    public IRelayCommand ClosePlanDetailsCommand { get; }

    public IRelayCommand SavePlanDetailsCommand => _savePlanDetailsCommand;

    public IRelayCommand ReverseSelectedLineCommand => _reverseSelectedLineCommand;

    public IRelayCommand RefreshCommand { get; }

    partial void OnValidationMessageChanged(string? value) => OnPropertyChanged(nameof(HasValidationMessage));

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnEngagementIdChanged(string value) => OnPropertyChanged(nameof(EngagementDisplay));

    partial void OnPlannedCountChanged(int value) => OnPropertyChanged(nameof(PlannedCountDisplay));

    partial void OnRequestedCountChanged(int value) => OnPropertyChanged(nameof(RequestedCountDisplay));

    partial void OnSelectedPlanChanged(InvoicePlanSummaryViewModel? value)
    {
        _loadPlanCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPlanDetailsVisibleChanged(bool value)
    {
        _closePlanDetailsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedLineChanged(RequestConfirmationLineViewModel? value)
    {
        if (_selectedLineSubscription is not null)
        {
            _selectedLineSubscription.PropertyChanged -= OnSelectedLinePropertyChanged;
        }

        _selectedLineSubscription = value;

        if (_selectedLineSubscription is not null)
        {
            _selectedLineSubscription.PropertyChanged += OnSelectedLinePropertyChanged;
        }

        OnPropertyChanged(nameof(HasSelectedLine));
        RefreshActionCommands();
    }

    private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (RequestConfirmationLineViewModel line in e.OldItems)
            {
                line.Detach();
            }
        }

        if (e.NewItems is not null)
        {
            foreach (RequestConfirmationLineViewModel line in e.NewItems)
            {
                line.Attach(this);
            }
        }

        OnPropertyChanged(nameof(HasLines));
        RefreshSummaries();
        EnsureSelectedLineIsValid();
        RefreshActionCommands();
    }

    private void OnAvailablePlansChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAvailablePlans));
    }

    private void LoadSelectedPlan()
    {
        ResetMessages();

        if (SelectedPlan is null)
        {
            ValidationMessage = LocalizationRegistry.Get("Request.Validation.PlanSelection");
            return;
        }

        try
        {
            var plan = _repository.GetPlan(SelectedPlan.Id);
            if (plan is null)
            {
                ClearLines();
                _currentCurrencyCode = null;
                EngagementId = string.Empty;
                CurrentPlanId = 0;
                ValidationMessage = LocalizationRegistry.Format("Request.Validation.PlanNotFound", SelectedPlan.Id);
                return;
            }

            CurrentPlanId = plan.Id;
            EngagementId = plan.EngagementId;
            _currentCurrencyCode = _repository.GetEngagementCurrency(plan.EngagementId);

            ClearLines();

            foreach (var item in plan.Items.OrderBy(item => item.SeqNo))
            {
                var line = new RequestConfirmationLineViewModel
                {
                    Id = item.Id,
                    Sequence = item.SeqNo,
                    Amount = item.Amount,
                    EmissionDate = item.EmissionDate,
                    Status = item.Status,
                    RitmNumber = string.IsNullOrWhiteSpace(item.RitmNumber) ? string.Empty : item.RitmNumber,
                    CoeResponsible = string.IsNullOrWhiteSpace(item.CoeResponsible) ? string.Empty : item.CoeResponsible,
                    RequestDate = item.RequestDate ?? DateTime.Today,
                };

                line.SetCurrency(_currentCurrencyCode);
                Lines.Add(line);
            }

            SelectedPlan.UpdateCounts(
                plan.Items.Count(item => item.Status == InvoiceItemStatus.Planned),
                plan.Items.Count(item => item.Status == InvoiceItemStatus.Requested),
                plan.Items.Count(item => item.Status == InvoiceItemStatus.Emitted),
                plan.Items.Count(item => item.Status == InvoiceItemStatus.Closed),
                plan.Items.Count(item => item.Status == InvoiceItemStatus.Canceled));

            RefreshSummaries();

            StatusMessage = LocalizationRegistry.Format("Request.Status.PlanLoaded", plan.Id, Lines.Count);

            if (Lines.Count > 0)
            {
                SelectedLine = Lines.FirstOrDefault();
                IsPlanDetailsVisible = true;
            }
            else
            {
                IsPlanDetailsVisible = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load invoice plan {PlanId}.", SelectedPlan.Id);
            ValidationMessage = LocalizationRegistry.Format("Request.Status.LoadFailureDetail", ex.Message);
        }
    }

    internal void HandleRequest(RequestConfirmationLineViewModel line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        ResetMessages();

        if (CurrentPlanId <= 0)
        {
            ValidationMessage = LocalizationRegistry.Get("Request.Validation.PlanRequired");
            return;
        }

        if (line.RequestDate is null)
        {
            ValidationMessage = LocalizationRegistry.Get("Request.Validation.RequestDateRequired");
            return;
        }

        var update = new InvoiceRequestUpdate
        {
            ItemId = line.Id,
            RitmNumber = (line.RitmNumber ?? string.Empty).Trim(),
            CoeResponsible = (line.CoeResponsible ?? string.Empty).Trim(),
            RequestDate = line.RequestDate.Value,
        };

        try
        {
            var result = _repository.MarkItemsAsRequested(CurrentPlanId, new[] { update });

            if (result.Updated == 0)
            {
                StatusMessage = LocalizationRegistry.Get("Request.Status.NoUpdates");
                ToastService.ShowWarning("Request.Toast.NoUpdates");
                return;
            }

            line.ApplyRequestedState(update.RitmNumber, update.CoeResponsible, update.RequestDate.Date);

            StatusMessage = LocalizationRegistry.Format("Request.Status.LineRequested", line.Sequence);
            ToastService.ShowSuccess("Request.Toast.LineRequested", line.Sequence);
            RefreshActionCommands();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request invoice item {ItemId}.", line.Id);
            ValidationMessage = ex.Message;
            ToastService.ShowError("Request.Toast.RequestFailed");
        }
    }

    internal void HandleUndo(RequestConfirmationLineViewModel line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        ResetMessages();

        if (CurrentPlanId <= 0)
        {
            ValidationMessage = LocalizationRegistry.Get("Request.Validation.UndoPlanRequired");
            return;
        }

        try
        {
            var result = _repository.UndoRequest(CurrentPlanId, new[] { line.Id });

            if (result.Updated == 0)
            {
                StatusMessage = LocalizationRegistry.Get("Request.Status.NoUndo");
                ToastService.ShowWarning("Request.Toast.NoUndo");
                return;
            }

            line.ResetToPlanned(DateTime.Today);

            StatusMessage = LocalizationRegistry.Format("Request.Status.LineUndone", line.Sequence);
            ToastService.ShowSuccess("Request.Toast.LineUndone", line.Sequence);
            RefreshActionCommands();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to undo invoice item {ItemId} request.", line.Id);
            ValidationMessage = ex.Message;
            ToastService.ShowError("Request.Toast.UndoFailed");
        }
    }

    internal void RefreshSummaries()
    {
        PlannedCount = Lines.Count(line => line.Status == InvoiceItemStatus.Planned);
        RequestedCount = Lines.Count(line => line.Status == InvoiceItemStatus.Requested);
        UpdateSelectedPlanSummaryCounts();
    }

    private void ResetMessages()
    {
        ValidationMessage = null;
        StatusMessage = null;
    }

    private void ClearLines()
    {
        foreach (var line in Lines.ToList())
        {
            line.Detach();
        }

        Lines.Clear();
        SelectedLine = null;
        IsPlanDetailsVisible = false;
        _currentCurrencyCode = null;
        RefreshSummaries();
        OnPropertyChanged(nameof(HasLines));
    }

    private void LoadAvailablePlans()
    {
        var previouslySelectedId = SelectedPlan?.Id;

        try
        {
            var plans = _repository.ListPlansForRequestStage();

            AvailablePlans.Clear();

            foreach (var summary in plans)
            {
                AvailablePlans.Add(InvoicePlanSummaryViewModel.FromSummary(summary));
            }

            PlanSelectionMessage = plans.Count == 0
                ? LocalizationRegistry.Get("Request.Message.Empty")
                : LocalizationRegistry.Get("Request.Message.SelectHint");

            if (_accessScope.IsInitialized && !_accessScope.HasAssignments && string.IsNullOrWhiteSpace(_accessScope.InitializationError))
            {
                PlanSelectionMessage = LocalizationRegistry.Format("Access.Message.NoAssignments", GetLoginDisplay(_accessScope));
            }

            SelectedPlan = AvailablePlans.FirstOrDefault(plan => plan.Id == previouslySelectedId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load available plans for request confirmation.");
            AvailablePlans.Clear();
            SelectedPlan = null;
            PlanSelectionMessage = LocalizationRegistry.Format("Request.Status.LoadFailure", ex.Message);
        }
    }

    private void UpdateSelectedPlanSummaryCounts()
    {
        if (SelectedPlan is null || SelectedPlan.Id != CurrentPlanId)
        {
            return;
        }

        var emitted = Lines.Count(line => line.Status == InvoiceItemStatus.Emitted);
        var closed = Lines.Count(line => line.Status == InvoiceItemStatus.Closed);
        var canceled = Lines.Count(line => line.Status == InvoiceItemStatus.Canceled);

        SelectedPlan.UpdateCounts(PlannedCount, RequestedCount, emitted, closed, canceled);
    }

    private void SavePlanDetails()
    {
        if (SelectedLine is null)
        {
            ValidationMessage = LocalizationRegistry.Get("Request.Validation.LineSelection");
            return;
        }

        HandleRequest(SelectedLine);
    }

    private bool CanSaveSelectedLine()
    {
        return SelectedLine is not null
            && SelectedLine.Status == InvoiceItemStatus.Planned
            && !string.IsNullOrWhiteSpace(SelectedLine.RitmNumber)
            && !string.IsNullOrWhiteSpace(SelectedLine.CoeResponsible)
            && SelectedLine.RequestDate is not null;
    }

    private void ReverseSelectedLine()
    {
        if (SelectedLine is null)
        {
            ValidationMessage = LocalizationRegistry.Get("Request.Validation.LineSelection");
            return;
        }

        HandleUndo(SelectedLine);
    }

    private bool CanReverseSelectedLine()
    {
        return SelectedLine is not null && SelectedLine.Status == InvoiceItemStatus.Requested;
    }

    private void ClosePlanDetails()
    {
        ResetMessages();
        ClearLines();
        CurrentPlanId = 0;
        EngagementId = string.Empty;
    }

    private void EnsureSelectedLineIsValid()
    {
        if (SelectedLine is not null && !Lines.Contains(SelectedLine))
        {
            SelectedLine = null;
        }
        else if (SelectedLine is null && Lines.Count > 0)
        {
            SelectedLine = Lines.FirstOrDefault();
        }
    }

    private void RefreshActionCommands()
    {
        _savePlanDetailsCommand.NotifyCanExecuteChanged();
        _reverseSelectedLineCommand.NotifyCanExecuteChanged();
    }

    private void OnSelectedLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshActionCommands();
    }
}
