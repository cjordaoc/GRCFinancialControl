using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using System.Collections.ObjectModel;
using System.Linq;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IRecipient<OpenDialogMessage>, IRecipient<CloseDialogMessage>
    {
        public EngagementsViewModel Engagements { get; }
        public FiscalYearsViewModel FiscalYears { get; }
        public GrcTeamViewModel GrcTeam { get; }
        public ImportViewModel Import { get; }
        public AllocationsViewModel Allocations { get; }
        public ReportsViewModel Reports { get; }
        public SettingsViewModel Settings { get; }
        public ClosingPeriodsViewModel ClosingPeriods { get; }
        public CustomersViewModel Customers { get; }

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        [ObservableProperty]
        private ViewModelBase? _currentDialog;

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
                                 ClosingPeriodsViewModel closingPeriodsViewModel,
                                 CustomersViewModel customersViewModel,
                                 IMessenger messenger)
            : base(messenger)
        {
            Engagements = engagementsViewModel;
            FiscalYears = fiscalYearsViewModel;
            GrcTeam = grcTeamViewModel;
            Import = importViewModel;
            Allocations = allocationsViewModel;
            Reports = reportsViewModel;
            Settings = settingsViewModel;
            ClosingPeriods = closingPeriodsViewModel;
            Customers = customersViewModel;

            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new("Import", Import),
                new("Closing Periods", ClosingPeriods),
                new("Engagements", Engagements),
                new("Customers", Customers),
                new("Fiscal Years", FiscalYears),
                new("GRC Team", GrcTeam),
                new("Allocations", Allocations),
                new("Reports", Reports),
                new("Settings", Settings)
            };

            SelectedNavigationItem = NavigationItems.FirstOrDefault();
        }

        public void Receive(OpenDialogMessage message)
        {
            CurrentDialog = message.Value;
        }

        public void Receive(CloseDialogMessage message)
        {
            CurrentDialog = null;
        }

        async partial void OnSelectedNavigationItemChanged(NavigationItem? value)
        {
            if (value?.ViewModel == null && value?.Children?.Count > 0)
            {
                SelectedNavigationItem = value.Children.First();
                return;
            }

            ActiveView = value?.ViewModel;
            if (ActiveView is not null)
            {
                await ActiveView.LoadDataAsync();
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
    }
}
