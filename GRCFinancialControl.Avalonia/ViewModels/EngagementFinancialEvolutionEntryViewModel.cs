using System;
using System.Collections.ObjectModel;
using System.Globalization;
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

        partial void OnSelectedClosingPeriodChanged(ClosingPeriod? value)
        {
            if (_isUpdating)
            {
                return;
            }

            try
            {
                _isUpdating = true;
                if (value is null)
                {
                    ClosingPeriodId = string.Empty;
                }
                else
                {
                    ClosingPeriodId = value.Id.ToString(CultureInfo.InvariantCulture);
                }
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
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var identifier))
                    {
                        SelectedClosingPeriod = _closingPeriods.FirstOrDefault(p => p.Id == identifier);
                    }
                    else
                    {
                        SelectedClosingPeriod = _closingPeriods.FirstOrDefault(p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase));
                    }
                }
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
                if (int.TryParse(ClosingPeriodId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var identifier))
                {
                    SelectedClosingPeriod = _closingPeriods.FirstOrDefault(p => p.Id == identifier);
                }
                else
                {
                    SelectedClosingPeriod = _closingPeriods.FirstOrDefault(p => string.Equals(p.Name, ClosingPeriodId, StringComparison.OrdinalIgnoreCase));
                }
            }
        }
    }
}
