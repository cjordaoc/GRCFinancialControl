using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class RankMappingEditorViewModel : DialogEditorViewModel<RankMapping>
    {
        private readonly IRankMappingService _rankMappingService;

        [ObservableProperty]
        private string _rawRank = string.Empty;

        [ObservableProperty]
        private string _normalizedRank = string.Empty;

        [ObservableProperty]
        private string _spreadsheetRank = string.Empty;

        [ObservableProperty]
        private bool _isActive = true;

        public RankMapping RankMapping { get; }

        public RankMappingEditorViewModel(
            RankMapping rankMapping,
            IRankMappingService rankMappingService,
            IMessenger messenger,
            bool isReadOnlyMode = false)
            : base(messenger ?? throw new ArgumentNullException(nameof(messenger)), isReadOnlyMode)
        {
            RankMapping = rankMapping ?? throw new ArgumentNullException(nameof(rankMapping));
            _rankMappingService = rankMappingService ?? throw new ArgumentNullException(nameof(rankMappingService));

            RawRank = rankMapping.RawRank;
            NormalizedRank = rankMapping.NormalizedRank;
            SpreadsheetRank = rankMapping.SpreadsheetRank;
            IsActive = rankMapping.IsActive;
        }

        protected override async Task PersistChangesAsync()
        {
            RankMapping.RawRank = (RawRank ?? string.Empty).Trim();
            RankMapping.NormalizedRank = (NormalizedRank ?? string.Empty).Trim();
            RankMapping.SpreadsheetRank = (SpreadsheetRank ?? string.Empty).Trim();
            RankMapping.IsActive = IsActive;

            if (RankMapping.Id == 0)
            {
                await _rankMappingService.AddAsync(RankMapping).ConfigureAwait(false);
            }
            else
            {
                await _rankMappingService.UpdateAsync(RankMapping).ConfigureAwait(false);
            }
        }
    }
}
