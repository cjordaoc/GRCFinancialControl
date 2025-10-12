using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;

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

            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new("Import", Import),
                new("Closing Periods", ClosingPeriods),
                new("Engagements", Engagements),
                new("Fiscal Years", FiscalYears),
                new("PAPD", Papds),
                new("Allocation", Allocation),
                new("Reports", Reports),
                new("Exceptions", Exceptions),
                new("Settings", Settings)
            };
            messenger.Register<OpenDialogMessage>(this);
            messenger.Register<CloseDialogMessage>(this);
        }

        public void Receive(OpenDialogMessage message)
        {
            CurrentDialog = message.Value;
        }

        public void Receive(CloseDialogMessage message)
        {
            CurrentDialog = null;
        }

        partial void OnSelectedNavigationItemChanged(NavigationItem? value)
        {
            ActiveView = value?.ViewModel;
        }
    }

    public record NavigationItem(string Title, ViewModelBase ViewModel);
}