using System;
using System.Collections.ObjectModel;
using System.Linq;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Avalonia;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ILoggingService _loggingService;

        public EngagementsViewModel Engagements { get; }
        public FiscalYearsViewModel FiscalYears { get; }
        public GrcTeamViewModel GrcTeam { get; }
        public ImportViewModel Import { get; }
        public AllocationsViewModel Allocations { get; }
        public ReportsViewModel Reports { get; }
        public SettingsViewModel Settings { get; }
        public RankMappingsViewModel RankMappings { get; }
        public ClosingPeriodsViewModel ClosingPeriods { get; }
        public CustomersViewModel Customers { get; }
        public TasksViewModel Tasks { get; }

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        [ObservableProperty]
        private NavigationItem? _selectedNavigationItem;

        [ObservableProperty]
        private ViewModelBase? _activeView;

        public MainWindowViewModel(EngagementsViewModel engagementsViewModel,
                                   FiscalYearsViewModel fiscalYearsViewModel,
                                   GrcTeamViewModel grcTeamViewModel,
                                   ImportViewModel importViewModel,
                                   AllocationsViewModel allocationsViewModel,
                                   ReportsViewModel reportsViewModel,
                                   SettingsViewModel settingsViewModel,
                                   RankMappingsViewModel rankMappingsViewModel,
                                   ClosingPeriodsViewModel closingPeriodsViewModel,
                                   CustomersViewModel customersViewModel,
                                   TasksViewModel tasksViewModel,
                                   ILoggingService loggingService,
                                   IMessenger messenger)
            : base(messenger)
        {
            ArgumentNullException.ThrowIfNull(loggingService);

            _loggingService = loggingService;
            Engagements = engagementsViewModel;
            FiscalYears = fiscalYearsViewModel;
            GrcTeam = grcTeamViewModel;
            Import = importViewModel;
            Allocations = allocationsViewModel;
            Reports = reportsViewModel;
            Settings = settingsViewModel;
            RankMappings = rankMappingsViewModel;
            ClosingPeriods = closingPeriodsViewModel;
            Customers = customersViewModel;
            Tasks = tasksViewModel;

            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new(LocalizationRegistry.Get("Navigation.Import"), Import),
                new(LocalizationRegistry.Get("Navigation.ClosingPeriods"), ClosingPeriods),
                new(LocalizationRegistry.Get("Navigation.Engagements"), Engagements),
                new(LocalizationRegistry.Get("Navigation.Customers"), Customers),
                new(LocalizationRegistry.Get("Navigation.FiscalYears"), FiscalYears),
                new(LocalizationRegistry.Get("Navigation.GrcTeam"), GrcTeam),
                new(LocalizationRegistry.Get("Navigation.Allocations"), Allocations),
                new(LocalizationRegistry.Get("Navigation.Reports"), Reports),
                new(LocalizationRegistry.Get("Navigation.Tasks"), Tasks),
                new(LocalizationRegistry.Get(
                    "Navigation.MasterData"),
                    null,
                    new ObservableCollection<NavigationItem>
                    {
                        new(LocalizationRegistry.Get("Navigation.MasterData.RankMappings"), RankMappings)
                    }),
                new(LocalizationRegistry.Get("Navigation.Settings"), Settings)
            };

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
            finally
            {
                UpdateSelectionFlags(value);
            }
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
    }

    public class NavigationItem : ObservableObject
    {
        public NavigationItem(string title, ViewModelBase? viewModel)
        {
            Title = title;
            ViewModel = viewModel;
            Children = new ObservableCollection<NavigationItem>();
        }

        public NavigationItem(string title, ViewModelBase? viewModel, ObservableCollection<NavigationItem> children)
        {
            Title = title;
            ViewModel = viewModel;
            Children = children;
        }

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
