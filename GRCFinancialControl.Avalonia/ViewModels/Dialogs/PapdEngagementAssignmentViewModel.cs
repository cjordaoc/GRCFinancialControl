using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using GRC.Shared.UI.Messages;

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
                ToastService.ShowError(
                    "Admin.PapdAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.PapdAssignments.Error.EngagementMissing"));
                return;
            }

            var exists = engagement.EngagementPapds.Any(a => a.PapdId == Papd.Id);
            if (exists)
            {
                ToastService.ShowWarning(
                    "Admin.PapdAssignments.Toast.Exists",
                    Papd.Name,
                    BuildEngagementDisplay(SelectedEngagement, engagement));
                return;
            }

            engagement.EngagementPapds.Add(new EngagementPapd
            {
                EngagementId = engagement.Id,
                PapdId = Papd.Id
            });

            try
            {
                await _engagementService.UpdateAsync(engagement);
                ToastService.ShowSuccess(
                    "Admin.PapdAssignments.Toast.SaveSuccess",
                    Papd.Name,
                    BuildEngagementDisplay(SelectedEngagement, engagement));
                Messenger.Send(new CloseDialogMessage(true));
            }
            catch (Exception ex)
            {
                ToastService.ShowError("Admin.PapdAssignments.Toast.OperationFailed", ex.Message);
            }
        }

        private async Task UpdateAssignmentAsync(int newEngagementId)
        {
            var currentEngagement = await _engagementService.GetByIdAsync(_assignment.EngagementId);
            if (currentEngagement is null)
            {
                ToastService.ShowError(
                    "Admin.PapdAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.PapdAssignments.Error.EngagementMissing"));
                return;
            }

            var existingAssignment = currentEngagement.EngagementPapds.FirstOrDefault(a => a.Id == _assignment.Id);
            if (existingAssignment is null)
            {
                ToastService.ShowError(
                    "Admin.PapdAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.PapdAssignments.Error.AssignmentMissing"));
                return;
            }

            if (currentEngagement.Id == newEngagementId)
            {
                ToastService.ShowWarning(
                    "Admin.PapdAssignments.Toast.Exists",
                    Papd.Name,
                    BuildEngagementDisplay(SelectedEngagement, currentEngagement));
                return;
            }

            currentEngagement.EngagementPapds.Remove(existingAssignment);
            try
            {
                await _engagementService.UpdateAsync(currentEngagement);
            }
            catch (Exception ex)
            {
                ToastService.ShowError("Admin.PapdAssignments.Toast.OperationFailed", ex.Message);
                return;
            }

            var targetEngagement = await _engagementService.GetByIdAsync(newEngagementId);
            if (targetEngagement is null)
            {
                ToastService.ShowError(
                    "Admin.PapdAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.PapdAssignments.Error.EngagementMissing"));
                return;
            }

            if (targetEngagement.EngagementPapds.Any(a => a.PapdId == Papd.Id))
            {
                ToastService.ShowWarning(
                    "Admin.PapdAssignments.Toast.Exists",
                    Papd.Name,
                    BuildEngagementDisplay(SelectedEngagement, targetEngagement));
                return;
            }

            targetEngagement.EngagementPapds.Add(new EngagementPapd
            {
                EngagementId = targetEngagement.Id,
                PapdId = Papd.Id
            });

            try
            {
                await _engagementService.UpdateAsync(targetEngagement);
                ToastService.ShowSuccess(
                    "Admin.PapdAssignments.Toast.SaveSuccess",
                    Papd.Name,
                    BuildEngagementDisplay(SelectedEngagement, targetEngagement));
                Messenger.Send(new CloseDialogMessage(true));
            }
            catch (Exception ex)
            {
                ToastService.ShowError("Admin.PapdAssignments.Toast.OperationFailed", ex.Message);
            }
        }

        private static string BuildEngagementDisplay(EngagementOption? option, Engagement engagement)
        {
            if (option is not null)
            {
                return option.ToString();
            }

            var engagementId = engagement.EngagementId;
            if (string.IsNullOrWhiteSpace(engagementId))
            {
                return engagement.Description;
            }

            return LocalizationRegistry.Format(
                "Admin.PapdAssignments.Format.EngagementDisplay",
                engagementId,
                engagement.Description);
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
