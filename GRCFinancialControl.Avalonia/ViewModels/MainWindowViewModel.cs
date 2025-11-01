using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly LoggingService _loggingService;
        private readonly ISettingsService _settingsService;
        private readonly Dictionary<string, NavigationItem> _navigationIndex;
        private string? _lastPersistedNavigationKey;

        public HomeViewModel Home { get; }
        public EngagementsViewModel Engagements { get; }
        public GrcTeamViewModel GrcTeam { get; }
        public ImportViewModel Import { get; }
        public AllocationsViewModel Allocations { get; }
        public ReportsViewModel Reports { get; }
        public SettingsViewModel Settings { get; }
        public AppMasterDataViewModel AppMasterData { get; }
        public TasksViewModel Tasks { get; }
        public ControlMasterDataViewModel ControlMasterData { get; }

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        [ObservableProperty]
        private NavigationItem? _selectedNavigationItem;

        [ObservableProperty]
        private ViewModelBase? _activeView;

        public MainWindowViewModel(HomeViewModel homeViewModel,
                                   EngagementsViewModel engagementsViewModel,
                                   GrcTeamViewModel grcTeamViewModel,
                                   ImportViewModel importViewModel,
                                   AllocationsViewModel allocationsViewModel,
                                   ReportsViewModel reportsViewModel,
                                   SettingsViewModel settingsViewModel,
                                   AppMasterDataViewModel appMasterDataViewModel,
                                   ControlMasterDataViewModel controlMasterDataViewModel,
                                   TasksViewModel tasksViewModel,
                                   LoggingService loggingService,
                                   ISettingsService settingsService,
                                   IMessenger messenger)
            : base(messenger)
        {
            ArgumentNullException.ThrowIfNull(loggingService);
            ArgumentNullException.ThrowIfNull(settingsService);

            _loggingService = loggingService;
            _settingsService = settingsService;
            Home = homeViewModel;
            Engagements = engagementsViewModel;
            GrcTeam = grcTeamViewModel;
            Import = importViewModel;
            Allocations = allocationsViewModel;
            Reports = reportsViewModel;
            Settings = settingsViewModel;
            AppMasterData = appMasterDataViewModel;
            Tasks = tasksViewModel;
            ControlMasterData = controlMasterDataViewModel;

            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new(NavigationKeys.Home, LocalizationRegistry.Get("FINC_Navigation_Home"), Home),
                new(NavigationKeys.Import, LocalizationRegistry.Get("FINC_Navigation_Import"), Import),
                new(NavigationKeys.Engagements, LocalizationRegistry.Get("FINC_Navigation_Engagements"), Engagements),
                new(NavigationKeys.GrcTeam, LocalizationRegistry.Get("FINC_Navigation_GrcTeam"), GrcTeam),
                new(NavigationKeys.Allocations, LocalizationRegistry.Get("FINC_Navigation_Allocations"), Allocations),
                new(NavigationKeys.Reports, LocalizationRegistry.Get("FINC_Navigation_Reports"), Reports),
                new(NavigationKeys.Tasks, LocalizationRegistry.Get("FINC_Navigation_Tasks"), Tasks),
                new(NavigationKeys.ControlMasterData, LocalizationRegistry.Get("FINC_Navigation_ControlMasterData"), ControlMasterData),
                new(NavigationKeys.AppMasterData, LocalizationRegistry.Get("FINC_Navigation_MasterData"), AppMasterData),
                new(NavigationKeys.Settings, LocalizationRegistry.Get("FINC_Navigation_Settings"), Settings)
            };

            _navigationIndex = BuildNavigationIndex(NavigationItems);

            var settings = _settingsService.GetAll();
            if (settings.TryGetValue(SettingKeys.LastGrcNavigationItemKey, out var storedKey)
                && !string.IsNullOrWhiteSpace(storedKey))
            {
                _lastPersistedNavigationKey = storedKey;
            }

            SelectedNavigationItem = NavigationItems.FirstOrDefault();
        }

        [RelayCommand]
        private void SelectNavigationItem(NavigationItem navigationItem)
        {
            if (navigationItem is null)
            {
                return;
            }

            SelectedNavigationItem = navigationItem;
        }

        async partial void OnSelectedNavigationItemChanged(NavigationItem? value)
        {
            if (value?.ViewModel == null && value?.Children?.Count > 0)
            {
                SelectedNavigationItem = value.Children.First();
                return;
            }

            ActiveView = value?.ViewModel;
            if (ActiveView is null)
            {
                UpdateSelectionFlags(value);
                await PersistSelectedNavigationItemAsync(value).ConfigureAwait(false);
                return;
            }

            try
            {
                await ActiveView.LoadDataAsync();
            }
            catch (Exception ex)
            {
                var sectionName = value?.Title ?? ActiveView.GetType().Name;
                _loggingService.LogError($"Failed to load '{sectionName}': {ex.Message}");
            }

            UpdateSelectionFlags(value);
            await PersistSelectedNavigationItemAsync(value).ConfigureAwait(false);
        }

        private void UpdateSelectionFlags(NavigationItem? selectedItem)
        {
            foreach (var item in NavigationItems)
            {
                UpdateSelectionFor(item, selectedItem);
            }
        }

        private static void UpdateSelectionFor(NavigationItem item, NavigationItem? selectedItem)
        {
            item.IsSelected = ReferenceEquals(item, selectedItem);
            foreach (var child in item.Children)
            {
                UpdateSelectionFor(child, selectedItem);
            }
        }

        private async Task PersistSelectedNavigationItemAsync(NavigationItem? item)
        {
            var key = item?.Key ?? string.Empty;
            if (string.Equals(_lastPersistedNavigationKey, key, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                var settings = await _settingsService.GetAllAsync().ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(key))
                {
                    if (settings.Remove(SettingKeys.LastGrcNavigationItemKey))
                    {
                        await _settingsService.SaveAllAsync(settings).ConfigureAwait(false);
                        _lastPersistedNavigationKey = key;
                    }

                    return;
                }

                settings[SettingKeys.LastGrcNavigationItemKey] = key;
                await _settingsService.SaveAllAsync(settings).ConfigureAwait(false);
                _lastPersistedNavigationKey = key;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to persist navigation state: {ex.Message}");
            }
        }

        private static Dictionary<string, NavigationItem> BuildNavigationIndex(IEnumerable<NavigationItem> items)
        {
            var index = new Dictionary<string, NavigationItem>(StringComparer.Ordinal);

            void AddItem(NavigationItem navigationItem)
            {
                if (!string.IsNullOrWhiteSpace(navigationItem.Key))
                {
                    index[navigationItem.Key] = navigationItem;
                }

                foreach (var child in navigationItem.Children)
                {
                    AddItem(child);
                }
            }

            foreach (var item in items)
            {
                AddItem(item);
            }

            return index;
        }

        private static class NavigationKeys
        {
            public const string Home = "Home";
            public const string Import = "Import";
            public const string Engagements = "Engagements";
            public const string GrcTeam = "GrcTeam";
            public const string Allocations = "Allocations";
            public const string Reports = "Reports";
            public const string Tasks = "Tasks";
            public const string ControlMasterData = "ControlMasterData";
            public const string AppMasterData = "AppMasterData";
            public const string Settings = "Settings";
        }
    }

    public class NavigationItem : ObservableObject
    {
        public NavigationItem(string key, string title, ViewModelBase? viewModel)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            ViewModel = viewModel;
            Children = new ObservableCollection<NavigationItem>();
        }

        public NavigationItem(string key, string title, ViewModelBase? viewModel, ObservableCollection<NavigationItem> children)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            ViewModel = viewModel;
            Children = children ?? throw new ArgumentNullException(nameof(children));
        }

        public string Key { get; }
        public string Title { get; }
        public ViewModelBase? ViewModel { get; }
        public ObservableCollection<NavigationItem> Children { get; }

        public bool HasChildren => Children.Count > 0;

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
