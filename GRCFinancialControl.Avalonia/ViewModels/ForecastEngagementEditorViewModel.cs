using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public sealed partial class ForecastEngagementEditorViewModel : ViewModelBase
    {
        private readonly IStaffAllocationForecastService _forecastService;
        private Dictionary<(string Rank, int FiscalYearId), decimal> _baseline = new();

        public ForecastEngagementEditorViewModel(
            EngagementForecastSummary summary,
            IReadOnlyList<ForecastAllocationRow> rows,
            IStaffAllocationForecastService forecastService,
            IMessenger messenger)
            : base(messenger)
        {
            Engagement = summary ?? throw new ArgumentNullException(nameof(summary));
            ArgumentNullException.ThrowIfNull(rows);
            _forecastService = forecastService ?? throw new ArgumentNullException(nameof(forecastService));

            FiscalYears = new ObservableCollection<ForecastEngagementFiscalYearViewModel>(
                rows
                    .GroupBy(r => r.FiscalYearId)
                    .Select(group => new ForecastEngagementFiscalYearViewModel(
                        group.Key,
                        group.First().FiscalYearName))
                    .OrderBy(fy => fy.Name, StringComparer.OrdinalIgnoreCase));

            Rows = new ObservableCollection<ForecastEngagementRowViewModel>();

            var rowGroups = rows
                .GroupBy(r => r.Rank ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (rowGroups.Count == 0 && FiscalYears.Count > 0)
            {
                var emptyRow = CreateEmptyRow();
                Rows.Add(emptyRow);
                SubscribeRow(emptyRow);
            }
            else
            {
                foreach (var group in rowGroups)
                {
                    var cells = new List<ForecastEngagementCellViewModel>(FiscalYears.Count);
                    foreach (var fiscalYear in FiscalYears)
                    {
                        var match = group.FirstOrDefault(r => r.FiscalYearId == fiscalYear.Id);
                        cells.Add(new ForecastEngagementCellViewModel(
                            fiscalYear.Id,
                            fiscalYear.Name,
                            match?.ForecastHours ?? 0m,
                            match?.ActualsHours ?? 0m));
                    }

                    var rowViewModel = new ForecastEngagementRowViewModel(group.Key, cells);
                    Rows.Add(rowViewModel);
                    SubscribeRow(rowViewModel);
                }
            }

            UpdateTotals();
            UpdateSnapshot();
        }

        public EngagementForecastSummary Engagement { get; }

        public ObservableCollection<ForecastEngagementFiscalYearViewModel> FiscalYears { get; }

        public ObservableCollection<ForecastEngagementRowViewModel> Rows { get; }

        public string Title => $"{Engagement.EngagementCode} · {Engagement.EngagementName}";

        public decimal InitialHoursBudget => Engagement.InitialHoursBudget;

        public decimal ActualHoursBudget => Engagement.ActualHours;

        [ObservableProperty]
        private decimal _totalForecastHours;

        [ObservableProperty]
        private decimal _remainingHours;

        [ObservableProperty]
        private bool _hasChanges;

        [ObservableProperty]
        private bool _isSaving;

        [ObservableProperty]
        private string? _statusMessage;

        partial void OnIsSavingChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void AddRow()
        {
            var newRow = CreateEmptyRow();
            Rows.Add(newRow);
            SubscribeRow(newRow);
            UpdateTotals();
            CheckForChanges();
            RemoveRowCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanRemoveRow))]
        private void RemoveRow(ForecastEngagementRowViewModel row)
        {
            if (row is null || !CanRemoveRow(row))
            {
                return;
            }

            UnsubscribeRow(row);
            Rows.Remove(row);
            UpdateTotals();
            CheckForChanges();
            RemoveRowCommand.NotifyCanExecuteChanged();
        }

        private bool CanRemoveRow(ForecastEngagementRowViewModel row)
        {
            return row is not null && row.CanDelete;
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            if (!CanSave())
            {
                return;
            }

            StatusMessage = null;

            if (Rows.Any(row => string.IsNullOrWhiteSpace(row.Rank)))
            {
                StatusMessage = "Informe o rank para todas as linhas antes de salvar.";
                return;
            }

            var entries = new List<EngagementForecastUpdateEntry>();
            foreach (var row in Rows)
            {
                var normalizedRank = row.Rank!.Trim();
                foreach (var cell in row.Cells)
                {
                    entries.Add(new EngagementForecastUpdateEntry(
                        cell.FiscalYearId,
                        normalizedRank,
                        decimal.Round(cell.ForecastHours, 2)));
                }
            }

            try
            {
                IsSaving = true;
                await _forecastService.SaveEngagementForecastAsync(Engagement.EngagementId, entries);
                UpdateSnapshot();
                StatusMessage = "Previsão salva com sucesso.";
                Messenger.Send(new RefreshDataMessage());
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao salvar forecast: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        private bool CanSave()
        {
            if (IsSaving || !HasChanges)
            {
                return false;
            }

            return Rows.All(row => !string.IsNullOrWhiteSpace(row.Rank));
        }

        [RelayCommand]
        private void Close()
        {
            Messenger.Send(new CloseDialogMessage(false));
        }

        private ForecastEngagementRowViewModel CreateEmptyRow()
        {
            var cells = FiscalYears
                .Select(fy => new ForecastEngagementCellViewModel(fy.Id, fy.Name, 0m, 0m))
                .ToList();

            return new ForecastEngagementRowViewModel(string.Empty, cells);
        }

        private void SubscribeRow(ForecastEngagementRowViewModel row)
        {
            row.PropertyChanged += OnRowPropertyChanged;
            foreach (var cell in row.Cells)
            {
                cell.PropertyChanged += OnCellPropertyChanged;
            }
        }

        private void UnsubscribeRow(ForecastEngagementRowViewModel row)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
            foreach (var cell in row.Cells)
            {
                cell.PropertyChanged -= OnCellPropertyChanged;
            }
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ForecastEngagementRowViewModel.Rank))
            {
                CheckForChanges();
            }
        }

        private void OnCellPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ForecastEngagementCellViewModel.ForecastHours))
            {
                UpdateTotals();
                CheckForChanges();
            }
        }

        private void UpdateTotals()
        {
            TotalForecastHours = Rows.Sum(row => row.Cells.Sum(cell => cell.ForecastHours));
            RemainingHours = InitialHoursBudget - (ActualHoursBudget + TotalForecastHours);
        }

        private void UpdateSnapshot()
        {
            _baseline = Rows
                .SelectMany(row => row.Cells.Select(cell => KeyValuePair.Create((NormalizeRankKey(row.Rank), cell.FiscalYearId), cell.ForecastHours)))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            CheckForChanges();
        }

        private void CheckForChanges()
        {
            var current = new Dictionary<(string Rank, int FiscalYearId), decimal>();
            foreach (var row in Rows)
            {
                var rankKey = NormalizeRankKey(row.Rank);
                foreach (var cell in row.Cells)
                {
                    current[(rankKey, cell.FiscalYearId)] = cell.ForecastHours;
                }
            }

            var hasEmptyRank = Rows.Any(row => string.IsNullOrWhiteSpace(row.Rank));
            var changed = hasEmptyRank || current.Count != _baseline.Count;

            if (!changed)
            {
                foreach (var kvp in current)
                {
                    if (!_baseline.TryGetValue(kvp.Key, out var original) ||
                        Math.Abs(original - kvp.Value) > 0.009m)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            HasChanges = changed;
            SaveCommand.NotifyCanExecuteChanged();

            if (changed)
            {
                StatusMessage = null;
            }
        }

        private static string NormalizeRankKey(string? rank)
        {
            return string.IsNullOrWhiteSpace(rank)
                ? string.Empty
                : rank.Trim().ToUpperInvariant();
        }

        public sealed class ForecastEngagementRowViewModel : ObservableObject
        {
            public ForecastEngagementRowViewModel(string rank, IEnumerable<ForecastEngagementCellViewModel> cells)
            {
                _rank = Normalize(rank);
                Cells = new ObservableCollection<ForecastEngagementCellViewModel>(cells);
                CanDelete = !Cells.Any(cell => cell.HasActuals);
            }

            private string _rank;

            public string Rank
            {
                get => _rank;
                set
                {
                    var normalized = Normalize(value);
                    SetProperty(ref _rank, normalized);
                }
            }

            public ObservableCollection<ForecastEngagementCellViewModel> Cells { get; }

            public bool CanDelete { get; }

            private static string Normalize(string? value)
            {
                return string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : value.Trim();
            }
        }

        public sealed partial class ForecastEngagementCellViewModel : ObservableObject
        {
            public ForecastEngagementCellViewModel(int fiscalYearId, string fiscalYearName, decimal forecastHours, decimal actualHours)
            {
                FiscalYearId = fiscalYearId;
                FiscalYearName = fiscalYearName;
                _forecastHours = forecastHours;
                ActualHours = actualHours;
            }

            public int FiscalYearId { get; }

            public string FiscalYearName { get; }

            [ObservableProperty]
            private decimal _forecastHours;

            public decimal ActualHours { get; }

            public bool HasActuals => ActualHours > 0m;
        }

        public sealed class ForecastEngagementFiscalYearViewModel
        {
            public ForecastEngagementFiscalYearViewModel(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public int Id { get; }

            public string Name { get; }
        }
    }
}
