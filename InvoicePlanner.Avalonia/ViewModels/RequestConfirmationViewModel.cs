using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public RequestConfirmationViewModel(
        IInvoicePlanRepository repository,
        ILogger<RequestConfirmationViewModel> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Lines.CollectionChanged += OnLinesCollectionChanged;

        LoadPlanCommand = new RelayCommand(LoadPlan);
    }

    public ObservableCollection<RequestConfirmationLineViewModel> Lines { get; } = new();

    [ObservableProperty]
    private string planIdText = string.Empty;

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

    public bool HasLines => Lines.Count > 0;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public IRelayCommand LoadPlanCommand { get; }

    partial void OnValidationMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasValidationMessage));
    }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
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

    private void LoadPlan()
    {
        ResetMessages();

        if (!TryResolvePlanId(out var planId))
        {
            ValidationMessage = Strings.Get("RequestValidationPlanId");
            return;
        }

        try
        {
            var plan = _repository.GetPlan(planId);
            if (plan is null)
            {
                ClearLines();
                EngagementId = string.Empty;
                CurrentPlanId = 0;
                ValidationMessage = Strings.Format("RequestValidationNotFound", planId);
                return;
            }

            CurrentPlanId = plan.Id;
            PlanIdText = plan.Id.ToString(CultureInfo.InvariantCulture);
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

            RefreshSummaries();

            StatusMessage = Strings.Format("RequestStatusPlanLoaded", plan.Id, Lines.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load invoice plan {PlanId}.", planId);
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

    private bool TryResolvePlanId(out int planId)
    {
        if (!string.IsNullOrWhiteSpace(PlanIdText))
        {
            var trimmed = PlanIdText.Trim();

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            {
                planId = parsed;
                return true;
            }

            planId = 0;
            return false;
        }

        if (CurrentPlanId > 0)
        {
            planId = CurrentPlanId;
            return true;
        }

        planId = 0;
        return false;
    }
}
