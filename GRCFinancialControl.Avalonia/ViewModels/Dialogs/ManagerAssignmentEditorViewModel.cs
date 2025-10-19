using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using App.Presentation.Controls;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Utilities;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels.Dialogs
{
    public partial class ManagerAssignmentEditorViewModel : ViewModelBase, IModalOverlayActionProvider
    {
        private readonly IManagerAssignmentService _assignmentService;
        private readonly EngagementManagerAssignment _assignment;

        [ObservableProperty]
        private ObservableCollection<EngagementOption> _engagements;

        [ObservableProperty]
        private EngagementOption? _selectedEngagement;

        [ObservableProperty]
        private ObservableCollection<Manager> _managers;

        [ObservableProperty]
        private Manager? _selectedManager;

        [ObservableProperty]
        private DateTime? _beginDate;

        [ObservableProperty]
        private DateTime? _endDate;

        [ObservableProperty]
        private bool _isReadOnlyMode;

        public string Title => _assignment.Id == 0
            ? LocalizationRegistry.Get("Admin.ManagerAssignments.Dialog.Add.Title")
            : LocalizationRegistry.Get("Admin.ManagerAssignments.Dialog.Edit.Title");

        public ManagerAssignmentEditorViewModel(
            EngagementManagerAssignment assignment,
            ObservableCollection<EngagementOption> engagements,
            ObservableCollection<Manager> managers,
            IManagerAssignmentService assignmentService,
            IMessenger messenger,
            bool isReadOnlyMode = false)
            : base(messenger)
        {
            _assignment = assignment;
            _assignmentService = assignmentService;
            _engagements = engagements;
            _managers = managers;

            _beginDate = assignment.BeginDate == default ? DateTime.Today : assignment.BeginDate;
            _endDate = assignment.EndDate;
            IsReadOnlyMode = isReadOnlyMode;
        }

        public bool AllowEditing => !IsReadOnlyMode;

        public bool IsPrimaryActionVisible => AllowEditing;

        public string? PrimaryActionText => LocalizationRegistry.Get("Common.Button.Save");

        public ICommand? PrimaryActionCommand => SaveCommand;

        public DateTimeOffset? BeginDateOffset
        {
            get => DateTimeOffsetHelper.FromDate(BeginDate);
            set => BeginDate = DateTimeOffsetHelper.ToDate(value);
        }

        public DateTimeOffset? EndDateOffset
        {
            get => DateTimeOffsetHelper.FromDate(EndDate);
            set => EndDate = DateTimeOffsetHelper.ToDate(value);
        }

        public override Task LoadDataAsync()
        {
            if (_assignment.Id > 0)
            {
                SelectedEngagement ??= Engagements.FirstOrDefault(e => e.InternalId == _assignment.EngagementId);
                SelectedManager ??= Managers.FirstOrDefault(m => m.Id == _assignment.ManagerId);
            }

            return Task.CompletedTask;
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (SelectedEngagement is null || SelectedManager is null)
            {
                return;
            }

            if (BeginDate is null)
            {
                return;
            }

            var beginDate = BeginDate.Value.Date;
            var endDate = EndDate?.Date;
            if (endDate.HasValue && endDate.Value < beginDate)
            {
                return;
            }

            _assignment.EngagementId = SelectedEngagement.InternalId;
            _assignment.ManagerId = SelectedManager.Id;
            _assignment.BeginDate = beginDate;
            _assignment.EndDate = endDate;

            if (_assignment.Id == 0)
            {
                await _assignmentService.AddAsync(_assignment);
            }
            else
            {
                await _assignmentService.UpdateAsync(_assignment);
            }

            Messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanSave() => SelectedEngagement is not null && SelectedManager is not null && BeginDate.HasValue && !IsReadOnlyMode;

        partial void OnSelectedEngagementChanged(EngagementOption? value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedManagerChanged(Manager? value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnBeginDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(BeginDateOffset));
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnEndDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(EndDateOffset));
        }

        partial void OnIsReadOnlyModeChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AllowEditing));
            OnPropertyChanged(nameof(IsPrimaryActionVisible));
        }
    }
}
