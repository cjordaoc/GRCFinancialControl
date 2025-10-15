using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Utilities;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class PapdAssignmentsViewModel : ViewModelBase
    {
        private readonly IPapdService _papdService;
        private readonly IEngagementService _engagementService;
        private List<Engagement> _engagementCache = new();

        [ObservableProperty]
        private ObservableCollection<Papd> _papds = new();

        [ObservableProperty]
        private Papd? _selectedPapd;

        [ObservableProperty]
        private ObservableCollection<EngagementOption> _engagements = new();

        [ObservableProperty]
        private EngagementOption? _selectedEngagement;

        [ObservableProperty]
        private ObservableCollection<PapdAssignmentDisplayItem> _assignments = new();

        [ObservableProperty]
        private PapdAssignmentDisplayItem? _selectedAssignment;

        [ObservableProperty]
        private DateTime? _effectiveDate = DateTime.Today;

        public PapdAssignmentsViewModel(IPapdService papdService, IEngagementService engagementService, IMessenger messenger)
            : base(messenger)
        {
            _papdService = papdService;
            _engagementService = engagementService;
        }

        public DateTimeOffset? EffectiveDateOffset
        {
            get => DateTimeOffsetHelper.FromDate(EffectiveDate);
            set => EffectiveDate = DateTimeOffsetHelper.ToDate(value);
        }

        public override async Task LoadDataAsync()
        {
            await LoadPapdsAsync();
            await LoadEngagementsAsync();
            await LoadAssignmentsAsync();
        }

        [RelayCommand(CanExecute = nameof(CanAssign))]
        private async Task AssignAsync()
        {
            if (SelectedPapd is null || SelectedEngagement is null || EffectiveDate is null)
            {
                return;
            }

            var engagement = await EnsureEngagementAsync(SelectedEngagement.InternalId);
            if (engagement is null)
            {
                return;
            }

            var effectiveDate = EffectiveDate.Value.Date;
            var hasExisting = engagement.EngagementPapds.Any(a => a.PapdId == SelectedPapd.Id && a.EffectiveDate == effectiveDate);
            if (!hasExisting)
            {
                engagement.EngagementPapds.Add(new EngagementPapd
                {
                    EngagementId = engagement.Id,
                    PapdId = SelectedPapd.Id,
                    EffectiveDate = effectiveDate
                });

                await _engagementService.UpdateAsync(engagement);
            }

            await RefreshAssignmentsAsync();
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanRemoveAssignment))]
        private async Task RemoveAssignmentAsync()
        {
            if (SelectedAssignment is null)
            {
                return;
            }

            var engagement = await EnsureEngagementAsync(SelectedAssignment.EngagementInternalId);
            if (engagement is null)
            {
                return;
            }

            var assignment = engagement.EngagementPapds.FirstOrDefault(a => a.Id == SelectedAssignment.AssignmentId);
            if (assignment is null)
            {
                return;
            }

            engagement.EngagementPapds.Remove(assignment);
            await _engagementService.UpdateAsync(engagement);

            await RefreshAssignmentsAsync();
            Messenger.Send(new RefreshDataMessage());
        }

        private bool CanAssign() => SelectedPapd is not null && SelectedEngagement is not null && EffectiveDate.HasValue;

        private bool CanRemoveAssignment() => SelectedAssignment is not null;

        partial void OnSelectedPapdChanged(Papd? value)
        {
            _ = LoadAssignmentsAsync();
            AssignCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedEngagementChanged(EngagementOption? value)
        {
            AssignCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedAssignmentChanged(PapdAssignmentDisplayItem? value)
        {
            RemoveAssignmentCommand.NotifyCanExecuteChanged();
        }

        partial void OnEffectiveDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(EffectiveDateOffset));
            AssignCommand.NotifyCanExecuteChanged();
        }

        private async Task LoadPapdsAsync()
        {
            var papds = await _papdService.GetAllAsync();
            var ordered = papds
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var previousId = SelectedPapd?.Id;
            Papds = new ObservableCollection<Papd>(ordered);
            SelectedPapd = previousId.HasValue
                ? Papds.FirstOrDefault(p => p.Id == previousId.Value) ?? Papds.FirstOrDefault()
                : Papds.FirstOrDefault();
        }

        private async Task LoadEngagementsAsync()
        {
            _engagementCache = await _engagementService.GetAllAsync();

            var options = _engagementCache
                .OrderBy(e => e.Description, StringComparer.OrdinalIgnoreCase)
                .Select(e => new EngagementOption(e.Id, e.EngagementId, e.Description))
                .ToList();

            var previousId = SelectedEngagement?.InternalId;
            Engagements = new ObservableCollection<EngagementOption>(options);
            SelectedEngagement = previousId.HasValue
                ? Engagements.FirstOrDefault(e => e.InternalId == previousId.Value) ?? Engagements.FirstOrDefault()
                : Engagements.FirstOrDefault();
        }

        private async Task LoadAssignmentsAsync()
        {
            if (SelectedPapd is null)
            {
                Assignments = new ObservableCollection<PapdAssignmentDisplayItem>();
                SelectedAssignment = null;
                return;
            }

            if (_engagementCache.Count == 0)
            {
                await LoadEngagementsAsync();
            }

            var items = _engagementCache
                .SelectMany(e => e.EngagementPapds.Select(a => (Engagement: e, Assignment: a)))
                .Where(pair => pair.Assignment.PapdId == SelectedPapd.Id)
                .Select(pair => new PapdAssignmentDisplayItem(
                    pair.Assignment.Id,
                    pair.Engagement.Id,
                    pair.Engagement.EngagementId,
                    pair.Engagement.Description,
                    pair.Assignment.EffectiveDate))
                .OrderBy(item => item.EffectiveDate)
                .ThenBy(item => item.EngagementDisplay, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assignments = new ObservableCollection<PapdAssignmentDisplayItem>(items);
            SelectedAssignment = Assignments.FirstOrDefault();
        }

        private async Task RefreshAssignmentsAsync()
        {
            await LoadEngagementsAsync();
            await LoadAssignmentsAsync();
        }

        private async Task<Engagement?> EnsureEngagementAsync(int engagementId)
        {
            var engagement = _engagementCache.FirstOrDefault(e => e.Id == engagementId);
            if (engagement is not null)
            {
                return engagement;
            }

            engagement = await _engagementService.GetByIdAsync(engagementId);
            if (engagement is not null)
            {
                _engagementCache.Add(engagement);
            }

            return engagement;
        }
    }

    public record PapdAssignmentDisplayItem(
        int AssignmentId,
        int EngagementInternalId,
        string EngagementId,
        string EngagementDescription,
        DateTime EffectiveDate)
    {
        public string EngagementDisplay => string.IsNullOrWhiteSpace(EngagementId)
            ? EngagementDescription
            : $"{EngagementId} - {EngagementDescription}";

        public string EffectiveDateDisplay => EffectiveDate.ToString("yyyy-MM-dd");
    }
}
