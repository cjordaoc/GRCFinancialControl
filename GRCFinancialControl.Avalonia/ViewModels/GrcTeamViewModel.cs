using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class GrcTeamViewModel : ViewModelBase
    {
        public PapdViewModel Papd { get; }
        public ManagersViewModel Managers { get; }

        public GrcTeamViewModel(
            PapdViewModel papdViewModel,
            ManagersViewModel managersViewModel,
            IMessenger messenger)
            : base(messenger)
        {
            Papd = papdViewModel;
            Managers = managersViewModel;
        }

        public override Task LoadDataAsync()
        {
            return Task.WhenAll(
                Papd.LoadDataAsync(),
                Managers.LoadDataAsync());
        }
    }
}
