using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services;
using GRCFinancialControl.Avalonia.ViewModels.Dialogs;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class PapdAssignmentsViewModel : ViewModelBase
    {
        private readonly IPapdService _papdService;
        private readonly IEngagementService _engagementService;
        private readonly DialogService _dialogService;
        private List<Engagement> _engagementCache = new();

        [ObservableProperty]
        private ObservableCollection<Papd> _papds = new();

        [ObservableProperty]
        private Papd? _selectedPapd;

        [ObservableProperty]
        private ObservableCollection<PapdAssignmentItem> _assignments = new();

        [ObservableProperty]
        private PapdAssignmentItem? _selectedAssignment;

        public PapdAssignmentsViewModel(
            IPapdService papdService,
            IEngagementService engagementService,
            DialogService dialogService,
            IMessenger messenger)
            : base(messenger)
        {
            _papdService = papdService;
            _engagementService = engagementService;
            _dialogService = dialogService;
        }

        public override async Task LoadDataAsync()
        {
            await LoadPapdsAsync();
            await RefreshEngagementCacheAsync();
            await LoadAssignmentsAsync();
        }

        [RelayCommand(CanExecute = nameof(CanModifyAssignments))]
        private async Task AddAsync()
        {
            if (SelectedPapd is null)
            {
                return;
            }

            var assignment = new EngagementPapd
            {
                PapdId = SelectedPapd.Id,
                Papd = SelectedPapd
            };

            var editorViewModel = new PapdEngagementAssignmentViewModel(
                SelectedPapd,
                assignment,
                _engagementService,
                Messenger);

            await editorViewModel.LoadDataAsync();
            var result = await _dialogService.ShowDialogAsync(editorViewModel);
            if (result)
            {
                Messenger.Send(new RefreshDataMessage());
                await RefreshEngagementCacheAsync();
                await LoadAssignmentsAsync();
            }
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task EditAsync()
        {
            if (SelectedPapd is null || SelectedAssignment is null)
            {
                return;
            }

            var engagement = await EnsureEngagementAsync(SelectedAssignment.EngagementInternalId);
            if (engagement is null)
            {
                ToastService.ShowError(
                    "Admin.PapdAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.PapdAssignments.Error.EngagementMissing"));
                return;
            }

            var assignment = engagement.EngagementPapds.FirstOrDefault(a => a.Id == SelectedAssignment.AssignmentId);
            if (assignment is null)
            {
                ToastService.ShowWarning(
                    "Admin.PapdAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.PapdAssignments.Error.AssignmentMissing"));
                return;
            }

            var editorViewModel = new PapdEngagementAssignmentViewModel(
                SelectedPapd,
                assignment,
                _engagementService,
                Messenger);

            await editorViewModel.LoadDataAsync();
            var result = await _dialogService.ShowDialogAsync(editorViewModel);
            if (result)
            {
                Messenger.Send(new RefreshDataMessage());
                await RefreshEngagementCacheAsync();
                await LoadAssignmentsAsync();
            }
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task ViewAsync()
        {
            if (SelectedPapd is null || SelectedAssignment is null)
            {
                return;
            }

            var engagement = await EnsureEngagementAsync(SelectedAssignment.EngagementInternalId);
            if (engagement is null)
            {
                ToastService.ShowError(
                    "Admin.PapdAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.PapdAssignments.Error.EngagementMissing"));
                return;
            }

            var assignment = engagement.EngagementPapds.FirstOrDefault(a => a.Id == SelectedAssignment.AssignmentId);
            if (assignment is null)
            {
                ToastService.ShowWarning(
                    "Admin.PapdAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.PapdAssignments.Error.AssignmentMissing"));
                return;
            }

            var editorViewModel = new PapdEngagementAssignmentViewModel(
                SelectedPapd,
                assignment,
                _engagementService,
                Messenger,
                isReadOnlyMode: true);

            await editorViewModel.LoadDataAsync();
            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task DeleteAsync()
        {
            if (SelectedPapd is null || SelectedAssignment is null)
            {
                return;
            }

            var engagement = await EnsureEngagementAsync(SelectedAssignment.EngagementInternalId);
            if (engagement is null)
            {
                ToastService.ShowError(
                    "Admin.PapdAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.PapdAssignments.Error.EngagementMissing"));
                return;
            }

            var assignment = engagement.EngagementPapds.FirstOrDefault(a => a.Id == SelectedAssignment.AssignmentId);
            if (assignment is null)
            {
                ToastService.ShowWarning(
                    "Admin.PapdAssignments.Toast.OperationFailed",
                    LocalizationRegistry.Get("Admin.PapdAssignments.Error.AssignmentMissing"));
                return;
            }

            engagement.EngagementPapds.Remove(assignment);
            try
            {
                await _engagementService.UpdateAsync(engagement);

                ToastService.ShowSuccess(
                    "Admin.PapdAssignments.Toast.DeleteSuccess",
                    SelectedPapd.Name,
                    SelectedAssignment.EngagementDisplay);

                Messenger.Send(new RefreshDataMessage());
                await RefreshEngagementCacheAsync();
                await LoadAssignmentsAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError("Admin.PapdAssignments.Toast.OperationFailed", ex.Message);
            }
        }

        private bool CanModifyAssignments() => SelectedPapd is not null;

        private bool CanModifySelection() => SelectedPapd is not null && SelectedAssignment is not null;

        partial void OnSelectedPapdChanged(Papd? value)
        {
            _ = LoadAssignmentsAsync();
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedAssignmentChanged(PapdAssignmentItem? value)
        {
            EditCommand.NotifyCanExecuteChanged();
            ViewCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
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

        private async Task RefreshEngagementCacheAsync()
        {
            _engagementCache = await _engagementService.GetAllAsync();
        }

        private async Task LoadAssignmentsAsync()
        {
            if (SelectedPapd is null)
            {
                Assignments = new ObservableCollection<PapdAssignmentItem>();
                SelectedAssignment = null;
                return;
            }

            if (_engagementCache.Count == 0)
            {
                await RefreshEngagementCacheAsync();
            }

            var items = _engagementCache
                .SelectMany(e => e.EngagementPapds.Select(a => (Engagement: e, Assignment: a)))
                .Where(pair => pair.Assignment.PapdId == SelectedPapd.Id)
                .Select(pair => new PapdAssignmentItem(
                    pair.Assignment.Id,
                    pair.Engagement.Id,
                    pair.Engagement.EngagementId,
                    pair.Engagement.Description))
                .OrderBy(item => item.EngagementDisplay, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assignments = new ObservableCollection<PapdAssignmentItem>(items);
            SelectedAssignment = Assignments.FirstOrDefault();
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

    public record PapdAssignmentItem(
        int AssignmentId,
        int EngagementInternalId,
        string EngagementId,
        string EngagementDescription)
    {
        public string EngagementDisplay => string.IsNullOrWhiteSpace(EngagementId)
            ? EngagementDescription
            : $"{EngagementId} - {EngagementDescription}";
    }
}
