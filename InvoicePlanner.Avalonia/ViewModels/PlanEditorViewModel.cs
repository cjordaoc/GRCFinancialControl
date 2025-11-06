using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Globalization;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Services;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Core.Payments;
using Invoices.Core.Validation;
using Invoices.Data.Repositories;
using Invoices.Core.Utilities;
using Microsoft.Extensions.Logging;
using GRC.Shared.UI.Messages;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class PlanEditorViewModel : ViewModelBase
{
    private const int ByDateEmissionIntervalDays = 30;
    private readonly IInvoicePlanRepository _repository;
    private readonly IInvoicePlanValidator _validator;
    private readonly ILogger<PlanEditorViewModel> _logger;
    private readonly IInvoiceAccessScope _accessScope;
    private readonly DialogService _dialogService;
    private readonly RelayCommand _savePlanCommand;
    private readonly RelayCommand _editLinesCommand;
    private readonly RelayCommand _createPlanCommand;
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand _deletePlanCommand;
    private readonly RelayCommand _closePlanFormCommand;
    private readonly RelayCommand _previewDescriptionCommand;
    private bool _suppressLineUpdates;
    private bool _isInitializing;
    private bool _isNormalizingFirstEmissionDate;
    private PlanEditorDialogViewModel? _dialogViewModel;
    private InvoicePlanLineViewModel? _selectedLineSubscription;

    public PlanEditorViewModel(
        IInvoicePlanRepository repository,
        IInvoicePlanValidator validator,
        ILogger<PlanEditorViewModel> logger,
        IInvoiceAccessScope accessScope,
        DialogService dialogService,
        IMessenger messenger)
        : base(messenger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accessScope = accessScope ?? throw new ArgumentNullException(nameof(accessScope));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        _accessScope.EnsureInitialized();

        _isInitializing = true;

        Items.CollectionChanged += OnItemsCollectionChanged;

        PlanTypes = Enum.GetValues<InvoicePlanType>();

        _savePlanCommand = new RelayCommand(SavePlan, CanExecuteSavePlan);
        _editLinesCommand = new RelayCommand(ShowInvoiceLinesDialog);
        _createPlanCommand = new RelayCommand(CreatePlan, CanCreatePlan);
        _refreshCommand = new RelayCommand(LoadEngagements);
        _deletePlanCommand = new RelayCommand(DeletePlan, CanDeletePlan);
        _closePlanFormCommand = new RelayCommand(ClosePlanForm);
        _previewDescriptionCommand = new RelayCommand(PreviewInvoiceDescription, CanPreviewInvoiceDescription);

        // Seed with default values so the editor presents a useful layout.
        PlanType = InvoicePlanType.ByDate;
        PlanningBaseValue = 0m;
        NumInvoices = 1;
        PaymentTermDays = 0;
        FirstEmissionDate = DateTime.Today;
        CustomerFocalPointName = string.Empty;
        CustomInstructions = string.Empty;
        RecipientEmails = string.Empty;

        _isInitializing = false;
        EnsureItemCount();
        RecalculateTotals();

        LoadEngagements();
    }

    public ObservableCollection<EngagementOptionViewModel> Engagements { get; } = new();

    public ObservableCollection<InvoicePlanLineViewModel> Items { get; } = new();
    public IReadOnlyList<PaymentTypeOption> PaymentTypeOptions { get; } = PaymentTypeCatalog.Options;

    public IReadOnlyList<InvoicePlanType> PlanTypes { get; }

    [ObservableProperty]
    private int planId;

    [ObservableProperty]
    private string engagementId = string.Empty;

    [ObservableProperty]
    private string engagementName = string.Empty;

    [ObservableProperty]
    private InvoicePlanType planType;

    [ObservableProperty]
    private int paymentTermDays;

    [ObservableProperty]
    private string customerFocalPointName = string.Empty;

    [ObservableProperty]
    private string? customInstructions;

    [ObservableProperty]
    private string? additionalDetails;

    [ObservableProperty]
    private string? recipientEmails;

    [ObservableProperty]
    private DateTime? firstEmissionDate;

    [ObservableProperty]
    private decimal planningBaseValue;

    [ObservableProperty]
    private decimal totalPercentage;

    [ObservableProperty]
    private decimal totalAmount;

    [ObservableProperty]
    private string currencySymbol = string.Empty;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string? validationMessage;

    [ObservableProperty]
    private bool hasTotalsMismatch;

    private int _numInvoices = 1;

    [ObservableProperty]
    private EngagementOptionViewModel? selectedEngagement;

    [ObservableProperty]
    private string? engagementSelectionMessage;

    private InvoicePlanLineViewModel? _selectedInvoiceLine;

    public InvoicePlanLineViewModel? SelectedInvoiceLine
    {
        get => _selectedInvoiceLine;
        set
        {
            if (SetProperty(ref _selectedInvoiceLine, value))
            {
                OnSelectedInvoiceLineChanged(value);
            }
        }
    }

    public bool HasSelectedInvoiceLine => SelectedInvoiceLine is not null;

    public int NumInvoices
    {
        get => _numInvoices;
        set
        {
            var sanitizedValue = Math.Max(1, value);

            if (SetProperty(ref _numInvoices, sanitizedValue))
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
        RecalculateDueDates();
        RecalculateTotals();
        _savePlanCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(Items));
        EnsureSelectedInvoiceLine();
        _previewDescriptionCommand.NotifyCanExecuteChanged();
    }

    public bool HasRecipientEmails => !string.IsNullOrWhiteSpace(RecipientEmails);

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

    partial void OnPaymentTermDaysChanged(int value)
    {
        if (_isInitializing)
        {
            return;
        }

        ApplyEmissionDateRule();
        RecalculateDueDates();
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

        if (!_isNormalizingFirstEmissionDate && PlanType == InvoicePlanType.ByDate && value is DateTime firstEmission)
        {
            var adjusted = BusinessDayCalculator.AdjustToNextBusinessDay(firstEmission);

            if (adjusted != firstEmission)
            {
                try
                {
                    _isNormalizingFirstEmissionDate = true;
                    FirstEmissionDate = adjusted;
                    value = adjusted;
                }
                finally
                {
                    _isNormalizingFirstEmissionDate = false;
                }
            }
        }

        ApplyEmissionDateRule();
        RecalculateDueDates();
    }

    partial void OnValidationMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasValidationMessage));
    }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    partial void OnSelectedEngagementChanged(EngagementOptionViewModel? value)
    {
        _createPlanCommand.NotifyCanExecuteChanged();
        UpdateCurrencySymbol(value?.Currency);
    }

    partial void OnPlanIdChanged(int value)
    {
        _deletePlanCommand.NotifyCanExecuteChanged();
    }

    partial void OnRecipientEmailsChanged(string? value)
    {
        OnPropertyChanged(nameof(HasRecipientEmails));
    }

    private void OnSelectedInvoiceLineChanged(InvoicePlanLineViewModel? value)
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

        OnPropertyChanged(nameof(HasSelectedInvoiceLine));
        _previewDescriptionCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrencySymbolChanged(string value)
    {
        OnPropertyChanged(nameof(HasCurrencySymbol));
        OnPropertyChanged(nameof(TotalAmountDisplay));
        foreach (var line in Items)
        {
            line.NotifyCurrencyChanged();
        }
    }

    partial void OnTotalAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(TotalAmountDisplay));
    }

    partial void OnHasTotalsMismatchChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSavePlan));
        _savePlanCommand.NotifyCanExecuteChanged();
    }

    public IRelayCommand SavePlanCommand => _savePlanCommand;
    public IRelayCommand EditLinesCommand => _editLinesCommand;
    public IRelayCommand PreviewDescriptionCommand => _previewDescriptionCommand;

    public IRelayCommand CreatePlanCommand => _createPlanCommand;

    public IRelayCommand RefreshCommand => _refreshCommand;

    public IRelayCommand DeletePlanCommand => _deletePlanCommand;

    public IRelayCommand ClosePlanFormCommand => _closePlanFormCommand;

    public bool HasCurrencySymbol => !string.IsNullOrWhiteSpace(CurrencySymbol);

    public string TotalAmountDisplay => FormatAmount(TotalAmount);

    public bool CanSavePlan => !HasTotalsMismatch;

    internal string FormatAmount(decimal amount)
    {
        return CurrencyDisplayHelper.Format(amount, SelectedEngagement?.Currency);
    }

    public bool LoadPlan(int planId)
    {
        ValidationMessage = null;
        StatusMessage = null;

        try
        {
            var plan = _repository.GetPlan(planId);
            if (plan is null)
            {
                ValidationMessage = LocalizationRegistry.Format("INV_InvoicePlan_Validation_PlanNotFound", planId);
                return false;
            }

            ApplyPlan(plan);
            StatusMessage = LocalizationRegistry.Format("INV_InvoicePlan_Status_PlanLoaded", planId, Items.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load invoice plan {PlanId}.", planId);
            ValidationMessage = LocalizationRegistry.Format("INV_InvoicePlan_Status_LoadFailure", ex.Message);
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
            SelectedEngagement = Engagements.FirstOrDefault(option => option.EngagementId == plan.EngagementId);
            EngagementName = SelectedEngagement?.Name ?? plan.EngagementId;
            PlanType = plan.Type;
            PaymentTermDays = plan.PaymentTermDays;
            CustomerFocalPointName = plan.CustomerFocalPointName;
            CustomInstructions = plan.CustomInstructions;
            AdditionalDetails = plan.AdditionalDetails;
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
                    PayerCnpj = item.PayerCnpj,
                    PoNumber = item.PoNumber,
                    FrsNumber = item.FrsNumber,
                    CustomerTicket = item.CustomerTicket,
                    Status = item.Status,
                    AdditionalDetails = item.AdditionalInfo,
                };

                line.SetPaymentType(item.PaymentTypeCode);
                line.SetDueDate(item.DueDate);
                line.CanEditEmissionDate = line.IsEditable && plan.Type != InvoicePlanType.ByDate;
                line.ShowDeliveryDescription = plan.Type == InvoicePlanType.ByDelivery;

                Items.Add(line);
            }

            RecipientEmails = BuildRecipientList(plan);

            PlanningBaseValue = Math.Round(plan.Items.Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero);
        }
        finally
        {
            _isInitializing = false;
            EnsureItemCount();
            RefreshSequences();
            UpdateLineTypeSettings();
            ApplyEmissionDateRule();
            RecalculateDueDates();
            RecalculateTotals();
            OnPropertyChanged(nameof(RequiresFirstEmissionDate));
            OnPropertyChanged(nameof(HasRecipientEmails));
            UpdateCurrencySymbol(SelectedEngagement?.Currency);
            ShowPlanDialog();
        }
    }

    internal void HandleLinePercentageChanged(InvoicePlanLineViewModel line)
    {
        if (_suppressLineUpdates)
        {
            return;
        }

        try
        {
            _suppressLineUpdates = true;
            RebalanceFromPercentageChange(line);
        }
        finally
        {
            _suppressLineUpdates = false;
        }

        RecalculateTotals();
    }

    internal void HandleLineEmissionChanged(InvoicePlanLineViewModel line)
    {
        if (line is null)
        {
            return;
        }

        line.SetDueDate(CalculateDueDate(line.EmissionDate));

        if (ReferenceEquals(line, SelectedInvoiceLine))
        {
            _previewDescriptionCommand.NotifyCanExecuteChanged();
        }
    }

    internal void HandleLineAmountChanged(InvoicePlanLineViewModel line)
    {
        if (_suppressLineUpdates)
        {
            return;
        }

        try
        {
            _suppressLineUpdates = true;
            RebalanceFromAmountChange(line);
        }
        finally
        {
            _suppressLineUpdates = false;
        }

        RecalculateTotals();
    }

    private void RebalanceFromPercentageChange(InvoicePlanLineViewModel changedLine)
    {
        var editableLines = Items.Where(line => line.IsEditable).ToList();

        if (editableLines.Count == 0)
        {
            return;
        }

        var lockedPercent = Items.Where(line => !line.IsEditable).Sum(line => line.Percentage);
        var lockedAmount = Items.Where(line => !line.IsEditable).Sum(line => line.Amount);

        var availablePercent = Math.Max(0m, 100m - lockedPercent);
        var availableAmount = Math.Max(0m, PlanningBaseValue - lockedAmount);

        var clampedPercent = Math.Clamp(changedLine.Percentage, 0m, availablePercent);
        var roundedPercent = Math.Round(clampedPercent, 4, MidpointRounding.AwayFromZero);
        changedLine.SetPercentage(roundedPercent);

        decimal lineAmount = 0m;
        if (availablePercent > 0m && availableAmount > 0m)
        {
            var ratio = roundedPercent / availablePercent;
            lineAmount = Math.Round(availableAmount * ratio, 2, MidpointRounding.AwayFromZero);
        }

        changedLine.SetAmount(lineAmount);

        var remainingLines = editableLines.Where(line => !ReferenceEquals(line, changedLine)).ToList();

        if (remainingLines.Count == 0)
        {
            return;
        }

        var remainingPercent = Math.Max(0m, availablePercent - changedLine.Percentage);
        var remainingAmount = Math.Max(0m, availableAmount - changedLine.Amount);
        var evenPercent = remainingLines.Count > 0
            ? Math.Round(remainingPercent / remainingLines.Count, 4, MidpointRounding.AwayFromZero)
            : 0m;

        decimal percentAssigned = 0m;
        decimal amountAssigned = 0m;

        for (var index = 0; index < remainingLines.Count; index++)
        {
            var line = remainingLines[index];
            var isLast = index == remainingLines.Count - 1;

            var percent = isLast
                ? Math.Round(remainingPercent - percentAssigned, 4, MidpointRounding.AwayFromZero)
                : evenPercent;

            if (percent < 0m)
            {
                percent = 0m;
            }

            decimal amount;
            if (availablePercent <= 0m || availableAmount <= 0m)
            {
                amount = 0m;
            }
            else if (isLast)
            {
                amount = Math.Round(remainingAmount - amountAssigned, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                var ratio = availablePercent == 0m ? 0m : percent / availablePercent;
                amount = Math.Round(availableAmount * ratio, 2, MidpointRounding.AwayFromZero);
            }

            if (amount < 0m)
            {
                amount = 0m;
            }

            line.SetPercentage(percent);
            line.SetAmount(amount);

            percentAssigned += percent;
            amountAssigned += amount;
        }
    }

    private void RebalanceFromAmountChange(InvoicePlanLineViewModel changedLine)
    {
        var editableLines = Items.Where(line => line.IsEditable).ToList();

        if (editableLines.Count == 0)
        {
            return;
        }

        var lockedPercent = Items.Where(line => !line.IsEditable).Sum(line => line.Percentage);
        var lockedAmount = Items.Where(line => !line.IsEditable).Sum(line => line.Amount);

        var availableAmount = Math.Max(0m, PlanningBaseValue - lockedAmount);
        var availablePercent = Math.Max(0m, 100m - lockedPercent);

        var clampedAmount = Math.Clamp(changedLine.Amount, 0m, availableAmount);
        var roundedAmount = Math.Round(clampedAmount, 2, MidpointRounding.AwayFromZero);
        changedLine.SetAmount(roundedAmount);

        decimal linePercent = 0m;
        if (availableAmount > 0m && availablePercent > 0m)
        {
            var ratio = roundedAmount / availableAmount;
            ratio = Math.Clamp(ratio, 0m, 1m);
            linePercent = Math.Round(availablePercent * ratio, 4, MidpointRounding.AwayFromZero);
        }

        changedLine.SetPercentage(linePercent);

        var remainingLines = editableLines.Where(line => !ReferenceEquals(line, changedLine)).ToList();

        if (remainingLines.Count == 0)
        {
            return;
        }

        var remainingAmount = Math.Max(0m, availableAmount - changedLine.Amount);
        var remainingPercent = Math.Max(0m, availablePercent - changedLine.Percentage);
        var evenAmount = remainingLines.Count > 0
            ? Math.Round(remainingAmount / remainingLines.Count, 2, MidpointRounding.AwayFromZero)
            : 0m;

        decimal amountAssigned = 0m;
        decimal percentAssigned = 0m;

        for (var index = 0; index < remainingLines.Count; index++)
        {
            var line = remainingLines[index];
            var isLast = index == remainingLines.Count - 1;

            var amount = isLast
                ? Math.Round(remainingAmount - amountAssigned, 2, MidpointRounding.AwayFromZero)
                : evenAmount;

            if (amount < 0m)
            {
                amount = 0m;
            }

            decimal percent;
            if (availableAmount <= 0m || availablePercent <= 0m)
            {
                percent = 0m;
            }
            else if (isLast)
            {
                percent = Math.Round(remainingPercent - percentAssigned, 4, MidpointRounding.AwayFromZero);
            }
            else
            {
                var ratio = amount / availableAmount;
                percent = Math.Round(availablePercent * ratio, 4, MidpointRounding.AwayFromZero);
            }

            if (percent < 0m)
            {
                percent = 0m;
            }

            line.SetAmount(amount);
            line.SetPercentage(percent);

            amountAssigned += amount;
            percentAssigned += percent;
        }
    }

    private void EnsureItemCount()
    {
        var desiredEditableCount = Math.Max(NumInvoices, 1);
        var currentEditableCount = Items.Count(line => line.IsEditable);

        while (currentEditableCount > desiredEditableCount)
        {
            var removable = Items.LastOrDefault(line => line.IsEditable);
            if (removable is null)
            {
                break;
            }

            Items.Remove(removable);
            currentEditableCount--;
        }

        while (currentEditableCount < desiredEditableCount)
        {
            Items.Add(CreateNewLine());
            currentEditableCount++;
        }

        UpdateLineTypeSettings();
        DistributePercentages();
        ApplyEmissionDateRule();
        RecalculateDueDates();
        RecalculateTotals();
        EnsureSelectedInvoiceLine();
    }

    private InvoicePlanLineViewModel CreateNewLine()
    {
        var line = new InvoicePlanLineViewModel
        {
            Sequence = Items.Count + 1,
            EmissionDate = FirstEmissionDate ?? DateTime.Today,
            PayerCnpj = string.Empty,
            PoNumber = string.Empty,
            FrsNumber = string.Empty,
            CustomerTicket = string.Empty,
            Status = InvoiceItemStatus.Planned,
        };

        line.SetPaymentType(PaymentTypeCatalog.TransferenciaBancariaCode);
        line.CanEditEmissionDate = line.IsEditable && PlanType != InvoicePlanType.ByDate;
        line.ShowDeliveryDescription = PlanType == InvoicePlanType.ByDelivery;
        line.SetDueDate(CalculateDueDate(line.EmissionDate));

        return line;
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
        var editableLines = Items.Where(line => line.IsEditable).ToList();

        if (editableLines.Count == 0)
        {
            RecalculateTotals();
            return;
        }

        var lockedPercent = Items.Where(line => !line.IsEditable).Sum(line => line.Percentage);
        var lockedAmount = Items.Where(line => !line.IsEditable).Sum(line => line.Amount);

        var remainingPercent = Math.Max(0m, 100m - lockedPercent);
        var remainingAmount = Math.Max(0m, PlanningBaseValue - lockedAmount);

        var evenPercent = editableLines.Count > 0 && remainingPercent > 0
            ? Math.Round(remainingPercent / editableLines.Count, 4, MidpointRounding.AwayFromZero)
            : 0m;

        _suppressLineUpdates = true;
        decimal percentAssigned = 0m;
        decimal amountAssigned = 0m;

        for (var index = 0; index < editableLines.Count; index++)
        {
            var line = editableLines[index];
            var percent = index == editableLines.Count - 1
                ? Math.Round(remainingPercent - percentAssigned, 4, MidpointRounding.AwayFromZero)
                : evenPercent;

            if (percent < 0)
            {
                percent = 0;
            }

            decimal amount;
            if (remainingPercent == 0m || remainingAmount == 0m)
            {
                amount = 0m;
            }
            else if (index == editableLines.Count - 1)
            {
                amount = Math.Round(remainingAmount - amountAssigned, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                amount = Math.Round(remainingAmount * (percent / remainingPercent), 2, MidpointRounding.AwayFromZero);
            }

            if (amount < 0)
            {
                amount = 0;
            }

            line.SetPercentage(percent);
            line.SetAmount(amount);

            percentAssigned += percent;
            amountAssigned += amount;
        }

        _suppressLineUpdates = false;

        RecalculateTotals();
    }

    private void EnsureSelectedInvoiceLine()
    {
        if (Items.Count == 0)
        {
            SelectedInvoiceLine = null;
            return;
        }

        if (SelectedInvoiceLine is not null && Items.Contains(SelectedInvoiceLine))
        {
            return;
        }

        SelectedInvoiceLine = Items.FirstOrDefault(line => line.IsEditable) ?? Items.FirstOrDefault();
    }

    private void OnSelectedLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(InvoicePlanLineViewModel.EmissionDate))
        {
            _previewDescriptionCommand.NotifyCanExecuteChanged();
        }
    }

    private void RecalculateAmountsFromPercentages()
    {
        var editableLines = Items.Where(line => line.IsEditable).ToList();

        if (editableLines.Count == 0)
        {
            RecalculateTotals();
            return;
        }

        var lockedAmount = Items.Where(line => !line.IsEditable).Sum(line => line.Amount);
        var remainingAmount = Math.Max(0m, PlanningBaseValue - lockedAmount);
        var totalEditablePercent = editableLines.Sum(line => line.Percentage);

        _suppressLineUpdates = true;
        decimal amountAssigned = 0m;

        for (var index = 0; index < editableLines.Count; index++)
        {
            var line = editableLines[index];
            decimal amount;

            if (remainingAmount <= 0m || totalEditablePercent <= 0m)
            {
                amount = 0m;
            }
            else if (index == editableLines.Count - 1)
            {
                amount = Math.Round(remainingAmount - amountAssigned, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                var ratio = line.Percentage / totalEditablePercent;
                amount = Math.Round(remainingAmount * ratio, 2, MidpointRounding.AwayFromZero);
            }

            if (amount < 0m)
            {
                amount = 0m;
            }

            line.SetAmount(amount);
            amountAssigned += amount;
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

        var baseEmissionDate = BusinessDayCalculator.AdjustToNextBusinessDay(FirstEmissionDate.Value);
        _suppressLineUpdates = true;
        foreach (var line in Items.Where(line => line.IsEditable))
        {
            var offsetDays = (line.Sequence - 1) * ByDateEmissionIntervalDays;
            var emissionDate = BusinessDayCalculator.AdjustToNextBusinessDay(baseEmissionDate.AddDays(offsetDays));
            line.SetEmissionDate(emissionDate);
        }
        _suppressLineUpdates = false;
    }

    private void RecalculateDueDates()
    {
        foreach (var line in Items)
        {
            UpdateLineDueDate(line);
        }
    }

    private void UpdateLineDueDate(InvoicePlanLineViewModel line)
    {
        var dueDate = CalculateDueDate(line.EmissionDate);
        line.SetDueDate(dueDate);
    }

    private DateTime? CalculateDueDate(DateTime? emissionDate)
    {
        if (emissionDate is null)
        {
            return null;
        }

        var baseDate = emissionDate.Value.AddDays(Math.Max(0, PaymentTermDays));
        return BusinessDayCalculator.AdjustToNextMonday(baseDate);
    }

    private void UpdateLineTypeSettings()
    {
        var isByDate = PlanType == InvoicePlanType.ByDate;
        var isByDelivery = PlanType == InvoicePlanType.ByDelivery;

        foreach (var line in Items)
        {
            line.CanEditEmissionDate = line.IsEditable && !isByDate;
            line.ShowDeliveryDescription = isByDelivery;
        }
    }

    private void RecalculateTotals()
    {
        TotalPercentage = Math.Round(Items.Sum(line => line.Percentage), 4, MidpointRounding.AwayFromZero);
        TotalAmount = Math.Round(Items.Sum(line => line.Amount), 2, MidpointRounding.AwayFromZero);
        UpdateTotalsState();
    }

    private void UpdateTotalsState()
    {
        var expectedAmount = Math.Round(PlanningBaseValue, 2, MidpointRounding.AwayFromZero);
        var totalAmount = Math.Round(TotalAmount, 2, MidpointRounding.AwayFromZero);
        var totalPercent = Math.Round(TotalPercentage, 4, MidpointRounding.AwayFromZero);

        var mismatch = Math.Abs(totalPercent - 100m) > 0.0001m
            || Math.Abs(totalAmount - expectedAmount) > 0.01m;

        HasTotalsMismatch = mismatch;
    }

    private void SavePlan()
    {
        ValidationMessage = null;
        StatusMessage = null;

        if (RequiresFirstEmissionDate && FirstEmissionDate is null)
        {
            ValidationMessage = LocalizationRegistry.Get("InvoicePlan.Validation.Global_FirstEmissionRequired");
            return;
        }

        if (Items.Count == 0)
        {
            ValidationMessage = LocalizationRegistry.Get("INV_InvoicePlan_Validation_NoLines");
            return;
        }

        RebalanceTotals();

        RefreshSequences();
        var plan = BuildPlanModel();
        var validationErrors = _validator.Validate(plan, PlanningBaseValue);

        if (validationErrors.Count > 0)
        {
            ValidationMessage = string.Join(Environment.NewLine, validationErrors.Distinct());
            ToastService.ShowWarning("INV_InvoicePlan_Toast_ValidationFailed");
            return;
        }

        try
        {
            var result = _repository.SavePlan(plan);
            var persisted = _repository.GetPlan(plan.Id) ?? plan;
            ApplyPlan(persisted);

            var action = result.Created == 1
                ? LocalizationRegistry.Get("INV_InvoicePlan_Status_ActionCreated")
                : LocalizationRegistry.Get("INV_InvoicePlan_Status_ActionUpdated");
            StatusMessage = LocalizationRegistry.Format(
                "INV_InvoicePlan_Status_PlanSaved",
                PlanId,
                action,
                Items.Count,
                result.AffectedRows,
                result.Deleted);
            ToastService.ShowSuccess("INV_InvoicePlan_Toast_PlanSaved", PlanId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save invoice plan {PlanId}.", plan.Id);
            ValidationMessage = LocalizationRegistry.Format("INV_InvoicePlan_Status_SaveFailure", ex.Message);
            ToastService.ShowError("INV_InvoicePlan_Toast_SaveFailed");
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
            CustomInstructions = string.IsNullOrWhiteSpace(CustomInstructions) ? null : CustomInstructions!.Trim(),
            AdditionalDetails = string.IsNullOrWhiteSpace(AdditionalDetails) ? null : AdditionalDetails!.Trim(),
            FirstEmissionDate = FirstEmissionDate,
        };

        var recipients = ParseRecipientEmails(RecipientEmails);
        plan.CustomerFocalPointEmail = recipients.FirstOrDefault() ?? string.Empty;

        foreach (var line in Items.OrderBy(line => line.Sequence))
        {
            var emissionDate = line.EmissionDate;
            var dueDate = CalculateDueDate(emissionDate);

            var item = new InvoiceItem
            {
                Id = line.Id,
                PlanId = PlanId,
                SeqNo = line.Sequence,
                Percentage = Math.Round(line.Percentage, 4, MidpointRounding.AwayFromZero),
                Amount = Math.Round(line.Amount, 2, MidpointRounding.AwayFromZero),
                EmissionDate = emissionDate,
                DueDate = dueDate,
                DeliveryDescription = string.IsNullOrWhiteSpace(line.DeliveryDescription) ? null : line.DeliveryDescription.Trim(),
                PayerCnpj = line.PayerCnpj?.Trim() ?? string.Empty,
                PoNumber = string.IsNullOrWhiteSpace(line.PoNumber) ? null : line.PoNumber.Trim(),
                FrsNumber = string.IsNullOrWhiteSpace(line.FrsNumber) ? null : line.FrsNumber.Trim(),
                CustomerTicket = string.IsNullOrWhiteSpace(line.CustomerTicket) ? null : line.CustomerTicket.Trim(),
                AdditionalInfo = string.IsNullOrWhiteSpace(line.AdditionalDetails) ? null : line.AdditionalDetails.Trim(),
                PaymentTypeCode = PaymentTypeCatalog.NormalizeCode(line.PaymentTypeCode),
            };

            plan.Items.Add(item);
        }

        var additionalRecipients = recipients.Skip(1)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var email in additionalRecipients)
        {
            plan.AdditionalEmails.Add(new InvoicePlanEmail
            {
                PlanId = PlanId,
                Email = email,
            });
        }

        return plan;
    }

    private static string BuildRecipientList(InvoicePlan plan)
    {
        var recipients = new List<string>();

        if (!string.IsNullOrWhiteSpace(plan.CustomerFocalPointEmail))
        {
            recipients.Add(plan.CustomerFocalPointEmail.Trim());
        }

        foreach (var email in plan.AdditionalEmails)
        {
            var trimmed = email.Email?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                recipients.Add(trimmed);
            }
        }

        return string.Join(Environment.NewLine, recipients.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static List<string> ParseRecipientEmails(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new List<string>();
        }

        var separators = new[] { ';', '\n', '\r', ',' };
        return input
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void UpdateCurrencySymbol(string? currencyCode)
    {
        var formatInfo = CurrencyDisplayHelper.Resolve(currencyCode);
        CurrencySymbol = formatInfo.Symbol;
    }

    private void LoadEngagements()
    {
        try
        {
            var engagements = _repository.ListEngagementsForPlanning();
            var previousSelection = SelectedEngagement?.EngagementId;

            Engagements.Clear();
            foreach (var engagement in engagements)
            {
                Engagements.Add(new EngagementOptionViewModel(engagement));
            }

            var selected = engagements.Count == 0
                ? null
                : Engagements.FirstOrDefault(option =>
                    string.Equals(option.EngagementId, previousSelection, StringComparison.OrdinalIgnoreCase))
                    ?? Engagements.FirstOrDefault();

            SelectedEngagement = selected;

            EngagementSelectionMessage = engagements.Count == 0
                ? LocalizationRegistry.Get("INV_InvoicePlan_Selection_Message_Empty")
                : LocalizationRegistry.Get("INV_InvoicePlan_Selection_Message_SelectHint");

            if (_accessScope.IsInitialized && !_accessScope.HasAssignments && string.IsNullOrWhiteSpace(_accessScope.InitializationError))
            {
                EngagementSelectionMessage = LocalizationRegistry.Format("INV_Access_Message_NoAssignments", GetLoginDisplay(_accessScope));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load engagements for planning.");
            Engagements.Clear();
            SelectedEngagement = null;
            EngagementSelectionMessage = LocalizationRegistry.Format("INV_InvoicePlan_Selection_Status_LoadFailure", ex.Message);
        }
    }

    private bool TryOpenExistingPlan(EngagementOptionViewModel engagement)
    {
        try
        {
            var plans = _repository.ListPlansForEngagement(engagement.EngagementId);
            var existing = plans
                .OrderByDescending(plan => plan.UpdatedAt)
                .ThenByDescending(plan => plan.CreatedAt)
                .FirstOrDefault();

            return existing is not null && LoadPlan(existing.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load existing invoice plan for engagement {EngagementId}.",
                engagement.EngagementId);
            ValidationMessage = LocalizationRegistry.Format("INV_InvoicePlan_Status_LoadFailure", ex.Message);
            return false;
        }
    }

    private bool CanCreatePlan() => SelectedEngagement is not null;

    private bool CanExecuteSavePlan() => !HasTotalsMismatch && Items.Count > 0;

    private bool CanDeletePlan() => PlanId > 0;

    private void CreatePlan()
    {
        if (SelectedEngagement is null)
        {
            return;
        }

        if (TryOpenExistingPlan(SelectedEngagement))
        {
            return;
        }

        ResetPlanEditor();

        EngagementId = SelectedEngagement.EngagementId;
        EngagementName = SelectedEngagement.Name;
        ShowPlanDialog();
    }

    private void ResetPlanEditor()
    {
        _isInitializing = true;

        try
        {
            PlanId = 0;
            PlanType = InvoicePlanType.ByDate;
            _numInvoices = 1;
            OnPropertyChanged(nameof(NumInvoices));
            PaymentTermDays = 0;
            PlanningBaseValue = 0m;
            FirstEmissionDate = DateTime.Today;
            CustomerFocalPointName = string.Empty;
            CustomInstructions = string.Empty;
            AdditionalDetails = string.Empty;
            RecipientEmails = string.Empty;
            Items.Clear();
            ValidationMessage = null;
            StatusMessage = null;
            SelectedInvoiceLine = null;
        }
        finally
        {
            _isInitializing = false;
        }

        EnsureItemCount();
        RecalculateTotals();
        OnPropertyChanged(nameof(HasRecipientEmails));
        UpdateCurrencySymbol(SelectedEngagement?.Currency);
        _previewDescriptionCommand.NotifyCanExecuteChanged();
    }

    private void ShowPlanDialog()
    {
        _dialogViewModel ??= new PlanEditorDialogViewModel(this);
        _ = _dialogService.ShowDialogAsync(_dialogViewModel, LocalizationRegistry.Get("INV_InvoicePlan_Title_Primary"));
    }

    private void ClosePlanForm()
    {
        Messenger.Send(new CloseDialogMessage(false));
    }

    private void ShowInvoiceLinesDialog()
    {
        _dialogViewModel ??= new PlanEditorDialogViewModel(this);
        _dialogViewModel.NavigateToInvoiceItems();
        Messenger.Send(new RefreshViewMessage(RefreshTargets.InvoiceLinesGrid));
    }

    private void DeletePlan()
    {
        var currentPlanId = PlanId;

        if (currentPlanId <= 0)
        {
            return;
        }

        ValidationMessage = null;
        StatusMessage = null;

        try
        {
            var result = _repository.DeletePlan(currentPlanId);

            if (result.Deleted == 0)
            {
                ValidationMessage = LocalizationRegistry.Format("INV_InvoicePlan_Validation_PlanNotFound", currentPlanId);
                ToastService.ShowWarning("INV_InvoicePlan_Toast_DeleteMissing", currentPlanId);
                return;
            }

            var selectedEngagement = SelectedEngagement;

            ResetPlanEditor();
            Messenger.Send(new CloseDialogMessage(true));

            LoadEngagements();
            if (selectedEngagement is not null)
            {
                SelectedEngagement = Engagements.FirstOrDefault(option =>
                    string.Equals(option.EngagementId, selectedEngagement.EngagementId, StringComparison.OrdinalIgnoreCase));
            }

            EngagementSelectionMessage = LocalizationRegistry.Format("INV_InvoicePlan_Selection_Status_PlanDeleted", currentPlanId);
            ToastService.ShowSuccess("INV_InvoicePlan_Toast_PlanDeleted", currentPlanId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete invoice plan {PlanId}.", currentPlanId);
            ValidationMessage = LocalizationRegistry.Format("INV_InvoicePlan_Status_DeleteFailure", ex.Message);
            ToastService.ShowError("INV_InvoicePlan_Toast_DeleteFailed");
        }
        finally
        {
            _deletePlanCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanPreviewInvoiceDescription()
    {
        return SelectedInvoiceLine is { EmissionDate: not null };
    }

    private void PreviewInvoiceDescription()
    {
        var line = SelectedInvoiceLine;

        if (line is null || line.EmissionDate is null)
        {
            return;
        }

        var recipients = ParseRecipientEmails(RecipientEmails);
        var primaryRecipient = recipients.FirstOrDefault();

        var emissionDate = line.EmissionDate.Value;
        var dueDate = CalculateDueDate(line.EmissionDate) ?? emissionDate;

        var engagementDescription = SelectedEngagement?.Name;
        if (string.IsNullOrWhiteSpace(engagementDescription))
        {
            engagementDescription = string.IsNullOrWhiteSpace(EngagementName)
                ? EngagementId
                : EngagementName;
        }

        var context = new InvoiceDescriptionContext
        {
            EngagementId = string.IsNullOrWhiteSpace(EngagementId)
                ? SelectedEngagement?.EngagementId ?? string.Empty
                : EngagementId,
            EngagementDescription = engagementDescription,
            Sequence = line.Sequence,
            TotalInvoices = NumInvoices,
            DueDate = dueDate,
            Amount = Math.Round(line.Amount, 2, MidpointRounding.AwayFromZero),
            CurrencyCode = SelectedEngagement?.Currency,
            PlanType = PlanType,
            DeliveryDescription = line.DeliveryDescription,
            PoNumber = line.PoNumber,
            FrsNumber = line.FrsNumber,
            CustomerTicket = line.CustomerTicket,
            LineAdditionalDetails = string.IsNullOrWhiteSpace(line.AdditionalDetails)
                ? null
                : line.AdditionalDetails.Trim(),
            CustomerName = SelectedEngagement?.CustomerName,
            CustomerFocalPointName = CustomerFocalPointName,
            CustomerFocalPointEmail = primaryRecipient,
            CoeResponsible = null,
            PlanAdditionalDetails = string.IsNullOrWhiteSpace(AdditionalDetails)
                ? null
                : AdditionalDetails.Trim(),
            CustomerEmails = recipients
        };

        var description = InvoiceDescriptionFormatter.Format(context);

        var previewViewModel = new InvoiceDescriptionPreviewViewModel(
            LocalizationRegistry.Get("INV_InvoicePlan_Preview_Title"),
            description,
            line.Sequence,
            NumInvoices);

        _ = _dialogService.ShowDialogAsync(
            previewViewModel,
            LocalizationRegistry.Get("INV_InvoicePlan_Preview_DialogTitle"));
    }

    private void AdjustLastLineForTotals()
    {
        var editableLines = Items.Where(line => line.IsEditable).ToList();

        if (editableLines.Count == 0)
        {
            return;
        }

        var lockedPercent = Items.Where(line => !line.IsEditable).Sum(line => line.Percentage);
        var lockedAmount = Items.Where(line => !line.IsEditable).Sum(line => line.Amount);

        var expectedAmount = Math.Max(0m, PlanningBaseValue - lockedAmount);
        var expectedPercent = Math.Max(0m, 100m - lockedPercent);

        if (editableLines.Count == 1)
        {
            _suppressLineUpdates = true;
            var onlyLine = editableLines[0];
            onlyLine.SetPercentage(Math.Round(expectedPercent, 4, MidpointRounding.AwayFromZero));
            onlyLine.SetAmount(Math.Round(expectedAmount, 2, MidpointRounding.AwayFromZero));
            _suppressLineUpdates = false;
            return;
        }

        var percentExceptLast = editableLines.Take(editableLines.Count - 1).Sum(line => line.Percentage);
        var amountExceptLast = editableLines.Take(editableLines.Count - 1).Sum(line => line.Amount);
        var lastLine = editableLines[^1];

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
