using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementsViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly IPapdService _papdService;
        private readonly ICustomerService _customerService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        public EngagementsViewModel(IEngagementService engagementService, IPapdService papdService, ICustomerService customerService, IMessenger messenger)
        {
            _engagementService = engagementService;
            _papdService = papdService;
            _customerService = customerService;
            _messenger = messenger;
            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit, () => SelectedEngagement != null);
            DeleteCommand = new AsyncRelayCommand(Delete, () => SelectedEngagement != null);
        }

        public IRelayCommand AddCommand { get; }
        public IRelayCommand EditCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }

        public override async Task LoadDataAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementService.GetAllAsync());
        }

        private void Add()
        {
            var editorViewModel = new EngagementEditorViewModel(new Engagement(), _engagementService, _papdService, _customerService, _messenger);
            _messenger.Send(new OpenDialogMessage(editorViewModel));
        }

        private void Edit()
        {
            if (SelectedEngagement == null) return;
            var editorViewModel = new EngagementEditorViewModel(SelectedEngagement, _engagementService, _papdService, _customerService, _messenger);
            _messenger.Send(new OpenDialogMessage(editorViewModel));
        }

        private async Task Delete()
        {
            if (SelectedEngagement == null) return;
            await _engagementService.DeleteAsync(SelectedEngagement.Id);
            await LoadDataAsync();
        }
    }
}