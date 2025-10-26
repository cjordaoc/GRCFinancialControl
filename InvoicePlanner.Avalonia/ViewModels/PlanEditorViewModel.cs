using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InvoicePlanner.Avalonia.Messages;
using InvoicePlanner.Avalonia.Services;
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
    private readonly IInvoiceAccessScope _accessScope;
    private readonly DialogService _dialogService;
    private bool _suppressLineUpdates;
    private bool _isInitializing;
    private PlanEditorDialogViewModel? _dialogViewModel;

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

        SavePlanCommand = new RelayCommand(SavePlan);
        EditLinesCommand = new RelayCommand(ShowInvoiceLinesDialog);
        CreatePlanCommand = new RelayCommand(CreatePlan, CanCreatePlan);
        ClosePlanFormCommand = new RelayCommand(() => Messenger.Send(new CloseDialogMessage(false)));
        RefreshCommand = new RelayCommand(() =>
        {
            LoadEngagements();
        });

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
    private string? statusMessage;

    [ObservableProperty]
    private string? validationMessage;

    private int _numInvoices = 1;

    [ObservableProperty]
    private EngagementOptionViewModel? selectedEngagement;

    [ObservableProperty]
    private string? engagementSelectionMessage;

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

    public IRelayCommand ClosePlanFormCommand { get; }

    public IRelayCommand RefreshCommand { get; }

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

    partial void OnSelectedEngagementChanged(EngagementOptionViewModel? value)
    {
        CreatePlanCommand.NotifyCanExecuteChanged();
    }

    partial void OnRecipientEmailsChanged(string? value)
    {
        OnPropertyChanged(nameof(HasRecipientEmails));
    }

    public IRelayCommand SavePlanCommand { get; }
    public IRelayCommand EditLinesCommand { get; }

    public IRelayCommand CreatePlanCommand { get; }

    public bool LoadPlan(int planId)
    {
        ValidationMessage = null;
        StatusMessage = null;

        try
        {
            var plan = _repository.GetPlan(planId);
            if (plan is null)
            {
                ValidationMessage = LocalizationRegistry.Format("InvoicePlan.Validation.PlanNotFound", planId);
                return false;
            }

            ApplyPlan(plan);
            StatusMessage = LocalizationRegistry.Format("InvoicePlan.Status.PlanLoaded", planId, Items.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load invoice plan {PlanId}.", planId);
            ValidationMessage = LocalizationRegistry.Format("InvoicePlan.Status.LoadFailure", ex.Message);
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
                };

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
            RecalculateTotals();
            OnPropertyChanged(nameof(RequiresFirstEmissionDate));
            OnPropertyChanged(nameof(HasRecipientEmails));
            ShowPlanDialog();
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
                PayerCnpj = string.Empty,
                PoNumber = string.Empty,
                FrsNumber = string.Empty,
                CustomerTicket = string.Empty,
                Status = InvoiceItemStatus.Planned,
            };

            line.CanEditEmissionDate = line.IsEditable && PlanType != InvoicePlanType.ByDate;
            line.ShowDeliveryDescription = PlanType == InvoicePlanType.ByDelivery;

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
            line.CanEditEmissionDate = line.IsEditable && !isByDate;
            line.ShowDeliveryDescription = isByDelivery;
        }
    }

    private void RecalculateTotals()
    {
        TotalPercentage = Math.Round(Items.Sum(line => line.Percentage), 4, MidpointRounding.AwayFromZero);
        TotalAmount = Math.Round(Items.Sum(line => line.Amount), 2, MidpointRounding.AwayFromZero);
    }

    private void SavePlan()
    {
        ValidationMessage = null;
        StatusMessage = null;

        if (RequiresFirstEmissionDate && FirstEmissionDate is null)
        {
            ValidationMessage = LocalizationRegistry.Get("InvoicePlan.Validation.FirstEmissionRequired");
            return;
        }

        if (Items.Count == 0)
        {
            ValidationMessage = LocalizationRegistry.Get("InvoicePlan.Validation.NoLines");
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
                ? LocalizationRegistry.Get("InvoicePlan.Status.ActionCreated")
                : LocalizationRegistry.Get("InvoicePlan.Status.ActionUpdated");
            StatusMessage = LocalizationRegistry.Format(
                "InvoicePlan.Status.PlanSaved",
                PlanId,
                action,
                Items.Count,
                result.AffectedRows,
                result.Deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save invoice plan {PlanId}.", plan.Id);
            ValidationMessage = LocalizationRegistry.Format("InvoicePlan.Status.SaveFailure", ex.Message);
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
            FirstEmissionDate = FirstEmissionDate,
        };

        var recipients = ParseRecipientEmails(RecipientEmails);
        plan.CustomerFocalPointEmail = recipients.FirstOrDefault() ?? string.Empty;

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
                PayerCnpj = line.PayerCnpj?.Trim() ?? string.Empty,
                PoNumber = string.IsNullOrWhiteSpace(line.PoNumber) ? null : line.PoNumber.Trim(),
                FrsNumber = string.IsNullOrWhiteSpace(line.FrsNumber) ? null : line.FrsNumber.Trim(),
                CustomerTicket = string.IsNullOrWhiteSpace(line.CustomerTicket) ? null : line.CustomerTicket.Trim(),
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
                ? LocalizationRegistry.Get("InvoicePlan.Selection.Message.Empty")
                : LocalizationRegistry.Get("InvoicePlan.Selection.Message.SelectHint");

            if (_accessScope.IsInitialized && !_accessScope.HasAssignments && string.IsNullOrWhiteSpace(_accessScope.InitializationError))
            {
                EngagementSelectionMessage = LocalizationRegistry.Format("Access.Message.NoAssignments", GetLoginDisplay(_accessScope));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load engagements for planning.");
            Engagements.Clear();
            SelectedEngagement = null;
            EngagementSelectionMessage = LocalizationRegistry.Format("InvoicePlan.Selection.Status.LoadFailure", ex.Message);
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
            ValidationMessage = LocalizationRegistry.Format("InvoicePlan.Status.LoadFailure", ex.Message);
            return false;
        }
    }

    private bool CanCreatePlan() => SelectedEngagement is not null;

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
            RecipientEmails = string.Empty;
            Items.Clear();
            ValidationMessage = null;
            StatusMessage = null;
        }
        finally
        {
            _isInitializing = false;
        }

        EnsureItemCount();
        RecalculateTotals();
        OnPropertyChanged(nameof(HasRecipientEmails));
    }

    private void ShowPlanDialog()
    {
        _dialogViewModel ??= new PlanEditorDialogViewModel(this);
        _ = _dialogService.ShowDialogAsync(_dialogViewModel, LocalizationRegistry.Get("InvoicePlan.Title.Primary"));
    }

    private void ShowInvoiceLinesDialog()
    {
        var invoiceLinesEditorViewModel = new InvoiceLinesEditorViewModel(this);
        _ = _dialogService.ShowDialogAsync(invoiceLinesEditorViewModel, LocalizationRegistry.Get("InvoicePlan.Section.InvoiceLines.Title"));
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
