using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class AppMasterDataViewModel : ViewModelBase
    {
        public AppMasterDataViewModel(RankMappingsViewModel rankMappingsViewModel,
                                      IMessenger messenger)
            : base(messenger)
        {
            RankMappingsView = rankMappingsViewModel;
            SelectedSection = RankMappingsView;
        }

        public RankMappingsViewModel RankMappingsView { get; }

        [ObservableProperty]
        private ViewModelBase? _selectedSection;

        public override async Task LoadDataAsync()
        {
            await RankMappingsView.LoadDataAsync();

            SelectedSection ??= RankMappingsView;
        }
    }
}
