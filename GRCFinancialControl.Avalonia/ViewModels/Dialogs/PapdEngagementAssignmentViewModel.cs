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
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class PapdEngagementAssignmentViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;

        [ObservableProperty]
        private ObservableCollection<PapdAssignmentItem> _assignments = new();

        [ObservableProperty]
        private PapdAssignmentItem? _selectedAssignment;

        [ObservableProperty]
        private ObservableCollection<EngagementOption> _engagements = new();

        [ObservableProperty]
        private EngagementOption? _selectedEngagement;

        [ObservableProperty]
        private DateTime? _effectiveDate = DateTime.Today;

        public Papd Papd { get; }

        public PapdEngagementAssignmentViewModel(Papd papd, IEngagementService engagementService, IMessenger messenger)
            : base(messenger)
        {
            Papd = papd;
            _engagementService = engagementService;
        }

        public DateTimeOffset? EffectiveDateOffset
        {
            get => DateTimeOffsetHelper.FromDate(EffectiveDate);
            set => EffectiveDate = DateTimeOffsetHelper.ToDate(value);
        }

        public override async Task LoadDataAsync()
        {
            var engagements = await _engagementService.GetAllAsync();

            var engagementOptions = engagements
                .OrderBy(e => e.Description, StringComparer.OrdinalIgnoreCase)
                .Select(e => new EngagementOption(e.Id, e.EngagementId, e.Description))
                .ToList();

            Engagements = new ObservableCollection<EngagementOption>(engagementOptions);
            if (SelectedEngagement is null)
            {
                SelectedEngagement = Engagements.FirstOrDefault();
            }

            var assignmentItems = engagements
                .SelectMany(e => e.EngagementPapds.Select(a => (Engagement: e, Assignment: a)))
                .Where(pair => pair.Assignment.PapdId == Papd.Id)
                .Select(pair => new PapdAssignmentItem(
                    pair.Assignment.Id,
                    pair.Engagement.Id,
                    pair.Engagement.EngagementId,
                    pair.Engagement.Description,
                    pair.Assignment.EffectiveDate))
                .OrderBy(a => a.EffectiveDate)
                .ThenBy(a => a.EngagementId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assignments = new ObservableCollection<PapdAssignmentItem>(assignmentItems);
            SelectedAssignment = Assignments.FirstOrDefault();
        }

        [RelayCommand(CanExecute = nameof(CanAssign))]
        private async Task AssignAsync()
        {
            if (SelectedEngagement is null || EffectiveDate is null)
            {
                return;
            }

            var engagement = await _engagementService.GetByIdAsync(SelectedEngagement.InternalId);
            if (engagement is null)
            {
                return;
            }

            var effectiveDate = EffectiveDate.Value.Date;
            var hasExisting = engagement.EngagementPapds.Any(a => a.PapdId == Papd.Id && a.EffectiveDate == effectiveDate);
            if (!hasExisting)
            {
                engagement.EngagementPapds.Add(new EngagementPapd
                {
                    EngagementId = engagement.Id,
                    PapdId = Papd.Id,
                    EffectiveDate = effectiveDate
                });

                await _engagementService.UpdateAsync(engagement);
            }

            await LoadDataAsync();
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand(CanExecute = nameof(CanRemoveAssignment))]
        private async Task RemoveAssignmentAsync()
        {
            if (SelectedAssignment is null)
            {
                return;
            }

            var engagement = await _engagementService.GetByIdAsync(SelectedAssignment.InternalEngagementId);
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

            await LoadDataAsync();
            Messenger.Send(new RefreshDataMessage());
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanAssign() => SelectedEngagement is not null && EffectiveDate is not null;

        private bool CanRemoveAssignment() => SelectedAssignment is not null;

        partial void OnSelectedAssignmentChanged(PapdAssignmentItem? value)
        {
            RemoveAssignmentCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedEngagementChanged(EngagementOption? value)
        {
            AssignCommand.NotifyCanExecuteChanged();
        }

        partial void OnEffectiveDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(EffectiveDateOffset));
            AssignCommand.NotifyCanExecuteChanged();
        }
    }

    public record PapdAssignmentItem(int AssignmentId, int InternalEngagementId, string EngagementId, string EngagementDescription, DateTime EffectiveDate);

    public record EngagementOption(int InternalId, string EngagementId, string Description)
    {
        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(EngagementId))
            {
                return Description;
            }

            return $"{EngagementId} - {Description}";
        }
    }
}
