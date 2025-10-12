using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class PapdEditorViewModel : ViewModelBase
    {
        private readonly IPapdService _papdService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private PapdLevel _level;

        public IEnumerable<PapdLevel> LevelOptions => System.Enum.GetValues<PapdLevel>();

        public Papd Papd { get; }

        public PapdEditorViewModel(Papd papd, IPapdService papdService, IMessenger messenger)
        {
            Papd = papd;
            _papdService = papdService;
            _messenger = messenger;

            Name = papd.Name;
            Level = papd.Level;
        }

        [RelayCommand]
        private async Task Save()
        {
            Papd.Name = Name;
            Papd.Level = Level;

            if (Papd.Id == 0)
            {
                await _papdService.AddAsync(Papd);
            }
            else
            {
                await _papdService.UpdateAsync(Papd);
            }

            _messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Cancel()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }
    }
}