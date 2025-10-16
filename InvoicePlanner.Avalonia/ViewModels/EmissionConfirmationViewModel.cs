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

public partial class EmissionConfirmationViewModel : ViewModelBase
{
    private readonly IInvoicePlanRepository _repository;
    private readonly ILogger<EmissionConfirmationViewModel> _logger;

    public EmissionConfirmationViewModel(
        IInvoicePlanRepository repository,
        ILogger<EmissionConfirmationViewModel> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Lines.CollectionChanged += OnLinesCollectionChanged;

        LoadPlanCommand = new RelayCommand(LoadPlan);
    }

    public ObservableCollection<EmissionConfirmationLineViewModel> Lines { get; } = new();

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
    private int requestedCount;

    [ObservableProperty]
    private int closedCount;

    [ObservableProperty]
    private int canceledCount;

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
    }

    private void LoadPlan()
    {
        ResetMessages();

        if (!TryResolvePlanId(out var planId))
        {
            ValidationMessage = "Provide a valid plan id to load.";
            return;
        }

        LoadPlanById(planId, suppressStatusMessage: false);
    }

    private void LoadPlanById(int planId, bool suppressStatusMessage)
    {
        ValidationMessage = null;

        try
        {
            var plan = _repository.GetPlan(planId);
            if (plan is null)
            {
                ClearLines();
                EngagementId = string.Empty;
                CurrentPlanId = 0;
                ValidationMessage = Strings.Format("EmissionValidationNotFound", planId);
                return;
            }

            CurrentPlanId = plan.Id;
            PlanIdText = plan.Id.ToString(CultureInfo.InvariantCulture);
            EngagementId = plan.EngagementId;

            ClearLines();

            foreach (var item in plan.Items
                         .Where(item => item.Status != InvoiceItemStatus.Planned)
                         .OrderBy(item => item.SeqNo))
            {
                var line = new EmissionConfirmationLineViewModel
                {
                    Id = item.Id,
                    Sequence = item.SeqNo,
                    Amount = item.Amount,
                    EmissionDate = item.EmissionDate,
                    DueDate = item.DueDate,
                    Status = item.Status,
                    RitmNumber = string.IsNullOrWhiteSpace(item.RitmNumber) ? string.Empty : item.RitmNumber,
                    BzCode = string.IsNullOrWhiteSpace(item.BzCode) ? string.Empty : item.BzCode,
                    EmittedAt = item.EmittedAt ?? DateTime.Today,
                    CancelReason = string.IsNullOrWhiteSpace(item.CancelReason) ? string.Empty : item.CancelReason,
                    ReissueEmissionDate = item.EmissionDate ?? DateTime.Today,
                };

                Lines.Add(line);
            }

            RefreshSummaries();

            if (!suppressStatusMessage)
            {
                StatusMessage = Strings.Format("EmissionStatusPlanLoaded", plan.Id, Lines.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load invoice plan {PlanId} for emission confirmation.", planId);
            ValidationMessage = Strings.Format("EmissionValidationLoadFailed", ex.Message);
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
            ValidationMessage = Strings.Get("EmissionValidationPlanRequired");
            return;
        }

        if (line.EmittedAt is null)
        {
            ValidationMessage = Strings.Get("EmissionValidationEmissionDate");
            return;
        }

        if (string.IsNullOrWhiteSpace(line.BzCode))
        {
            ValidationMessage = Strings.Get("EmissionValidationBzCode");
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
                StatusMessage = Strings.Get("EmissionStatusNoClosures");
                return;
            }

            line.ApplyClosedState(update.BzCode, update.EmittedAt.Value.Date);

            StatusMessage = Strings.Format("EmissionStatusLineClosed", line.Sequence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close invoice item {ItemId}.", line.Id);
            ValidationMessage = ex.Message;
        }
    }

    internal void HandleCancelAndReissue(EmissionConfirmationLineViewModel line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        ResetMessages();

        if (CurrentPlanId <= 0)
        {
            ValidationMessage = Strings.Get("EmissionValidationCancelPlanRequired");
            return;
        }

        if (string.IsNullOrWhiteSpace(line.CancelReason))
        {
            ValidationMessage = Strings.Get("EmissionValidationCancelReason");
            return;
        }

        if (line.ReissueEmissionDate is null)
        {
            ValidationMessage = Strings.Get("EmissionValidationReissueDate");
            return;
        }

        var request = new InvoiceReissueRequest
        {
            ItemId = line.Id,
            CancelReason = line.CancelReason.Trim(),
            ReplacementEmissionDate = line.ReissueEmissionDate.Value,
        };

        try
        {
            var result = _repository.CancelAndReissue(CurrentPlanId, new[] { request });

            if (result.Updated == 0)
            {
                StatusMessage = Strings.Get("EmissionStatusNoCancellations");
                return;
            }

            LoadPlanById(CurrentPlanId, suppressStatusMessage: true);

            StatusMessage = Strings.Format("EmissionStatusLineCanceled", line.Sequence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel invoice item {ItemId}.", line.Id);
            ValidationMessage = ex.Message;
        }
    }

    internal void RefreshSummaries()
    {
        RequestedCount = Lines.Count(line => line.Status == InvoiceItemStatus.Requested);
        ClosedCount = Lines.Count(line => line.Status == InvoiceItemStatus.Closed);
        CanceledCount = Lines.Count(line => line.Status == InvoiceItemStatus.Canceled);
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
