using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class ManagerEditorViewModel : DialogEditorViewModel<Manager>
    {
        private readonly IManagerService _managerService;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private ManagerPosition _position;

        [ObservableProperty]
        private string? _windowsLogin;

        [ObservableProperty]
        private string? _engagementManagerGui;

        public IEnumerable<ManagerPosition> PositionOptions => System.Enum.GetValues<ManagerPosition>();

        public Manager Manager { get; }

        public ManagerEditorViewModel(
            Manager manager,
            IManagerService managerService,
            IMessenger messenger,
            bool isReadOnlyMode = false)
            : base(messenger ?? throw new ArgumentNullException(nameof(messenger)), isReadOnlyMode)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _managerService = managerService ?? throw new ArgumentNullException(nameof(managerService));

            Name = manager.Name;
            Email = manager.Email;
            Position = manager.Position;
            WindowsLogin = manager.WindowsLogin;
            EngagementManagerGui = manager.EngagementManagerGui;
        }

        protected override async Task PersistChangesAsync()
        {
            Manager.Name = Name;
            Manager.Email = Email;
            Manager.Position = Position;
            Manager.WindowsLogin = string.IsNullOrWhiteSpace(WindowsLogin)
                ? null
                : WindowsLogin.Trim();
            Manager.EngagementManagerGui = string.IsNullOrWhiteSpace(EngagementManagerGui)
                ? null
                : EngagementManagerGui.Trim();

            if (Manager.Id == 0)
            {
                await _managerService.AddAsync(Manager);
            }
            else
            {
                await _managerService.UpdateAsync(Manager);
            }
        }

        protected override void OnSaveSucceeded()
        {
            ToastService.ShowSuccess("FINC_Managers_Toast_SaveSuccess", Manager.Name);
        }

        protected override void OnSaveFailed(Exception exception)
        {
            ToastService.ShowError("FINC_Managers_Toast_OperationFailed", exception.Message);
        }
    }
}
