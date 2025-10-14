using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GRCFinancialControl.Core.Models;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class EngagementFinancialEvolutionEntryViewModel : ObservableObject
    {
        private readonly ObservableCollection<ClosingPeriod> _closingPeriods;
        private bool _isUpdating;

        public EngagementFinancialEvolutionEntryViewModel(ObservableCollection<ClosingPeriod> closingPeriods)
        {
            _closingPeriods = closingPeriods;
        }

        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private ClosingPeriod? _selectedClosingPeriod;

        [ObservableProperty]
        private string _closingPeriodId = string.Empty;

        [ObservableProperty]
        private decimal? _hours;

        [ObservableProperty]
        private decimal? _value;

        [ObservableProperty]
        private decimal? _margin;

        [ObservableProperty]
        private decimal? _expenses;

        public bool IsInitialClosingPeriod => string.Equals(ClosingPeriodId, "Initial", StringComparison.OrdinalIgnoreCase);

        public bool CanEditClosingPeriod => !IsInitialClosingPeriod;

        partial void OnSelectedClosingPeriodChanged(ClosingPeriod? value)
        {
            if (_isUpdating)
            {
                return;
            }

            try
            {
                _isUpdating = true;
                ClosingPeriodId = value?.Name ?? ClosingPeriodId;
                OnPropertyChanged(nameof(IsInitialClosingPeriod));
                OnPropertyChanged(nameof(CanEditClosingPeriod));
            }
            finally
            {
                _isUpdating = false;
            }
        }

        partial void OnClosingPeriodIdChanged(string value)
        {
            if (_isUpdating)
            {
                return;
            }

            try
            {
                _isUpdating = true;
                if (string.IsNullOrWhiteSpace(value))
                {
                    SelectedClosingPeriod = null;
                }
                else
                {
                    SelectedClosingPeriod = _closingPeriods.FirstOrDefault(p => p.Name == value);
                }
                OnPropertyChanged(nameof(IsInitialClosingPeriod));
                OnPropertyChanged(nameof(CanEditClosingPeriod));
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void RefreshSelection()
        {
            if (!string.IsNullOrWhiteSpace(ClosingPeriodId))
            {
                SelectedClosingPeriod = _closingPeriods.FirstOrDefault(p => p.Name == ClosingPeriodId);
            }
        }
    }
}
