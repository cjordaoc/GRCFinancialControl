
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoicePlanner.Avalonia.Services;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class InvoiceSummaryViewModel : ViewModelBase
{
    private readonly IInvoicePlanRepository _repository;
    private readonly InvoiceSummaryExporter _exporter;
    private readonly ILogger<InvoiceSummaryViewModel> _logger;
    private readonly IInvoiceAccessScope _accessScope;
    private readonly Dictionary<string, EngagementFilterOption> _engagementIndex = new();
    private readonly Dictionary<int, CustomerFilterOption> _customerIndex = new();
    private InvoiceSummaryResult _latestResult = new();

    public InvoiceSummaryViewModel(
        IInvoicePlanRepository repository,
        InvoiceSummaryExporter exporter,
        ILogger<InvoiceSummaryViewModel> logger,
        IInvoiceAccessScope accessScope)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accessScope = accessScope ?? throw new ArgumentNullException(nameof(accessScope));

        _accessScope.EnsureInitialized();

        Groups = new ObservableCollection<InvoiceSummaryGroupViewModel>();
        Engagements = new ObservableCollection<EngagementFilterOption>();
        Customers = new ObservableCollection<CustomerFilterOption>();
        StatusFilters = new ObservableCollection<InvoiceSummaryStatusOption>();

        var allEngagements = new EngagementFilterOption(null, LocalizationRegistry.Get("INV_InvoiceSummary_Filter_AllEngagements"));
        Engagements.Add(allEngagements);
        var allCustomers = new CustomerFilterOption(null, LocalizationRegistry.Get("INV_InvoiceSummary_Filter_AllCustomers"));
        Customers.Add(allCustomers);
        SelectedEngagement = allEngagements;
        SelectedCustomer = allCustomers;

        foreach (var status in Enum.GetValues<InvoiceItemStatus>())
        {
            var option = new InvoiceSummaryStatusOption(status, status is not InvoiceItemStatus.Reissued)
            {
                DisplayName = LocalizationRegistry.Get(GetStatusResourceKey(status)),
            };
            option.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasStatusSelection));
            StatusFilters.Add(option);
        }

        RefreshCommand = new RelayCommand(RefreshSummary);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ExportExcelCommand = new RelayCommand(ExportExcel, () => _latestResult.Groups.Count > 0);
        ExportPdfCommand = new RelayCommand(ExportPdf, () => _latestResult.Groups.Count > 0);

        RefreshSummary();
    }

    public ObservableCollection<InvoiceSummaryGroupViewModel> Groups { get; }

    public ObservableCollection<InvoiceSummaryStatusOption> StatusFilters { get; }

    public ObservableCollection<EngagementFilterOption> Engagements { get; }

    public ObservableCollection<CustomerFilterOption> Customers { get; }

    [ObservableProperty]
    private EngagementFilterOption? selectedEngagement;

    [ObservableProperty]
    private CustomerFilterOption? selectedCustomer;

    [ObservableProperty]
    private decimal totalAmount;

    [ObservableProperty]
    private decimal totalPercentage;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string? validationMessage;

    public bool HasStatusSelection => StatusFilters.Any(option => option.IsSelected);

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public string TotalAmountDisplay => CurrencyDisplayHelper.Format(TotalAmount, null);

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand ClearFiltersCommand { get; }

    public IRelayCommand ExportExcelCommand { get; }

    public IRelayCommand ExportPdfCommand { get; }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    partial void OnValidationMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasValidationMessage));
    }

    partial void OnTotalAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(TotalAmountDisplay));
    }

    private void RefreshSummary()
    {
        ResetMessages();

        if (!HasStatusSelection)
        {
            ValidationMessage = LocalizationRegistry.Get("INV_InvoiceSummary_Validation_StatusRequired");
            return;
        }

        if (_accessScope.IsInitialized && !_accessScope.HasAssignments && string.IsNullOrWhiteSpace(_accessScope.InitializationError))
        {
            Groups.Clear();
            TotalAmount = 0m;
            TotalPercentage = 0m;
            StatusMessage = LocalizationRegistry.Format("INV_Access_Message_NoAssignments", GetLoginDisplay(_accessScope));
            (ExportExcelCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (ExportPdfCommand as RelayCommand)?.NotifyCanExecuteChanged();
            return;
        }

        var statuses = StatusFilters
            .Where(option => option.IsSelected)
            .Select(option => option.Status)
            .ToArray();

        var filter = new InvoiceSummaryFilter
        {
            EngagementId = SelectedEngagement?.EngagementId,
            CustomerId = SelectedCustomer?.CustomerId,
            Statuses = statuses,
        };

        try
        {
            var result = _repository.SearchSummary(filter);
            _latestResult = result;
            ApplyResult(result);
            UpdateFilterCollections(result);

            StatusMessage = result.Groups.Count == 0
                ? LocalizationRegistry.Get("INV_InvoiceSummary_Status_Empty")
                : LocalizationRegistry.Format(
                    "INV_InvoiceSummary_Status_Loaded",
                    result.Groups.Sum(group => group.Items.Count),
                    result.Groups.Count);

            (ExportExcelCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (ExportPdfCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load invoice summary.");
            ValidationMessage = LocalizationRegistry.Format("INV_InvoiceSummary_Status_LoadFailure", ex.Message);
        }
    }

    private void ApplyResult(InvoiceSummaryResult result)
    {
        Groups.Clear();

        foreach (var group in result.Groups)
        {
            Groups.Add(new InvoiceSummaryGroupViewModel(group));
        }

        TotalAmount = result.TotalAmount;
        TotalPercentage = result.TotalPercentage;
    }

    private void UpdateFilterCollections(InvoiceSummaryResult result)
    {
        foreach (var group in result.Groups)
        {
            if (!_engagementIndex.ContainsKey(group.EngagementId))
            {
                var option = new EngagementFilterOption(group.EngagementId, $"{group.EngagementName} ({group.EngagementId})");
                _engagementIndex[group.EngagementId] = option;
                Engagements.Add(option);
            }

            if (group.CustomerId.HasValue)
            {
                var displayName = string.IsNullOrWhiteSpace(group.CustomerCode)
                    ? group.CustomerName ?? LocalizationRegistry.Get("INV_InvoiceSummary_Value_UnknownCustomer")
                    : $"{group.CustomerName} ({group.CustomerCode})";
                var option = new CustomerFilterOption(group.CustomerId, displayName);
                if (!_customerIndex.ContainsKey(group.CustomerId.Value))
                {
                    _customerIndex[group.CustomerId.Value] = option;
                    Customers.Add(option);
                }
            }
        }
    }

    private void ClearFilters()
    {
        ResetMessages();

        foreach (var option in StatusFilters)
        {
            option.IsSelected = option.Status is not InvoiceItemStatus.Reissued;
        }

        SelectedEngagement = Engagements.FirstOrDefault();
        SelectedCustomer = Customers.FirstOrDefault();

        RefreshSummary();
    }

    private void ExportExcel()
    {
        ExportToFile("InvoiceSummary", "xlsx", _exporter.CreateExcel);
    }

    private void ExportPdf()
    {
        ExportToFile("InvoiceSummary", "pdf", _exporter.CreatePdf);
    }

    private void ExportToFile(string entity, string extension, Func<InvoiceSummaryResult, byte[]> generator)
    {
        ResetMessages();

        if (_latestResult.Groups.Count == 0)
        {
            ValidationMessage = LocalizationRegistry.Get("INV_InvoiceSummary_Validation_NoDataToExport");
            return;
        }

        try
        {
            var directory = EnsureExportDirectory();
            var fileName = _exporter.BuildFileName(entity, extension);
            var fullPath = Path.Combine(directory, fileName);
            var content = generator(_latestResult);
            File.WriteAllBytes(fullPath, content);
            StatusMessage = LocalizationRegistry.Format("INV_InvoiceSummary_Status_ExportSuccess", entity, fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export {Entity} summary.", entity);
            ValidationMessage = LocalizationRegistry.Format("INV_InvoiceSummary_Status_ExportFailure", ex.Message);
        }
    }

    private static string GetStatusResourceKey(InvoiceItemStatus status)
    {
        return status switch
        {
            InvoiceItemStatus.Planned => "INV_Invoice_Status_Planned",
            InvoiceItemStatus.Requested => "INV_Invoice_Status_Requested",
            InvoiceItemStatus.Emitted => "INV_Invoice_Status_Emitted",
            InvoiceItemStatus.Closed => "INV_Invoice_Status_Closed",
            InvoiceItemStatus.Canceled => "INV_Invoice_Status_Canceled",
            InvoiceItemStatus.Reissued => "INV_Invoice_Status_Reissued",
            _ => status.ToString(),
        };
    }

    private static string EnsureExportDirectory()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var directory = Path.Combine(documents, "InvoicePlanner", "Exports");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private void ResetMessages()
    {
        StatusMessage = null;
        ValidationMessage = null;
    }

    public sealed class EngagementFilterOption
    {
        public EngagementFilterOption(string? engagementId, string displayName)
        {
            EngagementId = engagementId;
            DisplayName = displayName;
        }

        public string? EngagementId { get; }

        public string DisplayName { get; }
    }

    public sealed class CustomerFilterOption
    {
        public CustomerFilterOption(int? customerId, string displayName)
        {
            CustomerId = customerId;
            DisplayName = displayName;
        }

        public int? CustomerId { get; }

        public string DisplayName { get; }
    }
}
