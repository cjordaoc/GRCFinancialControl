using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Invoices.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class NotificationPreviewViewModel : ViewModelBase
{
    private readonly IInvoicePlanRepository _repository;
    private readonly ILogger<NotificationPreviewViewModel> _logger;
    private readonly IInvoiceAccessScope _accessScope;
    private bool _isInitialised;

    public NotificationPreviewViewModel(
        IInvoicePlanRepository repository,
        ILogger<NotificationPreviewViewModel> logger,
        IInvoiceAccessScope accessScope)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accessScope = accessScope ?? throw new ArgumentNullException(nameof(accessScope));

        _accessScope.EnsureInitialized();

        Items = new ObservableCollection<NotificationPreviewItemViewModel>();
        Items.CollectionChanged += OnItemsCollectionChanged;
        RefreshCommand = new RelayCommand(Refresh, () => !IsBusy);

        SelectedDate = DateTime.Today;
        _isInitialised = true;
        Refresh();
    }

    public ObservableCollection<NotificationPreviewItemViewModel> Items { get; }

    [ObservableProperty]
    private DateTime selectedDate;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string? validationMessage;

    [ObservableProperty]
    private bool isBusy;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool IsEmpty => Items.Count == 0;

    public IRelayCommand RefreshCommand { get; }

    partial void OnStatusMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    partial void OnValidationMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasValidationMessage));
    }

    partial void OnIsBusyChanged(bool value)
    {
        (RefreshCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        if (_isInitialised)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (IsBusy)
        {
            return;
        }

        ResetMessages();

        if (_accessScope.IsInitialized && !_accessScope.HasAssignments && string.IsNullOrWhiteSpace(_accessScope.InitializationError))
        {
            Items.Clear();
            StatusMessage = LocalizationRegistry.Format("Access.Message.NoAssignments", GetLoginDisplay(_accessScope));
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        try
        {
            IsBusy = true;
            var previews = _repository.PreviewNotifications(SelectedDate);

            Items.Clear();
            foreach (var preview in previews)
            {
                Items.Add(new NotificationPreviewItemViewModel(preview));
            }

            StatusMessage = previews.Count == 0
                ? LocalizationRegistry.Format("Notifications.Status.Empty", SelectedDate)
                : LocalizationRegistry.Format("Notifications.Status.Loaded", previews.Count, SelectedDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notification preview for {Date}", SelectedDate);
            ValidationMessage = LocalizationRegistry.Format("Notifications.Status.LoadFailure", ex.Message);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private void ResetMessages()
    {
        StatusMessage = null;
        ValidationMessage = null;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
    }
}
