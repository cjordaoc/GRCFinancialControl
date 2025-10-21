using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Messages;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public sealed partial class ForecastAllocationsViewModel : ViewModelBase, IRecipient<ForecastOperationRequestMessage>
    {
        private readonly IStaffAllocationForecastService _forecastService;
        private readonly IExportService _exportService;
        private readonly ILoggingService _loggingService;
        private readonly IDialogService _dialogService;

        private readonly Dictionary<int, IReadOnlyList<ForecastAllocationRow>> _rowsByEngagement = new();

        [ObservableProperty]
        private ObservableCollection<ForecastAllocationRow> _rows = new();

        [ObservableProperty]
        private ObservableCollection<EngagementForecastSummary> _engagements = new();

        [ObservableProperty]
        private EngagementForecastSummary? _selectedEngagement;

        [ObservableProperty]
        private bool _isBusy;

        public ForecastAllocationsViewModel(
            IStaffAllocationForecastService forecastService,
            IExportService exportService,
            ILoggingService loggingService,
            IDialogService dialogService,
            IMessenger messenger)
            : base(messenger)
        {
            _forecastService = forecastService ?? throw new ArgumentNullException(nameof(forecastService));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public override async Task LoadDataAsync()
        {
            await RefreshInternalAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await RefreshInternalAsync();
        }

        [RelayCommand(CanExecute = nameof(CanEditEngagement))]
        private async Task EditEngagementAsync(EngagementForecastSummary summary)
        {
            if (summary is null)
            {
                return;
            }

            if (!_rowsByEngagement.TryGetValue(summary.EngagementId, out var rows))
            {
                _loggingService.LogWarning(
                    $"Forecast rows for engagement {summary.EngagementId} were not found.");
                return;
            }

            var editorViewModel = new ForecastEngagementEditorViewModel(
                summary,
                rows,
                _forecastService,
                Messenger);

            await _dialogService.ShowDialogAsync(editorViewModel);
        }

        [RelayCommand]
        private async Task ExportPendingAsync()
        {
            if (Rows.Count == 0)
            {
                _loggingService.LogInfo("Nenhum dado de forecast disponível para exportação de pendências.");
                return;
            }

            var pending = Rows
                .Where(row => !string.Equals(row.Status, "OK", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (pending.Count == 0)
            {
                _loggingService.LogInfo("Nenhuma pendência de forecast encontrada.");
                return;
            }

            try
            {
                await _exportService.ExportToExcelAsync(pending, "ForecastPendencias");
                _loggingService.LogInfo("Exportação de pendências concluída com sucesso.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Falha ao exportar pendências: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task GenerateTemplateRetainAsync()
        {
            if (Rows.Count == 0)
            {
                _loggingService.LogWarning("Não há dados para gerar o template Retain.");
                return;
            }

            var templateRows = Rows
                .Select(row => new RetainTemplateRow(
                    row.EngagementCode,
                    row.EngagementName,
                    row.FiscalYearName,
                    row.Rank,
                    row.ForecastHours,
                    row.AvailableHours))
                .ToList();

            try
            {
                await _exportService.ExportToExcelAsync(templateRows, "ForecastRetainTemplate");
                _loggingService.LogInfo("Template Retain gerado com sucesso.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Falha ao gerar template Retain: {ex.Message}");
            }
        }

        private async Task RefreshInternalAsync()
        {
            if (IsBusy)
            {
                return;
            }

            var previouslySelectedId = SelectedEngagement?.EngagementId;

            try
            {
                IsBusy = true;
                var forecast = await _forecastService.GetCurrentForecastAsync();
                var ordered = forecast
                    .OrderBy(row => row.EngagementCode, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => row.FiscalYearName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => row.Rank, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Rows = new ObservableCollection<ForecastAllocationRow>(ordered);

                _rowsByEngagement.Clear();

                var summaries = ordered
                    .GroupBy(row => row.EngagementId)
                    .Select(CreateSummary)
                    .OrderBy(summary => summary.EngagementCode, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var group in ordered.GroupBy(row => row.EngagementId))
                {
                    _rowsByEngagement[group.Key] = group
                        .OrderBy(r => r.FiscalYearName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(r => r.Rank, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                Engagements = new ObservableCollection<EngagementForecastSummary>(summaries);

                if (previouslySelectedId.HasValue)
                {
                    var match = Engagements.FirstOrDefault(e => e.EngagementId == previouslySelectedId.Value);
                    SelectedEngagement = match ?? Engagements.FirstOrDefault();
                }
                else
                {
                    SelectedEngagement = Engagements.FirstOrDefault();
                }

                _loggingService.LogInfo(
                    $"Forecast carregado com {Rows.Count} linhas distribuídas em {Engagements.Count} engagements.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Falha ao carregar forecast: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static EngagementForecastSummary CreateSummary(IGrouping<int, ForecastAllocationRow> group)
        {
            var rows = group.ToList();

            if (rows.Count == 0)
            {
                return new EngagementForecastSummary(0, string.Empty, string.Empty, 0m, 0m, 0m, 0m, 0, 0, 0, 0);
            }

            var first = rows[0];

            decimal actualsTotal = rows
                .GroupBy(r => r.FiscalYearId)
                .Sum(fyGroup => fyGroup.First().ActualsHours);

            var availableToActuals = rows[0].AvailableToActuals;
            var initialBudget = actualsTotal + availableToActuals;

            var forecastTotal = rows.Sum(r => r.ForecastHours);
            var remaining = initialBudget - (actualsTotal + forecastTotal);

            var fiscalYearCount = rows.Select(r => r.FiscalYearId).Distinct().Count();
            var rankCount = rows.Select(r => r.Rank).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var riskCount = rows.Count(r => string.Equals(r.Status, "Risco", StringComparison.OrdinalIgnoreCase));
            var overrunCount = rows.Count(r => string.Equals(r.Status, "Estouro", StringComparison.OrdinalIgnoreCase));

            return new EngagementForecastSummary(
                first.EngagementId,
                first.EngagementCode,
                first.EngagementName,
                initialBudget,
                actualsTotal,
                forecastTotal,
                remaining,
                fiscalYearCount,
                rankCount,
                riskCount,
                overrunCount);
        }

        private bool CanEditEngagement(EngagementForecastSummary summary) => summary is not null;

        partial void OnSelectedEngagementChanged(EngagementForecastSummary? value)
        {
            EditEngagementCommand.NotifyCanExecuteChanged();
        }

        public void Receive(ForecastOperationRequestMessage message)
        {
            switch (message.Operation)
            {
                case ForecastOperationRequestType.Refresh:
                    _ = RefreshInternalAsync();
                    break;
                case ForecastOperationRequestType.GenerateTemplateRetain:
                    _ = GenerateTemplateRetainAsync();
                    break;
                case ForecastOperationRequestType.ExportPending:
                    _ = ExportPendingAsync();
                    break;
            }
        }

        private sealed record RetainTemplateRow(
            string EngagementCode,
            string EngagementName,
            string FiscalYear,
            string Rank,
            decimal ForecastHours,
            decimal AvailableHours);
    }
}
