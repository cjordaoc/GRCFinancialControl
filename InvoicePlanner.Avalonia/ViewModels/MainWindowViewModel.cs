using System;
using System.Collections.Generic;
using System.Linq;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;
using InvoicePlanner.Avalonia.Messages;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PlanEditorViewModel _planEditor;
    private readonly RequestConfirmationViewModel _requestConfirmation;
    private readonly EmissionConfirmationViewModel _emissionConfirmation;
    private readonly ConnectionSettingsViewModel _connectionSettings;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    private string title = LocalizationRegistry.Get("Shell.Title.Application");

    [ObservableProperty]
    private ViewModelBase currentViewModel;

    [ObservableProperty]
    private object? modalOverlayContent;

    [ObservableProperty]
    private string? modalOverlayTitle;

    [ObservableProperty]
    private bool modalOverlayCanClose = true;

    public IReadOnlyList<NavigationItemViewModel> MenuItems { get; }

    public MainWindowViewModel(
        PlanEditorViewModel planEditor,
        RequestConfirmationViewModel requestConfirmation,
        EmissionConfirmationViewModel emissionConfirmation,
        ConnectionSettingsViewModel connectionSettings,
        ISettingsService settingsService,
        IMessenger messenger)
    {
        _planEditor = planEditor;
        _requestConfirmation = requestConfirmation;
        _emissionConfirmation = emissionConfirmation;
        _connectionSettings = connectionSettings;
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        var items = new List<NavigationItemViewModel>
        {
            CreateNavigationItem(LocalizationRegistry.Get("Shell.Navigation.InvoicePlan"), _planEditor),
            CreateNavigationItem(LocalizationRegistry.Get("Shell.Navigation.ConfirmRequest"), _requestConfirmation),
            CreateNavigationItem(LocalizationRegistry.Get("Shell.Navigation.ConfirmEmission"), _emissionConfirmation),
            CreateNavigationItem(LocalizationRegistry.Get("Shell.Navigation.ConnectionSettings"), _connectionSettings)
        };

        MenuItems = items;
        var settings = settingsService.GetAllAsync().GetAwaiter().GetResult();
        var isConfigured = settings.TryGetValue(SettingKeys.Server, out var server) && !string.IsNullOrWhiteSpace(server)
            && settings.TryGetValue(SettingKeys.Database, out var database) && !string.IsNullOrWhiteSpace(database)
            && settings.TryGetValue(SettingKeys.User, out var user) && !string.IsNullOrWhiteSpace(user)
            && settings.TryGetValue(SettingKeys.Password, out var password) && !string.IsNullOrWhiteSpace(password);

        if (isConfigured)
        {
            currentViewModel = _planEditor;
            Activate(_planEditor, items.First());
        }
        else
        {
            currentViewModel = _connectionSettings;
            _connectionSettings.StatusMessage = LocalizationRegistry.Get("Connection.Message.SetupPrompt");
            Activate(_connectionSettings, items.Last());
        }
    }

    private NavigationItemViewModel CreateNavigationItem(string title, ViewModelBase viewModel)
    {
        NavigationItemViewModel? item = null;
        item = new NavigationItemViewModel(title, selected => Activate(viewModel, selected));
        return item;
    }

    private void Activate(ViewModelBase viewModel, NavigationItemViewModel selectedItem)
    {
        CurrentViewModel = viewModel;
        foreach (var item in MenuItems)
        {
            item.IsSelected = ReferenceEquals(item, selectedItem);
        }
    }

}
