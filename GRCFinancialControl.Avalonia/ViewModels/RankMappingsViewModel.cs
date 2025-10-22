using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class RankMappingsViewModel : ViewModelBase
    {
        private readonly IRankMappingService _rankMappingService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<RankMapping> _rankMappings = new();

        [ObservableProperty]
        private RankMapping? _selectedRankMapping;

        public RankMappingsViewModel(
            IRankMappingService rankMappingService,
            IDialogService dialogService,
            IMessenger messenger)
            : base(messenger)
        {
            _rankMappingService = rankMappingService ?? throw new ArgumentNullException(nameof(rankMappingService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public override async Task LoadDataAsync()
        {
            var mappings = await _rankMappingService.GetAllAsync().ConfigureAwait(false);
            RankMappings = new ObservableCollection<RankMapping>(mappings);
        }

        [RelayCommand]
        private async Task AddAsync()
        {
            var editor = new RankMappingEditorViewModel(new RankMapping(), _rankMappingService, Messenger);
            var title = LocalizationRegistry.Get("MasterData.RankMappings.Dialog.AddTitle");
            await _dialogService.ShowDialogAsync(editor, title).ConfigureAwait(false);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanModify))]
        private async Task EditAsync(RankMapping? rankMapping)
        {
            if (rankMapping is null)
            {
                return;
            }

            var editor = new RankMappingEditorViewModel(rankMapping, _rankMappingService, Messenger);
            var title = LocalizationRegistry.Get("MasterData.RankMappings.Dialog.EditTitle");
            await _dialogService.ShowDialogAsync(editor, title).ConfigureAwait(false);
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanModify))]
        private async Task DeleteAsync(RankMapping? rankMapping)
        {
            if (rankMapping is null)
            {
                return;
            }

            await _rankMappingService.DeleteAsync(rankMapping.Id).ConfigureAwait(false);
            Messenger.Send(new RefreshDataMessage());
        }

        private static bool CanModify(RankMapping? rankMapping) => rankMapping is not null;

        partial void OnSelectedRankMappingChanged(RankMapping? value)
        {
            NotifyCommandCanExecute(EditCommand);
            NotifyCommandCanExecute(DeleteCommand);
        }
    }
}
