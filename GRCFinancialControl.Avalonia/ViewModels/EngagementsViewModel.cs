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
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<Engagement> _engagements = new();

        [ObservableProperty]
        private Engagement? _selectedEngagement;

        public EngagementsViewModel(IEngagementService engagementService, IPapdService papdService, IMessenger messenger)
        {
            _engagementService = engagementService;
            _papdService = papdService;
            _messenger = messenger;
            LoadEngagementsCommand = new AsyncRelayCommand(LoadEngagementsAsync);
            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit, () => SelectedEngagement != null);
            DeleteCommand = new AsyncRelayCommand(Delete, () => SelectedEngagement != null);
            _ = LoadEngagementsAsync();
        }

        public IAsyncRelayCommand LoadEngagementsCommand { get; }
        public IRelayCommand AddCommand { get; }
        public IRelayCommand EditCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }

        private async Task LoadEngagementsAsync()
        {
            Engagements = new ObservableCollection<Engagement>(await _engagementService.GetAllAsync());
        }

        private void Add()
        {
            var editorViewModel = new EngagementEditorViewModel(new Engagement(), _engagementService, _papdService, _messenger);
            _messenger.Send(new OpenDialogMessage(editorViewModel));
        }

        private void Edit()
        {
            if (SelectedEngagement == null) return;
            var editorViewModel = new EngagementEditorViewModel(SelectedEngagement, _engagementService, _papdService, _messenger);
            _messenger.Send(new OpenDialogMessage(editorViewModel));
        }

        private async Task Delete()
        {
            if (SelectedEngagement == null) return;
            await _engagementService.DeleteAsync(SelectedEngagement.Id);
            await LoadEngagementsAsync();
        }
    }
}