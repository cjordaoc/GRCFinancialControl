using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoicePlanner.Avalonia.Resources;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Core.Validation;
using Invoices.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class PlanEditorViewModel : ViewModelBase
{
    private readonly IInvoicePlanRepository _repository;
    private readonly IInvoicePlanValidator _validator;
    private readonly ILogger<PlanEditorViewModel> _logger;
    private bool _suppressLineUpdates;
    private bool _isInitializing;

    public PlanEditorViewModel(
        IInvoicePlanRepository repository,
        IInvoicePlanValidator validator,
        ILogger<PlanEditorViewModel> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _isInitializing = true;

        Items.CollectionChanged += OnItemsCollectionChanged;
        AdditionalEmails.CollectionChanged += OnAdditionalEmailsChanged;

        PlanTypes = Enum.GetValues<InvoicePlanType>();

        AddEmailCommand = new RelayCommand(AddEmail);
        RebalanceCommand = new RelayCommand(RebalanceTotals);
        SavePlanCommand = new RelayCommand(SavePlan);

        // Seed with default values so the editor presents a useful layout.
        PlanType = InvoicePlanType.ByDate;
        PlanningBaseValue = 10000m;
        NumInvoices = 3;
        PaymentTermDays = 30;
        FirstEmissionDate = DateTime.Today;
        CustomerFocalPointName = string.Empty;
        CustomerFocalPointEmail = string.Empty;
        CustomInstructions = string.Empty;

        _isInitializing = false;
        EnsureItemCount();
        RecalculateTotals();
    }

    public ObservableCollection<InvoicePlanLineViewModel> Items { get; } = new();

    public ObservableCollection<PlanEmailViewModel> AdditionalEmails { get; } = new();

    public IReadOnlyList<InvoicePlanType> PlanTypes { get; }

    [ObservableProperty]
    private int planId;

    [ObservableProperty]
    private string engagementId = "ENG-001";

    [ObservableProperty]
    private string engagementName = "Sample Engagement";

    [ObservableProperty]
    private InvoicePlanType planType;

    [ObservableProperty]
    private int paymentTermDays;

    [ObservableProperty]
    private string customerFocalPointName = string.Empty;

    [ObservableProperty]
    private string customerFocalPointEmail = string.Empty;

    [ObservableProperty]
    private string? customInstructions;

    [ObservableProperty]
    private DateTime? firstEmissionDate;

    [ObservableProperty]
    private decimal planningBaseValue;

    [ObservableProperty]
    private decimal totalPercentage;

    [ObservableProperty]
    private decimal totalAmount;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string? validationMessage;

    private int _numInvoices = 1;

    public int NumInvoices
    {
        get => _numInvoices;
        set
        {
            if (value < 1)
            {
                value = 1;
            }

        if (SetProperty(ref _numInvoices, value))
        {
            if (!_isInitializing)
            {
                EnsureItemCount();
            }
        }
    }
    }

    public bool RequiresFirstEmissionDate => PlanType == InvoicePlanType.ByDate;

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (InvoicePlanLineViewModel line in e.OldItems)
            {
                line.Detach();
            }
        }

        if (e.NewItems is not null)
        {
            foreach (InvoicePlanLineViewModel line in e.NewItems)
            {
                line.Attach(this);
            }
        }

        RefreshSequences();
        RecalculateTotals();
    }

    private void OnAdditionalEmailsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasAdditionalEmails));
    }

    public bool HasAdditionalEmails => AdditionalEmails.Count > 0;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnPlanTypeChanged(InvoicePlanType value)
    {
        if (_isInitializing)
        {
            return;
        }

        UpdateLineTypeSettings();

        ApplyEmissionDateRule();
        OnPropertyChanged(nameof(RequiresFirstEmissionDate));
    }

    partial void OnPlanningBaseValueChanged(decimal value)
    {
        if (_isInitializing)
        {
            return;
        }

        if (value < 0)
        {
            PlanningBaseValue = 0;
            return;
        }

        RecalculateAmountsFromPercentages();
    }

    partial void OnFirstEmissionDateChanged(DateTime? value)
    {
        if (_isInitializing)
        {
            return;
        }

        ApplyEmissionDateRule();
    }

    partial void OnValidationMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasValidationMessage));
    }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    public IRelayCommand AddEmailCommand { get; }

    public IRelayCommand RebalanceCommand { get; }

    public IRelayCommand SavePlanCommand { get; }

    public bool LoadPlan(int planId)
    {
        ValidationMessage = null;
        StatusMessage = null;

        try
        {
            var plan = _repository.GetPlan(planId);
            if (plan is null)
            {
                ValidationMessage = Strings.Format("PlanEditorValidationPlanNotFound", planId);
                return false;
            }

            ApplyPlan(plan);
            StatusMessage = Strings.Format("PlanEditorStatusPlanLoaded", planId, Items.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load invoice plan {PlanId}.", planId);
            ValidationMessage = Strings.Format("PlanEditorValidationLoadFailed", ex.Message);
            return false;
        }
    }

    private void ApplyPlan(InvoicePlan plan)
    {
        _isInitializing = true;

        try
        {
            PlanId = plan.Id;
            EngagementId = plan.EngagementId;
            PlanType = plan.Type;
            PaymentTermDays = plan.PaymentTermDays;
            CustomerFocalPointName = plan.CustomerFocalPointName;
            CustomerFocalPointEmail = plan.CustomerFocalPointEmail;
            CustomInstructions = plan.CustomInstructions;
            FirstEmissionDate = plan.FirstEmissionDate;
            _numInvoices = plan.NumInvoices;
            OnPropertyChanged(nameof(NumInvoices));

            Items.Clear();
            foreach (var item in plan.Items.OrderBy(item => item.SeqNo))
            {
                var line = new InvoicePlanLineViewModel
                {
                    Id = item.Id,
                    Sequence = item.SeqNo,
                    EmissionDate = item.EmissionDate,
                    DeliveryDescription = item.DeliveryDescription,
                    Percentage = item.Percentage,
                    Amount = item.Amount,
                    CanEditEmissionDate = plan.Type != InvoicePlanType.ByDate,
                    ShowDeliveryDescription = plan.Type == InvoicePlanType.ByDelivery,
                };

                Items.Add(line);
            }

            AdditionalEmails.Clear();
            foreach (var email in plan.AdditionalEmails)
            {
                var emailVm = new PlanEmailViewModel(RemoveEmail)
                {
                    Id = email.Id,
                    Email = email.Email,
                };

                AdditionalEmails.Add(emailVm);
            }

            PlanningBaseValue = Math.Round(plan.Items.Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero);
        }
        finally
        {
            _isInitializing = false;
            EnsureItemCount();
            RefreshSequences();
            UpdateLineTypeSettings();
            ApplyEmissionDateRule();
            RecalculateTotals();
            OnPropertyChanged(nameof(RequiresFirstEmissionDate));
        }
    }

    internal void HandleLinePercentageChanged(InvoicePlanLineViewModel line)
    {
        if (_suppressLineUpdates)
        {
            return;
        }

        _suppressLineUpdates = true;
        var amount = Math.Round(PlanningBaseValue * line.Percentage / 100m, 2, MidpointRounding.AwayFromZero);
        line.SetAmount(amount);
        _suppressLineUpdates = false;

        RecalculateTotals();
    }

    internal void HandleLineAmountChanged(InvoicePlanLineViewModel line)
    {
        if (_suppressLineUpdates)
        {
            return;
        }

        _suppressLineUpdates = true;
        var baseValue = PlanningBaseValue;
        var percent = baseValue == 0
            ? 0
            : Math.Round(line.Amount / baseValue * 100m, 4, MidpointRounding.AwayFromZero);
        line.SetPercentage(percent);
        _suppressLineUpdates = false;

        RecalculateTotals();
    }

    private void EnsureItemCount()
    {
        while (Items.Count < NumInvoices)
        {
            var line = new InvoicePlanLineViewModel
            {
                Sequence = Items.Count + 1,
                EmissionDate = DateTime.Today,
                CanEditEmissionDate = PlanType != InvoicePlanType.ByDate,
                ShowDeliveryDescription = PlanType == InvoicePlanType.ByDelivery,
            };

            Items.Add(line);
        }

        while (Items.Count > NumInvoices)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        UpdateLineTypeSettings();
        DistributePercentages();
        ApplyEmissionDateRule();
    }

    private void RefreshSequences()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].Sequence = index + 1;
        }
    }

    private void DistributePercentages()
    {
        if (Items.Count == 0)
        {
            return;
        }

        var evenPercent = Math.Round(100m / Items.Count, 4, MidpointRounding.AwayFromZero);

        _suppressLineUpdates = true;
        foreach (var line in Items)
        {
            line.SetPercentage(evenPercent);
            line.SetAmount(Math.Round(PlanningBaseValue * evenPercent / 100m, 2, MidpointRounding.AwayFromZero));
        }
        _suppressLineUpdates = false;

        RecalculateTotals();
    }

    private void RecalculateAmountsFromPercentages()
    {
        _suppressLineUpdates = true;
        foreach (var line in Items)
        {
            var amount = Math.Round(PlanningBaseValue * line.Percentage / 100m, 2, MidpointRounding.AwayFromZero);
            line.SetAmount(amount);
        }
        _suppressLineUpdates = false;

        RecalculateTotals();
    }

    private void ApplyEmissionDateRule()
    {
        if (PlanType != InvoicePlanType.ByDate)
        {
            return;
        }

        if (FirstEmissionDate is null)
        {
            return;
        }

        _suppressLineUpdates = true;
        foreach (var line in Items)
        {
            var emissionDate = FirstEmissionDate.Value.AddMonths(line.Sequence - 1);
            line.SetEmissionDate(emissionDate);
        }
        _suppressLineUpdates = false;
    }

    private void UpdateLineTypeSettings()
    {
        var isByDate = PlanType == InvoicePlanType.ByDate;
        var isByDelivery = PlanType == InvoicePlanType.ByDelivery;

        foreach (var line in Items)
        {
            line.CanEditEmissionDate = !isByDate;
            line.ShowDeliveryDescription = isByDelivery;
        }
    }

    private void RecalculateTotals()
    {
        TotalPercentage = Math.Round(Items.Sum(line => line.Percentage), 4, MidpointRounding.AwayFromZero);
        TotalAmount = Math.Round(Items.Sum(line => line.Amount), 2, MidpointRounding.AwayFromZero);
        OnPropertyChanged(nameof(HasAdditionalEmails));
    }

    private void AddEmail()
    {
        AdditionalEmails.Add(new PlanEmailViewModel(RemoveEmail));
    }

    private void RemoveEmail(PlanEmailViewModel email)
    {
        AdditionalEmails.Remove(email);
    }

    private void SavePlan()
    {
        ValidationMessage = null;
        StatusMessage = null;

        if (RequiresFirstEmissionDate && FirstEmissionDate is null)
        {
            ValidationMessage = Strings.Get("PlanEditorValidationFirstEmissionRequired");
            return;
        }

        if (Items.Count == 0)
        {
            ValidationMessage = Strings.Get("PlanEditorValidationNoLines");
            return;
        }

        RebalanceTotals();

        RefreshSequences();
        var plan = BuildPlanModel();
        var validationErrors = _validator.Validate(plan, PlanningBaseValue);

        if (validationErrors.Count > 0)
        {
            ValidationMessage = string.Join(Environment.NewLine, validationErrors.Distinct());
            return;
        }

        try
        {
            var result = _repository.SavePlan(plan);
            var persisted = _repository.GetPlan(plan.Id) ?? plan;
            ApplyPlan(persisted);

            var action = result.Created == 1
                ? Strings.Get("PlanEditorStatusActionCreated")
                : Strings.Get("PlanEditorStatusActionUpdated");
            StatusMessage = Strings.Format(
                "PlanEditorStatusPlanSaved",
                PlanId,
                action,
                Items.Count,
                result.AffectedRows,
                result.Deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save invoice plan {PlanId}.", plan.Id);
            ValidationMessage = Strings.Format("PlanEditorValidationSaveFailed", ex.Message);
        }
    }

    private InvoicePlan BuildPlanModel()
    {
        var plan = new InvoicePlan
        {
            Id = PlanId,
            EngagementId = EngagementId?.Trim() ?? string.Empty,
            Type = PlanType,
            NumInvoices = NumInvoices,
            PaymentTermDays = PaymentTermDays,
            CustomerFocalPointName = CustomerFocalPointName?.Trim() ?? string.Empty,
            CustomerFocalPointEmail = CustomerFocalPointEmail?.Trim() ?? string.Empty,
            CustomInstructions = string.IsNullOrWhiteSpace(CustomInstructions) ? null : CustomInstructions!.Trim(),
            FirstEmissionDate = FirstEmissionDate,
        };

        foreach (var line in Items.OrderBy(line => line.Sequence))
        {
            var emissionDate = line.EmissionDate;
            var item = new InvoiceItem
            {
                Id = line.Id,
                PlanId = PlanId,
                SeqNo = line.Sequence,
                Percentage = Math.Round(line.Percentage, 4, MidpointRounding.AwayFromZero),
                Amount = Math.Round(line.Amount, 2, MidpointRounding.AwayFromZero),
                EmissionDate = emissionDate,
                DueDate = emissionDate?.AddDays(PaymentTermDays),
                DeliveryDescription = string.IsNullOrWhiteSpace(line.DeliveryDescription) ? null : line.DeliveryDescription.Trim(),
                PayerCnpj = string.Empty,
            };

            plan.Items.Add(item);
        }

        foreach (var email in AdditionalEmails)
        {
            var trimmed = email.Email?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            plan.AdditionalEmails.Add(new InvoicePlanEmail
            {
                Id = email.Id,
                PlanId = PlanId,
                Email = trimmed,
            });
        }

        return plan;
    }

    private void AdjustLastLineForTotals()
    {
        if (Items.Count == 0)
        {
            return;
        }

        var expectedAmount = PlanningBaseValue;
        const decimal expectedPercent = 100m;

        if (Items.Count == 1)
        {
            _suppressLineUpdates = true;
            var onlyLine = Items[0];
            onlyLine.SetPercentage(Math.Round(expectedPercent, 4, MidpointRounding.AwayFromZero));
            onlyLine.SetAmount(Math.Round(expectedAmount, 2, MidpointRounding.AwayFromZero));
            _suppressLineUpdates = false;
            return;
        }

        var percentExceptLast = Items.Take(Items.Count - 1).Sum(line => line.Percentage);
        var amountExceptLast = Items.Take(Items.Count - 1).Sum(line => line.Amount);
        var lastLine = Items[^1];

        var adjustedPercent = Math.Round(expectedPercent - percentExceptLast, 4, MidpointRounding.AwayFromZero);
        var adjustedAmount = Math.Round(expectedAmount - amountExceptLast, 2, MidpointRounding.AwayFromZero);

        if (adjustedPercent < 0)
        {
            adjustedPercent = 0;
        }

        if (adjustedAmount < 0)
        {
            adjustedAmount = 0;
        }

        _suppressLineUpdates = true;
        lastLine.SetPercentage(adjustedPercent);
        lastLine.SetAmount(adjustedAmount);
        _suppressLineUpdates = false;
    }

    private void RebalanceTotals()
    {
        AdjustLastLineForTotals();
        RecalculateTotals();
    }
}
