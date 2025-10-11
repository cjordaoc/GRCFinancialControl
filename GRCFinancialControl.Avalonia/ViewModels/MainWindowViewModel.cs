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

        [ObservableProperty]
        private ViewModelBase? _currentDialog;

        public MainWindowViewModel(EngagementsViewModel engagementsViewModel,
                                 FiscalYearsViewModel fiscalYearsViewModel,
                                 PapdViewModel papdViewModel,
                                 ImportViewModel importViewModel,
                                 AllocationViewModel allocationViewModel,
                                 ReportsViewModel reportsViewModel,
                                 ExceptionsViewModel exceptionsViewModel,
                                 SettingsViewModel settingsViewModel,
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
    }
}