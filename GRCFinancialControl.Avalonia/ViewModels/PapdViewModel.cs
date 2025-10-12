using System;
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
    public partial class PapdViewModel : ViewModelBase
    {
        private readonly IPapdService _papdService;
        private readonly IMessenger _messenger;
        private readonly Func<EngagementPapdAssignmentViewModel> _assignmentViewModelFactory;

        [ObservableProperty]
        private ObservableCollection<Papd> _papds = new();

        [ObservableProperty]
        private Papd? _selectedPapd;

        public PapdViewModel(IPapdService papdService, IMessenger messenger, Func<EngagementPapdAssignmentViewModel> assignmentViewModelFactory)
        {
            _papdService = papdService;
            _messenger = messenger;
            _assignmentViewModelFactory = assignmentViewModelFactory;
            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit, () => SelectedPapd != null);
            DeleteCommand = new AsyncRelayCommand(Delete, () => SelectedPapd != null);
            ManageAssignmentsCommand = new AsyncRelayCommand(ManageAssignmentsAsync);
        }

        public IRelayCommand AddCommand { get; }
        public IRelayCommand EditCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }
        public IAsyncRelayCommand ManageAssignmentsCommand { get; }

        public override async Task LoadDataAsync()
        {
            Papds = new ObservableCollection<Papd>(await _papdService.GetAllAsync());
        }

        private void Add()
        {
            var editorViewModel = new PapdEditorViewModel(new Papd(), _papdService, _messenger);
            _messenger.Send(new OpenDialogMessage(editorViewModel));
        }

        private void Edit()
        {
            if (SelectedPapd == null) return;
            var editorViewModel = new PapdEditorViewModel(SelectedPapd, _papdService, _messenger);
            _messenger.Send(new OpenDialogMessage(editorViewModel));
        }

        private async Task Delete()
        {
            if (SelectedPapd == null) return;
            await _papdService.DeleteAsync(SelectedPapd.Id);
            await LoadDataAsync();
        }

        private async Task ManageAssignmentsAsync()
        {
            var assignmentViewModel = _assignmentViewModelFactory();
            await assignmentViewModel.LoadDataAsync();
            _messenger.Send(new OpenDialogMessage(assignmentViewModel));
        }

        partial void OnSelectedPapdChanged(Papd? value)
        {
            if (EditCommand is RelayCommand edit)
            {
                edit.NotifyCanExecuteChanged();
            }

            if (DeleteCommand is AsyncRelayCommand delete)
            {
                delete.NotifyCanExecuteChanged();
            }
        }
    }
}