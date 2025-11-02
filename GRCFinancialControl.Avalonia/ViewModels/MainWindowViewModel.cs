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
        private bool? _lastPersistedSidebarExpanded;
        private bool _suppressSidebarPersistence;

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

        [ObservableProperty]
        private bool _isSidebarExpanded = true;

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
                new(NavigationKeys.Home, LocalizationRegistry.Get("FINC_Navigation_Home"), Home) { Icon = "🏠" },
                new(NavigationKeys.Import, LocalizationRegistry.Get("FINC_Navigation_Import"), Import) { Icon = "📥" },
                new(NavigationKeys.Engagements, LocalizationRegistry.Get("FINC_Navigation_Engagements"), Engagements) { Icon = "🤝" },
                new(NavigationKeys.GrcTeam, LocalizationRegistry.Get("FINC_Navigation_GrcTeam"), GrcTeam) { Icon = "👥" },
                new(NavigationKeys.Allocations, LocalizationRegistry.Get("FINC_Navigation_Allocations"), Allocations) { Icon = "📊" },
                new(NavigationKeys.Reports, LocalizationRegistry.Get("FINC_Navigation_Reports"), Reports) { Icon = "📈" },
                new(NavigationKeys.Tasks, LocalizationRegistry.Get("FINC_Navigation_Tasks"), Tasks) { Icon = "✅" },
                new(NavigationKeys.ControlMasterData, LocalizationRegistry.Get("FINC_Navigation_ControlMasterData"), ControlMasterData) { Icon = "🛠" },
                new(NavigationKeys.AppMasterData, LocalizationRegistry.Get("FINC_Navigation_MasterData"), AppMasterData) { Icon = "🗃" },
                new(NavigationKeys.Settings, LocalizationRegistry.Get("FINC_Navigation_Settings"), Settings) { Icon = "⚙" }
            };

            _navigationIndex = BuildNavigationIndex(NavigationItems);

            var settings = _settingsService.GetAll();
            if (settings.TryGetValue(SettingKeys.FinancialControlSidebarExpanded, out var sidebarValue)
                && bool.TryParse(sidebarValue, out var storedSidebarExpanded))
            {
                _suppressSidebarPersistence = true;
                IsSidebarExpanded = storedSidebarExpanded;
                _lastPersistedSidebarExpanded = storedSidebarExpanded;
                _suppressSidebarPersistence = false;
            }
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

        async partial void OnIsSidebarExpandedChanged(bool value)
        {
            if (_suppressSidebarPersistence || (_lastPersistedSidebarExpanded.HasValue && _lastPersistedSidebarExpanded.Value == value))
            {
                return;
            }

            try
            {
                var settings = await _settingsService.GetAllAsync().ConfigureAwait(false);
                settings[SettingKeys.FinancialControlSidebarExpanded] = value.ToString();
                await _settingsService.SaveAllAsync(settings).ConfigureAwait(false);
                _lastPersistedSidebarExpanded = value;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to persist sidebar state: {ex.Message}");
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
            _icon = BuildDefaultIcon(title);
        }

        public NavigationItem(string key, string title, ViewModelBase? viewModel, ObservableCollection<NavigationItem> children)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            ViewModel = viewModel;
            Children = children ?? throw new ArgumentNullException(nameof(children));
            _icon = BuildDefaultIcon(title);
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

        private string _icon = string.Empty;

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, string.IsNullOrWhiteSpace(value) ? BuildDefaultIcon(Title) : value);
        }

        private static string BuildDefaultIcon(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var parts = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var first = FindLeadingCharacter(parts.FirstOrDefault());
            if (first is null)
            {
                return string.Empty;
            }

            var second = parts.Skip(1).Select(FindLeadingCharacter).FirstOrDefault(c => c is not null);
            return second is null
                ? char.ToUpperInvariant(first.Value).ToString()
                : string.Concat(char.ToUpperInvariant(first.Value), char.ToUpperInvariant(second.Value));
        }

        private static char? FindLeadingCharacter(string? segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return null;
            }

            foreach (var character in segment)
            {
                if (char.IsLetterOrDigit(character))
                {
                    return character;
                }
            }

            return null;
        }
    }
}
