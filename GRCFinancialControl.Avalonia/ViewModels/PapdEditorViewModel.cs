using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        }

        protected override async Task PersistChangesAsync()
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
        }
    }
}