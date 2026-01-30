using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Presentation.Localization;
using GRC.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRC.Shared.Core.Enums;
using GRC.Shared.Core.Models.Core;
using GRC.Shared.Core.Models.Financial;
using GRC.Shared.Core.Models.Allocations;
using GRC.Shared.Core.Models.Assignments;
using GRC.Shared.Core.Models.Lookups;
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

        [ObservableProperty]
        private string? _engagementPapdGui;

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
            EngagementPapdGui = papd.EngagementPapdGui;
        }

        protected override async Task PersistChangesAsync()
        {
            Papd.Name = Name;
            Papd.Level = Level;
            Papd.WindowsLogin = string.IsNullOrWhiteSpace(WindowsLogin)
                ? null
                : WindowsLogin.Trim();
            Papd.EngagementPapdGui = string.IsNullOrWhiteSpace(EngagementPapdGui)
                ? null
                : EngagementPapdGui.Trim();

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
            var message = LocalizationRegistry.Format("FINC_Papds_Toast_SaveSuccess", Papd.Name);
            ToastService.ShowSuccess(message);
        }

        protected override void OnSaveFailed(Exception exception)
        {
            var message = LocalizationRegistry.Format("FINC_Papds_Toast_OperationFailed", exception.Message);
            ToastService.ShowError(message);
        }
    }
}