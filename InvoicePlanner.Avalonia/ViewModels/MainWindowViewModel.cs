using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PlanEditorViewModel _planEditor;
    private readonly RequestConfirmationViewModel _requestConfirmation;
    private readonly EmissionConfirmationViewModel _emissionConfirmation;
    private readonly ConnectionSettingsViewModel _connectionSettings;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly Dictionary<string, NavigationItemViewModel> _navigationIndex = new(StringComparer.Ordinal);
    private string? _lastPersistedMenuKey;

    [ObservableProperty]
    private string title = LocalizationRegistry.Get("Shell.Title.InvoicePlanner");

    [ObservableProperty]
    private ViewModelBase currentViewModel;

    public IReadOnlyList<NavigationItemViewModel> MenuItems { get; }

    public MainWindowViewModel(
        PlanEditorViewModel planEditor,
        RequestConfirmationViewModel requestConfirmation,
        EmissionConfirmationViewModel emissionConfirmation,
        ConnectionSettingsViewModel connectionSettings,
        ISettingsService settingsService,
        IMessenger messenger,
        ILogger<MainWindowViewModel> logger)
        : base(messenger)
    {
        _planEditor = planEditor;
        _requestConfirmation = requestConfirmation;
        _emissionConfirmation = emissionConfirmation;
        _connectionSettings = connectionSettings;
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var items = new List<NavigationItemViewModel>
        {
            CreateNavigationItem(NavigationKeys.PlanEditor, LocalizationRegistry.Get("Shell.Navigation.InvoicePlan"), _planEditor),
            CreateNavigationItem(NavigationKeys.RequestConfirmation, LocalizationRegistry.Get("Shell.Navigation.ConfirmRequest"), _requestConfirmation),
            CreateNavigationItem(NavigationKeys.EmissionConfirmation, LocalizationRegistry.Get("Shell.Navigation.ConfirmEmission"), _emissionConfirmation),
            CreateNavigationItem(NavigationKeys.ConnectionSettings, LocalizationRegistry.Get("Shell.Navigation.ConnectionSettings"), _connectionSettings)
        };

        MenuItems = items;
        foreach (var item in items)
        {
            _navigationIndex[item.Key] = item;
        }

        var settings = _settingsService.GetAll();
        NavigationItemViewModel? storedSelection = null;
        if (settings.TryGetValue(SettingKeys.LastInvoicePlannerSectionKey, out var storedKey)
            && !string.IsNullOrWhiteSpace(storedKey)
            && _navigationIndex.TryGetValue(storedKey, out var storedItem))
        {
            _lastPersistedMenuKey = storedKey;
            storedSelection = storedItem;
        }

        var isConfigured = settings.TryGetValue(SettingKeys.Server, out var server) && !string.IsNullOrWhiteSpace(server)
            && settings.TryGetValue(SettingKeys.Database, out var database) && !string.IsNullOrWhiteSpace(database)
            && settings.TryGetValue(SettingKeys.User, out var user) && !string.IsNullOrWhiteSpace(user)
            && settings.TryGetValue(SettingKeys.Password, out var password) && !string.IsNullOrWhiteSpace(password);

        if (isConfigured)
        {
            var initialItem = storedSelection ?? items.First();
            currentViewModel = initialItem.TargetViewModel;
            Activate(initialItem);
        }
        else
        {
            currentViewModel = _connectionSettings;
            _connectionSettings.StatusMessage = LocalizationRegistry.Get("Connection.Message.SetupPrompt");
            Activate(_navigationIndex[NavigationKeys.ConnectionSettings]);
        }
    }

    private NavigationItemViewModel CreateNavigationItem(string key, string title, ViewModelBase viewModel)
    {
        return new NavigationItemViewModel(key, title, viewModel, Activate);
    }

    private void Activate(NavigationItemViewModel selectedItem)
    {
        if (selectedItem?.TargetViewModel is null)
        {
            return;
        }

        CurrentViewModel = selectedItem.TargetViewModel;
        foreach (var item in MenuItems)
        {
            item.IsSelected = ReferenceEquals(item, selectedItem);
        }

        _ = PersistNavigationSelectionAsync(selectedItem);
    }

    private async Task PersistNavigationSelectionAsync(NavigationItemViewModel? selectedItem)
    {
        var key = selectedItem?.Key ?? string.Empty;
        if (string.Equals(_lastPersistedMenuKey, key, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var settings = await _settingsService.GetAllAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(key))
            {
                if (settings.Remove(SettingKeys.LastInvoicePlannerSectionKey))
                {
                    await _settingsService.SaveAllAsync(settings).ConfigureAwait(false);
                    _lastPersistedMenuKey = key;
                }

                return;
            }

            settings[SettingKeys.LastInvoicePlannerSectionKey] = key;
            await _settingsService.SaveAllAsync(settings).ConfigureAwait(false);
            _lastPersistedMenuKey = key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist the navigation selection.");
        }
    }

    private static class NavigationKeys
    {
        public const string PlanEditor = "PlanEditor";
        public const string RequestConfirmation = "RequestConfirmation";
        public const string EmissionConfirmation = "EmissionConfirmation";
        public const string ConnectionSettings = "ConnectionSettings";
    }
}
