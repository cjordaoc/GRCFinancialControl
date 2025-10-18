using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InvoicePlanner.Avalonia.Resources;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PlanEditorViewModel _planEditor;
    private readonly RequestConfirmationViewModel _requestConfirmation;
    private readonly EmissionConfirmationViewModel _emissionConfirmation;
    private readonly ConnectionSettingsViewModel _connectionSettings;

    [ObservableProperty]
    private string title = Strings.Get("AppTitle");

    [ObservableProperty]
    private ViewModelBase currentViewModel;

    public IReadOnlyList<NavigationItemViewModel> MenuItems { get; }

    public MainWindowViewModel(
        PlanEditorViewModel planEditor,
        RequestConfirmationViewModel requestConfirmation,
        EmissionConfirmationViewModel emissionConfirmation,
        ConnectionSettingsViewModel connectionSettings,
        ISettingsService settingsService)
    {
        _planEditor = planEditor;
        _requestConfirmation = requestConfirmation;
        _emissionConfirmation = emissionConfirmation;
        _connectionSettings = connectionSettings;

        var items = new List<NavigationItemViewModel>
        {
            CreateNavigationItem(Strings.Get("NavInvoicePlan"), _planEditor),
            CreateNavigationItem(Strings.Get("NavConfirmRequest"), _requestConfirmation),
            CreateNavigationItem(Strings.Get("NavConfirmEmissions"), _emissionConfirmation),
            CreateNavigationItem(Strings.Get("NavConnectionSettings"), _connectionSettings)
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
            _connectionSettings.StatusMessage = Strings.Get("ConnectionSetupPrompt");
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
