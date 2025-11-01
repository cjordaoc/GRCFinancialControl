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

public partial class EmissionConfirmationViewModel : ViewModelBase
{
    private readonly IInvoicePlanRepository _repository;
    private readonly ILogger<EmissionConfirmationViewModel> _logger;
    private readonly IInvoiceAccessScope _accessScope;
    private readonly RelayCommand _loadPlanCommand;
    private readonly RelayCommand _saveSelectedLineCommand;
    private readonly RelayCommand _cancelSelectedLineCommand;
    private readonly RelayCommand _closePlanDetailsCommand;
    private EmissionConfirmationLineViewModel? _selectedLineSubscription;
    private string? _currentCurrencyCode;

    public EmissionConfirmationViewModel(
        IInvoicePlanRepository repository,
        ILogger<EmissionConfirmationViewModel> logger,
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
        _saveSelectedLineCommand = new RelayCommand(SaveSelectedLine, CanSaveSelectedLine);
        _cancelSelectedLineCommand = new RelayCommand(CancelSelectedLine, CanCancelSelectedLine);
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

    public ObservableCollection<EmissionConfirmationLineViewModel> Lines { get; } = new();

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
    private int requestedCount;

    [ObservableProperty]
    private int emittedCount;

    [ObservableProperty]
    private int canceledCount;

    [ObservableProperty]
    private string? planSelectionMessage;

    [ObservableProperty]
    private EmissionConfirmationLineViewModel? selectedLine;

    public bool HasLines => Lines.Count > 0;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasAvailablePlans => AvailablePlans.Count > 0;

    public bool HasSelectedLine => SelectedLine is not null;

    public string EngagementDisplay => LocalizationRegistry.Format(
        "INV_Emission_Status_EngagementFormat",
        string.IsNullOrWhiteSpace(EngagementId) ? string.Empty : EngagementId);

    public string RequestedCountDisplay => LocalizationRegistry.Format(
        "INV_Emission_Status_RequestedFormat",
        RequestedCount);

    public string EmittedCountDisplay => LocalizationRegistry.Format(
        "INV_Emission_Status_EmittedFormat",
        EmittedCount);

    public string CanceledCountDisplay => LocalizationRegistry.Format(
        "INV_Emission_Status_CanceledFormat",
        CanceledCount);

    public IRelayCommand LoadPlanCommand => _loadPlanCommand;

    public IRelayCommand ClosePlanDetailsCommand { get; }

    public IRelayCommand SavePlanDetailsCommand => _saveSelectedLineCommand;

    public IRelayCommand CancelSelectedLineCommand => _cancelSelectedLineCommand;

    public IRelayCommand RefreshCommand { get; }

    partial void OnValidationMessageChanged(string? value) => OnPropertyChanged(nameof(HasValidationMessage));

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnEngagementIdChanged(string value) => OnPropertyChanged(nameof(EngagementDisplay));

    partial void OnRequestedCountChanged(int value) => OnPropertyChanged(nameof(RequestedCountDisplay));

    partial void OnEmittedCountChanged(int value) => OnPropertyChanged(nameof(EmittedCountDisplay));

    partial void OnCanceledCountChanged(int value) => OnPropertyChanged(nameof(CanceledCountDisplay));

    partial void OnIsPlanDetailsVisibleChanged(bool value)
    {
        _closePlanDetailsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPlanChanged(InvoicePlanSummaryViewModel? value)
    {
        _loadPlanCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedLineChanged(EmissionConfirmationLineViewModel? value)
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
            foreach (EmissionConfirmationLineViewModel line in e.OldItems)
            {
                line.Detach();
            }
        }

        if (e.NewItems is not null)
        {
            foreach (EmissionConfirmationLineViewModel line in e.NewItems)
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
        if (SelectedPlan is null)
        {
            ValidationMessage = LocalizationRegistry.Get("INV_Emission_Validation_PlanSelection");
            return;
        }

        LoadPlanById(SelectedPlan.Id, suppressStatusMessage: false);
    }

    private void LoadPlanById(int planId, bool suppressStatusMessage)
    {
        ValidationMessage = null;
        ResetMessages();

        try
        {
            var plan = _repository.GetPlan(planId);
            if (plan is null)
            {
                ClearLines();
                _currentCurrencyCode = null;
                EngagementId = string.Empty;
                CurrentPlanId = 0;
                ValidationMessage = LocalizationRegistry.Format("INV_Emission_Validation_PlanNotFound", planId);
                return;
            }

            CurrentPlanId = plan.Id;
            EngagementId = plan.EngagementId;
            _currentCurrencyCode = _repository.GetEngagementCurrency(plan.EngagementId);

            ClearLines();

            foreach (var item in plan.Items
                         .Where(item => item.Status != InvoiceItemStatus.Planned)
                         .OrderBy(item => item.SeqNo))
            {
                var defaultEmissionDate = item.EmissionDate ?? DateTime.Today;
                var activeEmission = item.Emissions
                    .OrderByDescending(emission => emission.EmittedAt)
                    .FirstOrDefault(emission => emission.CanceledAt == null);
                var lastCanceledEmission = item.Emissions
                    .Where(emission => emission.CanceledAt != null && !string.IsNullOrWhiteSpace(emission.CancelReason))
                    .OrderByDescending(emission => emission.CanceledAt)
                    .ThenByDescending(emission => emission.Id)
                    .FirstOrDefault();

                var line = new EmissionConfirmationLineViewModel
                {
                    Id = item.Id,
                    Sequence = item.SeqNo,
                    Amount = item.Amount,
                    EmissionDate = item.EmissionDate,
                    DueDate = item.DueDate,
                    Status = item.Status,
                    RitmNumber = string.IsNullOrWhiteSpace(item.RitmNumber) ? string.Empty : item.RitmNumber,
                    BzCode = activeEmission?.BzCode ?? string.Empty,
                    EmittedAt = activeEmission?.EmittedAt ?? defaultEmissionDate,
                    CancelReason = string.Empty,
                    LastCancellationReason = lastCanceledEmission?.CancelReason ?? string.Empty,
                };

                line.SetCurrency(_currentCurrencyCode);

                Lines.Add(line);
            }

            if (SelectedPlan is not null && SelectedPlan.Id == plan.Id)
            {
                SelectedPlan.UpdateCounts(
                    plan.Items.Count(item => item.Status == InvoiceItemStatus.Planned),
                    plan.Items.Count(item => item.Status == InvoiceItemStatus.Requested),
                    plan.Items.Count(item => item.Status == InvoiceItemStatus.Emitted),
                    plan.Items.Count(item => item.Status == InvoiceItemStatus.Closed),
                    plan.Items.Count(item => item.Status == InvoiceItemStatus.Canceled));
            }

            RefreshSummaries();

            SelectedLine = Lines.FirstOrDefault();
            IsPlanDetailsVisible = Lines.Count > 0;

            if (!suppressStatusMessage)
            {
                StatusMessage = LocalizationRegistry.Format("INV_Emission_Status_PlanLoaded", plan.Id, Lines.Count);
            }

            RefreshActionCommands();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load invoice plan {PlanId} for emission confirmation.", planId);
            ValidationMessage = LocalizationRegistry.Format("INV_Emission_Status_LoadFailureDetail", ex.Message);
        }
    }

    internal void HandleClose(EmissionConfirmationLineViewModel line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        ResetMessages();

        if (CurrentPlanId <= 0)
        {
            var message = LocalizationRegistry.Get("INV_Emission_Validation_PlanRequired");
            ValidationMessage = message;
            ToastService.ShowWarning("INV_Emission_Toast_ValidationFailed", message);
            return;
        }

        if (line.EmittedAt is null)
        {
            var message = LocalizationRegistry.Get("INV_Emission_Validation_EmissionDate");
            ValidationMessage = message;
            ToastService.ShowWarning("INV_Emission_Toast_ValidationFailed", message);
            return;
        }

        if (string.IsNullOrWhiteSpace(line.BzCode))
        {
            var message = LocalizationRegistry.Get("INV_Emission_Validation_BzCode");
            ValidationMessage = message;
            ToastService.ShowWarning("INV_Emission_Toast_ValidationFailed", message);
            return;
        }

        var update = new InvoiceEmissionUpdate
        {
            ItemId = line.Id,
            BzCode = line.BzCode.Trim(),
            EmittedAt = line.EmittedAt.Value,
        };

        try
        {
            var result = _repository.CloseItems(CurrentPlanId, new[] { update });

            if (result.Updated == 0)
            {
                StatusMessage = LocalizationRegistry.Get("INV_Emission_Status_NoEmissions");
                ToastService.ShowWarning("INV_Emission_Toast_NoEmissions");
                return;
            }

            var sequence = line.Sequence;
            LoadPlanById(CurrentPlanId, suppressStatusMessage: true);
            SelectedLine = Lines.FirstOrDefault(l => l.Sequence == sequence);
            StatusMessage = LocalizationRegistry.Format("INV_Emission_Status_LineEmitted", sequence);
            ToastService.ShowSuccess("INV_Emission_Toast_LineEmitted", sequence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close invoice item {ItemId}.", line.Id);
            ValidationMessage = ex.Message;
            ToastService.ShowError("INV_Emission_Toast_OperationFailed", ex.Message);
        }
    }

    internal void HandleCancel(EmissionConfirmationLineViewModel line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        ResetMessages();

        if (CurrentPlanId <= 0)
        {
            var message = LocalizationRegistry.Get("INV_Emission_Validation_CancelPlanRequired");
            ValidationMessage = message;
            ToastService.ShowWarning("INV_Emission_Toast_ValidationFailed", message);
            return;
        }

        if (string.IsNullOrWhiteSpace(line.CancelReason))
        {
            var message = LocalizationRegistry.Get("INV_Emission_Validation_CancelReason");
            ValidationMessage = message;
            ToastService.ShowWarning("INV_Emission_Toast_ValidationFailed", message);
            return;
        }

        var cancellation = new InvoiceEmissionCancellation
        {
            ItemId = line.Id,
            CancelReason = line.CancelReason.Trim(),
            CanceledAt = DateTime.Today,
        };

        try
        {
            var result = _repository.CancelEmissions(CurrentPlanId, new[] { cancellation });

            if (result.Updated == 0)
            {
                StatusMessage = LocalizationRegistry.Get("INV_Emission_Status_NoCancellations");
                ToastService.ShowWarning("INV_Emission_Toast_NoCancellations");
                return;
            }

            LoadPlanById(CurrentPlanId, suppressStatusMessage: true);
            SelectedLine = Lines.FirstOrDefault(l => l.Sequence == line.Sequence);

            StatusMessage = LocalizationRegistry.Format("INV_Emission_Status_LineCanceled", line.Sequence);
            ToastService.ShowSuccess("INV_Emission_Toast_LineCanceled", line.Sequence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel invoice item {ItemId}.", line.Id);
            ValidationMessage = ex.Message;
            ToastService.ShowError("INV_Emission_Toast_OperationFailed", ex.Message);
        }
    }

    internal void RefreshSummaries()
    {
        RequestedCount = Lines.Count(line => line.Status == InvoiceItemStatus.Requested);
        EmittedCount = Lines.Count(line => line.Status == InvoiceItemStatus.Emitted);
        CanceledCount = Lines.Count(line => line.Status == InvoiceItemStatus.Canceled);
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
        RefreshActionCommands();
    }

    private void LoadAvailablePlans()
    {
        var previouslySelectedId = SelectedPlan?.Id;

        try
        {
            var plans = _repository.ListPlansForEmissionStage();

            AvailablePlans.Clear();

            foreach (var summary in plans)
            {
                AvailablePlans.Add(InvoicePlanSummaryViewModel.FromSummary(summary));
            }

            PlanSelectionMessage = plans.Count == 0
                ? LocalizationRegistry.Get("INV_Emission_Message_Empty")
                : LocalizationRegistry.Get("INV_Emission_Message_SelectHint");

            if (_accessScope.IsInitialized && !_accessScope.HasAssignments && string.IsNullOrWhiteSpace(_accessScope.InitializationError))
            {
                PlanSelectionMessage = LocalizationRegistry.Format("INV_Access_Message_NoAssignments", GetLoginDisplay(_accessScope));
            }

            SelectedPlan = AvailablePlans.FirstOrDefault(plan => plan.Id == previouslySelectedId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load available plans for emission confirmation.");
            AvailablePlans.Clear();
            SelectedPlan = null;
            PlanSelectionMessage = LocalizationRegistry.Format("INV_Emission_Status_LoadFailure", ex.Message);
        }
    }

    private void UpdateSelectedPlanSummaryCounts()
    {
        if (SelectedPlan is null || SelectedPlan.Id != CurrentPlanId)
        {
            return;
        }

        var planned = SelectedPlan.PlannedItemCount;
        SelectedPlan.UpdateCounts(planned, RequestedCount, EmittedCount, Lines.Count(line => line.Status == InvoiceItemStatus.Closed), CanceledCount);
    }

    private void EnsureSelectedLineIsValid()
    {
        if (Lines.Count == 0)
        {
            SelectedLine = null;
            IsPlanDetailsVisible = false;
            return;
        }

        if (SelectedLine is null || !Lines.Contains(SelectedLine))
        {
            SelectedLine = Lines.FirstOrDefault();
        }

        IsPlanDetailsVisible = SelectedLine is not null;
    }

    private bool CanSaveSelectedLine()
    {
        return SelectedLine is { Status: InvoiceItemStatus.Requested } line
               && !string.IsNullOrWhiteSpace(line.BzCode)
               && line.EmittedAt is not null;
    }

    private void SaveSelectedLine()
    {
        if (SelectedLine is not null)
        {
            HandleClose(SelectedLine);
        }
    }

    private bool CanCancelSelectedLine()
    {
        return SelectedLine is { Status: InvoiceItemStatus.Emitted } line
               && !string.IsNullOrWhiteSpace(line.CancelReason);
    }

    private void CancelSelectedLine()
    {
        if (SelectedLine is not null)
        {
            HandleCancel(SelectedLine);
        }
    }

    private void ClosePlanDetails()
    {
        IsPlanDetailsVisible = false;
        SelectedLine = null;
    }

    private void RefreshActionCommands()
    {
        _saveSelectedLineCommand.NotifyCanExecuteChanged();
        _cancelSelectedLineCommand.NotifyCanExecuteChanged();
        _closePlanDetailsCommand.NotifyCanExecuteChanged();
    }

    private void OnSelectedLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EmissionConfirmationLineViewModel.BzCode)
            or nameof(EmissionConfirmationLineViewModel.EmittedAt)
            or nameof(EmissionConfirmationLineViewModel.Status)
            or nameof(EmissionConfirmationLineViewModel.CancelReason))
        {
            RefreshActionCommands();
        }
    }
}
