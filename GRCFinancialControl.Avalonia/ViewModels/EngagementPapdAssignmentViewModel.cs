using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementPapdAssignmentViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<EngagementPapd> _assignments;

        [ObservableProperty]
        private EngagementPapd? _selectedAssignment;

        [ObservableProperty]
        private ObservableCollection<Papd> _availablePapds;

        [ObservableProperty]
        private Papd? _selectedPapd;

        [ObservableProperty]
        private DateTimeOffset? _effectiveDate = DateTimeOffset.Now;

        public EngagementPapdAssignmentViewModel(ObservableCollection<EngagementPapd> assignments, IEnumerable<Papd> availablePapds, IMessenger messenger)
            : base(messenger)
        {
            Assignments = assignments ?? new ObservableCollection<EngagementPapd>();
            AvailablePapds = new ObservableCollection<Papd>(availablePapds ?? Enumerable.Empty<Papd>());
            SelectedPapd = AvailablePapds.FirstOrDefault();
        }

        [RelayCommand(CanExecute = nameof(CanAddAssignment))]
        private void AddAssignment()
        {
            if (SelectedPapd == null || EffectiveDate is null) return;

            var newAssignment = new EngagementPapd
            {
                Papd = SelectedPapd,
                EffectiveDate = EffectiveDate.Value.Date
            };
            Assignments.Add(newAssignment);
            SelectedAssignment = newAssignment;
        }

        [RelayCommand(CanExecute = nameof(CanRemoveAssignment))]
        private void RemoveAssignment()
        {
            if (SelectedAssignment != null)
            {
                Assignments.Remove(SelectedAssignment);
                SelectedAssignment = Assignments.LastOrDefault();
            }
        }

        [RelayCommand]
        private void Confirm()
        {
            Messenger.Send(new CloseDialogMessage(true));
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private bool CanAddAssignment() => SelectedPapd is not null && EffectiveDate is not null;

        private bool CanRemoveAssignment() => SelectedAssignment is not null;

        partial void OnSelectedPapdChanged(Papd? value)
        {
            AddAssignmentCommand.NotifyCanExecuteChanged();
        }

        partial void OnEffectiveDateChanged(DateTimeOffset? value)
        {
            AddAssignmentCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedAssignmentChanged(EngagementPapd? value)
        {
            RemoveAssignmentCommand.NotifyCanExecuteChanged();
        }
    }
}