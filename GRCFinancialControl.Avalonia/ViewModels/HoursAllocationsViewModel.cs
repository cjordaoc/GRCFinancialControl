using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    /// <summary>
    /// Presents the engagement list and coordinates opening the hours allocation editor.
    /// </summary>
    public sealed partial class HoursAllocationsViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly Func<HoursAllocationDetailViewModel> _detailFactory;
        private readonly DialogService _dialogService;
        private readonly LoggingService _loggingService;

        [ObservableProperty]
        private ObservableCollection<EngagementListItem> _engagements = new();

        [ObservableProperty]
        private EngagementListItem? _selectedEngagement;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _statusMessage;

        public HoursAllocationsViewModel(
            IEngagementService engagementService,
            Func<HoursAllocationDetailViewModel> detailFactory,
            DialogService dialogService,
            LoggingService loggingService,
            IMessenger messenger)
            : base(messenger)
        {
            _engagementService = engagementService ?? throw new ArgumentNullException(nameof(engagementService));
            _detailFactory = detailFactory ?? throw new ArgumentNullException(nameof(detailFactory));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// Gets the title displayed at the top of the engagements list.
        /// </summary>
        public string Header => "Engagements";

        /// <summary>
        /// Gets a value indicating whether the detail dialog can be opened.
        /// </summary>
        public bool CanOpenDetail => SelectedEngagement is not null && !IsBusy;

        public override async Task LoadDataAsync()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = null;

                var engagements = await _engagementService.GetAllAsync().ConfigureAwait(false);
                var ordered = engagements
                    .OrderBy(e => e.EngagementId, StringComparer.OrdinalIgnoreCase)
                    .Select(e => new EngagementListItem(e.Id, e.EngagementId, e.Description, e.Customer?.Name ?? string.Empty))
                    .ToList();

                Engagements = new ObservableCollection<EngagementListItem>(ordered);

                if (ordered.Count == 0)
                {
                    SelectedEngagement = null;
                    StatusMessage = "No engagements available.";
                    return;
                }

                SelectedEngagement ??= ordered.First();
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = ex.Message;
                Engagements = new ObservableCollection<EngagementListItem>();
                SelectedEngagement = null;
            }
            finally
            {
                IsBusy = false;
                NotifyCommandCanExecute(OpenDetailCommand);
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadDataAsync().ConfigureAwait(false);
        }

        [RelayCommand(CanExecute = nameof(CanExecuteOpenDetail))]
        private async Task OpenDetailAsync()
        {
            if (SelectedEngagement is null)
            {
                return;
            }

            try
            {
                IsBusy = true;
                NotifyCommandCanExecute(OpenDetailCommand);

                var detail = _detailFactory();
                detail.InitializeSelection(SelectedEngagement.Id);
                await detail.LoadDataAsync().ConfigureAwait(false);
                await _dialogService.ShowDialogAsync(detail, "Hours Allocation").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
                NotifyCommandCanExecute(OpenDetailCommand);
            }
        }

        private bool CanExecuteOpenDetail() => CanOpenDetail;

        partial void OnSelectedEngagementChanged(EngagementListItem? value)
        {
            OnPropertyChanged(nameof(CanOpenDetail));
            NotifyCommandCanExecute(OpenDetailCommand);
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanOpenDetail));
            NotifyCommandCanExecute(OpenDetailCommand);
        }

        /// <summary>
        /// Represents the engagement entry displayed in the list.
        /// </summary>
        public sealed record EngagementListItem(int Id, string Code, string Name, string CustomerName)
        {
            public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Code : $"{Code} - {Name}";
        }
    }
}
