using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Messages;
using InvoicePlanner.Avalonia.Resources;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class RequestConfirmationViewModel : ViewModelBase
{
    private readonly IInvoicePlanRepository _repository;
    private readonly ILogger<RequestConfirmationViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly RelayCommand _loadPlanCommand;

    public RequestConfirmationViewModel(
        IInvoicePlanRepository repository,
        ILogger<RequestConfirmationViewModel> logger,
        IMessenger messenger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        Lines.CollectionChanged += OnLinesCollectionChanged;
        AvailablePlans.CollectionChanged += OnAvailablePlansChanged;

        _loadPlanCommand = new RelayCommand(LoadSelectedPlan, () => SelectedPlan is not null);

        _messenger.Register<ConnectionSettingsImportedMessage>(this, (_, _) => LoadAvailablePlans());

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
    private string? statusMessage;

    [ObservableProperty]
    private string? validationMessage;

    [ObservableProperty]
    private int plannedCount;

    [ObservableProperty]
    private int requestedCount;

    [ObservableProperty]
    private string? planSelectionMessage;

    public bool HasLines => Lines.Count > 0;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasAvailablePlans => AvailablePlans.Count > 0;

    public IRelayCommand LoadPlanCommand => _loadPlanCommand;

    partial void OnValidationMessageChanged(string? value) => OnPropertyChanged(nameof(HasValidationMessage));

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnSelectedPlanChanged(InvoicePlanSummaryViewModel? value)
    {
        _loadPlanCommand.NotifyCanExecuteChanged();
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
            ValidationMessage = Strings.Get("RequestValidationPlanSelection");
            return;
        }

        try
        {
            var plan = _repository.GetPlan(SelectedPlan.Id);
            if (plan is null)
            {
                ClearLines();
                EngagementId = string.Empty;
                CurrentPlanId = 0;
                ValidationMessage = Strings.Format("RequestValidationNotFound", SelectedPlan.Id);
                return;
            }

            CurrentPlanId = plan.Id;
            EngagementId = plan.EngagementId;

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

                Lines.Add(line);
            }

            SelectedPlan.UpdateCounts(
                plan.Items.Count(item => item.Status == InvoiceItemStatus.Planned),
                plan.Items.Count(item => item.Status == InvoiceItemStatus.Requested),
                plan.Items.Count(item => item.Status == InvoiceItemStatus.Closed),
                plan.Items.Count(item => item.Status == InvoiceItemStatus.Canceled));

            RefreshSummaries();

            StatusMessage = Strings.Format("RequestStatusPlanLoaded", plan.Id, Lines.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load invoice plan {PlanId}.", SelectedPlan.Id);
            ValidationMessage = Strings.Format("RequestValidationLoadFailed", ex.Message);
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
            ValidationMessage = Strings.Get("RequestValidationPlanRequired");
            return;
        }

        if (line.RequestDate is null)
        {
            ValidationMessage = Strings.Get("RequestValidationRequestDateRequired");
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
                StatusMessage = Strings.Get("RequestStatusNoUpdates");
                return;
            }

            line.ApplyRequestedState(update.RitmNumber, update.CoeResponsible, update.RequestDate.Date);

            StatusMessage = Strings.Format("RequestStatusLineRequested", line.Sequence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request invoice item {ItemId}.", line.Id);
            ValidationMessage = ex.Message;
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
            ValidationMessage = Strings.Get("RequestValidationUndoPlanRequired");
            return;
        }

        try
        {
            var result = _repository.UndoRequest(CurrentPlanId, new[] { line.Id });

            if (result.Updated == 0)
            {
                StatusMessage = Strings.Get("RequestStatusNoUndo");
                return;
            }

            line.ResetToPlanned(DateTime.Today);

            StatusMessage = Strings.Format("RequestStatusLineUndone", line.Sequence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to undo invoice item {ItemId} request.", line.Id);
            ValidationMessage = ex.Message;
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
                ? Strings.Get("RequestPlansEmpty")
                : Strings.Get("RequestPlansSelectHint");

            SelectedPlan = AvailablePlans.FirstOrDefault(plan => plan.Id == previouslySelectedId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load available plans for request confirmation.");
            AvailablePlans.Clear();
            SelectedPlan = null;
            PlanSelectionMessage = Strings.Format("RequestPlansLoadError", ex.Message);
        }
    }

    private void UpdateSelectedPlanSummaryCounts()
    {
        if (SelectedPlan is null || SelectedPlan.Id != CurrentPlanId)
        {
            return;
        }

        var closed = Lines.Count(line => line.Status == InvoiceItemStatus.Closed);
        var canceled = Lines.Count(line => line.Status == InvoiceItemStatus.Canceled);

        SelectedPlan.UpdateCounts(PlannedCount, RequestedCount, closed, canceled);
    }
}
