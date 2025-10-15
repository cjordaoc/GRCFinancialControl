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
    public partial class ManagerEditorViewModel : ViewModelBase
    {
        private readonly IManagerService _managerService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private ManagerPosition _position;

        public IEnumerable<ManagerPosition> PositionOptions => System.Enum.GetValues<ManagerPosition>();

        public Manager Manager { get; }

        public ManagerEditorViewModel(Manager manager, IManagerService managerService, IMessenger messenger)
        {
            Manager = manager;
            _managerService = managerService;
            _messenger = messenger;

            Name = manager.Name;
            Email = manager.Email;
            Position = manager.Position;
        }

        [RelayCommand]
        private async Task Save()
        {
            Manager.Name = Name;
            Manager.Email = Email;
            Manager.Position = Position;

            if (Manager.Id == 0)
            {
                await _managerService.AddAsync(Manager);
            }
            else
            {
                await _managerService.UpdateAsync(Manager);
            }

            _messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.Send(new CloseDialogMessage(false));
        }
    }
}
