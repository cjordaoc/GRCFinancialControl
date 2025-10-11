namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class ReportsViewModel : ViewModelBase
    {
        public PlannedVsActualViewModel PlannedVsActual { get; }
        public BacklogViewModel Backlog { get; }

        public ReportsViewModel(PlannedVsActualViewModel plannedVsActual, BacklogViewModel backlog)
        {
            PlannedVsActual = plannedVsActual;
            Backlog = backlog;
        }
    }
}