using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class GrcTeamViewModel : ViewModelBase
    {
        public PapdViewModel Papd { get; }
        public ManagersViewModel Managers { get; }
        public ManagerAssignmentsViewModel ManagerAssignments { get; }

        public GrcTeamViewModel(
            PapdViewModel papdViewModel,
            ManagersViewModel managersViewModel,
            ManagerAssignmentsViewModel managerAssignmentsViewModel,
            IMessenger messenger)
            : base(messenger)
        {
            Papd = papdViewModel;
            Managers = managersViewModel;
            ManagerAssignments = managerAssignmentsViewModel;
        }

        public override Task LoadDataAsync()
        {
            return Task.WhenAll(Papd.LoadDataAsync(), Managers.LoadDataAsync(), ManagerAssignments.LoadDataAsync());
        }
    }
}
