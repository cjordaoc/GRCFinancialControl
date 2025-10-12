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
        public PapdViewModel Papds { get; }
        public ImportViewModel Import { get; }
        public AllocationViewModel Allocation { get; }
        public ReportsViewModel Reports { get; }
        public ExceptionsViewModel Exceptions { get; }
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
                                 PapdViewModel papdViewModel,
                                 ImportViewModel importViewModel,
                                 AllocationViewModel allocationViewModel,
                                 ReportsViewModel reportsViewModel,
                                 ExceptionsViewModel exceptionsViewModel,
                                 SettingsViewModel settingsViewModel,
                                 ClosingPeriodsViewModel closingPeriodsViewModel,
                                 CustomersViewModel customersViewModel,
                                 IMessenger messenger)
        {
            Engagements = engagementsViewModel;
            FiscalYears = fiscalYearsViewModel;
            Papds = papdViewModel;
            Import = importViewModel;
            Allocation = allocationViewModel;
            Reports = reportsViewModel;
            Exceptions = exceptionsViewModel;
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
                new("PAPD", Papds),
                new("Allocation", Allocation),
                new("Reports", Reports),
                new("Exceptions", Exceptions),
                new("Settings", Settings)
            };
            messenger.Register<OpenDialogMessage>(this);
            messenger.Register<CloseDialogMessage>(this);

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
            ActiveView = value?.ViewModel;
            if (ActiveView is not null)
            {
                await ActiveView.LoadDataAsync();
            }
        }
    }

    public record NavigationItem(string Title, ViewModelBase ViewModel);
}