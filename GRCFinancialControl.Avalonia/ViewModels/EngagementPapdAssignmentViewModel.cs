using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementPapdAssignmentViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<EngagementPapd> _assignments;

        [ObservableProperty]
        private EngagementPapd _selectedAssignment;

        [ObservableProperty]
        private ObservableCollection<Papd> _availablePapds;

        [ObservableProperty]
        private Papd _selectedPapd;

        [ObservableProperty]
        private DateTime _effectiveDate = DateTime.Today;

        public EngagementPapdAssignmentViewModel(ObservableCollection<EngagementPapd> assignments, IEnumerable<Papd> availablePapds)
        {
            Assignments = assignments;
            AvailablePapds = new ObservableCollection<Papd>(availablePapds);
            SelectedPapd = AvailablePapds.FirstOrDefault();
        }

        [RelayCommand]
        private void AddAssignment()
        {
            if (SelectedPapd == null) return;

            var newAssignment = new EngagementPapd
            {
                Papd = SelectedPapd,
                EffectiveDate = EffectiveDate
            };
            Assignments.Add(newAssignment);
        }

        [RelayCommand]
        private void RemoveAssignment()
        {
            if (SelectedAssignment != null)
            {
                Assignments.Remove(SelectedAssignment);
            }
        }
    }
}