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
    public partial class PapdEditorViewModel : DialogEditorViewModel<Papd>
    {
        private readonly IPapdService _papdService;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private PapdLevel _level;

        [ObservableProperty]
        private string? _windowsLogin;

        public IEnumerable<PapdLevel> LevelOptions => System.Enum.GetValues<PapdLevel>();

        public Papd Papd { get; }

        public PapdEditorViewModel(
            Papd papd,
            IPapdService papdService,
            IMessenger messenger,
            bool isReadOnlyMode = false)
            : base(messenger ?? throw new ArgumentNullException(nameof(messenger)), isReadOnlyMode)
        {
            Papd = papd ?? throw new ArgumentNullException(nameof(papd));
            _papdService = papdService ?? throw new ArgumentNullException(nameof(papdService));

            Name = papd.Name;
            Level = papd.Level;
            WindowsLogin = papd.WindowsLogin;
        }

        protected override async Task PersistChangesAsync()
        {
            Papd.Name = Name;
            Papd.Level = Level;
            Papd.WindowsLogin = string.IsNullOrWhiteSpace(WindowsLogin)
                ? null
                : WindowsLogin.Trim();

            if (Papd.Id == 0)
            {
                await _papdService.AddAsync(Papd);
            }
            else
            {
                await _papdService.UpdateAsync(Papd);
            }
        }

        protected override void OnSaveSucceeded()
        {
            ToastService.ShowSuccess("Papds.Toast.SaveSuccess", Papd.Name);
        }

        protected override void OnSaveFailed(Exception exception)
        {
            ToastService.ShowError("Papds.Toast.OperationFailed", exception.Message);
        }
    }
}