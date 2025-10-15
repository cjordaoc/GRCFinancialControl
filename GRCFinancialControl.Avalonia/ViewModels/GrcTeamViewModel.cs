using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class GrcTeamViewModel : ViewModelBase
    {
        public PapdViewModel Papd { get; }
        public ManagersViewModel Managers { get; }
        public ManagerAssignmentsViewModel ManagerAssignments { get; }
        public PapdAssignmentsViewModel PapdAssignments { get; }

        public GrcTeamViewModel(
            PapdViewModel papdViewModel,
            ManagersViewModel managersViewModel,
            ManagerAssignmentsViewModel managerAssignmentsViewModel,
            PapdAssignmentsViewModel papdAssignmentsViewModel,
            IMessenger messenger)
            : base(messenger)
        {
            Papd = papdViewModel;
            Managers = managersViewModel;
            ManagerAssignments = managerAssignmentsViewModel;
            PapdAssignments = papdAssignmentsViewModel;
        }

        public override Task LoadDataAsync()
        {
            return Task.WhenAll(
                Papd.LoadDataAsync(),
                Managers.LoadDataAsync(),
                ManagerAssignments.LoadDataAsync(),
                PapdAssignments.LoadDataAsync());
        }
    }
}
