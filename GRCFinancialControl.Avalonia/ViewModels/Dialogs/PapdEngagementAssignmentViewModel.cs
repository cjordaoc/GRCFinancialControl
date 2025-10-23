using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class PapdEngagementAssignmentViewModel : ViewModelBase
    {
        private readonly IEngagementService _engagementService;
        private readonly EngagementPapd _assignment;

        [ObservableProperty]
        private ObservableCollection<EngagementOption> _engagements = new();

        [ObservableProperty]
        private EngagementOption? _selectedEngagement;

        [ObservableProperty]
        private bool _isReadOnlyMode;

        public Papd Papd { get; }

        public string Title => _assignment.Id == 0
            ? LocalizationRegistry.Get("Admin.PapdAssignments.Title.Editor")
            : LocalizationRegistry.Get("Admin.PapdAssignments.Title.Editor");

        public PapdEngagementAssignmentViewModel(
            Papd papd,
            EngagementPapd assignment,
            IEngagementService engagementService,
            IMessenger messenger,
            bool isReadOnlyMode = false)
            : base(messenger)
        {
            Papd = papd ?? throw new ArgumentNullException(nameof(papd));
            _assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));
            _engagementService = engagementService ?? throw new ArgumentNullException(nameof(engagementService));
            IsReadOnlyMode = isReadOnlyMode;
        }

        public bool AllowEditing => !IsReadOnlyMode;

        public override async Task LoadDataAsync()
        {
            var engagements = await _engagementService.GetAllAsync();

            var options = engagements
                .OrderBy(e => e.Description, StringComparer.OrdinalIgnoreCase)
                .Select(e => new EngagementOption(e.Id, e.EngagementId, e.Description))
                .ToList();

            Engagements = new ObservableCollection<EngagementOption>(options);

            if (_assignment.EngagementId != 0)
            {
                SelectedEngagement = Engagements.FirstOrDefault(e => e.InternalId == _assignment.EngagementId)
                    ?? Engagements.FirstOrDefault();
            }
            else
            {
                SelectedEngagement = Engagements.FirstOrDefault();
            }
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (SelectedEngagement is null)
            {
                return;
            }

            if (_assignment.Id == 0)
            {
                await AddAssignmentAsync(SelectedEngagement.InternalId);
            }
            else
            {
                await UpdateAssignmentAsync(SelectedEngagement.InternalId);
            }
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanSave() => SelectedEngagement is not null && AllowEditing;

        private async Task AddAssignmentAsync(int engagementId)
        {
            var engagement = await _engagementService.GetByIdAsync(engagementId);
            if (engagement is null)
            {
                return;
            }

            var exists = engagement.EngagementPapds.Any(a => a.PapdId == Papd.Id);
            if (exists)
            {
                Messenger.Send(new CloseDialogMessage(false));
                return;
            }

            engagement.EngagementPapds.Add(new EngagementPapd
            {
                EngagementId = engagement.Id,
                PapdId = Papd.Id
            });

            await _engagementService.UpdateAsync(engagement);
            Messenger.Send(new CloseDialogMessage(true));
        }

        private async Task UpdateAssignmentAsync(int newEngagementId)
        {
            var currentEngagement = await _engagementService.GetByIdAsync(_assignment.EngagementId);
            if (currentEngagement is null)
            {
                return;
            }

            var existingAssignment = currentEngagement.EngagementPapds.FirstOrDefault(a => a.Id == _assignment.Id);
            if (existingAssignment is null)
            {
                return;
            }

            if (currentEngagement.Id == newEngagementId)
            {
                Messenger.Send(new CloseDialogMessage(true));
                return;
            }

            currentEngagement.EngagementPapds.Remove(existingAssignment);
            await _engagementService.UpdateAsync(currentEngagement);

            var targetEngagement = await _engagementService.GetByIdAsync(newEngagementId);
            if (targetEngagement is null)
            {
                return;
            }

            if (targetEngagement.EngagementPapds.Any(a => a.PapdId == Papd.Id))
            {
                Messenger.Send(new CloseDialogMessage(true));
                return;
            }

            targetEngagement.EngagementPapds.Add(new EngagementPapd
            {
                EngagementId = targetEngagement.Id,
                PapdId = Papd.Id
            });

            await _engagementService.UpdateAsync(targetEngagement);
            Messenger.Send(new CloseDialogMessage(true));
        }
    }

    public record EngagementOption(int InternalId, string EngagementId, string Description)
    {
        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(EngagementId))
            {
                return Description;
            }

            return LocalizationRegistry.Format(
                "Admin.PapdAssignments.Format.EngagementDisplay",
                EngagementId,
                Description);
        }
    }
}
