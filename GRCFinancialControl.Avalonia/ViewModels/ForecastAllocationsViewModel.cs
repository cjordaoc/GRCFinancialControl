using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GRCFinancialControl.Avalonia.Services.Interfaces;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public sealed partial class ForecastAllocationsViewModel : ViewModelBase
    {
        private readonly IStaffAllocationForecastService _forecastService;
        private readonly IExportService _exportService;
        private readonly IFilePickerService _filePickerService;
        private readonly ILoggingService _loggingService;

        [ObservableProperty]
        private ObservableCollection<ForecastAllocationRow> _rows = new();

        [ObservableProperty]
        private bool _isBusy;

        public ForecastAllocationsViewModel(
            IStaffAllocationForecastService forecastService,
            IExportService exportService,
            IFilePickerService filePickerService,
            ILoggingService loggingService,
            IMessenger messenger)
            : base(messenger)
        {
            _forecastService = forecastService ?? throw new ArgumentNullException(nameof(forecastService));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
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

        [RelayCommand]
        private async Task GeneratePowerAutomateJsonAsync()
        {
            if (Rows.Count == 0)
            {
                _loggingService.LogWarning("Não há dados para gerar o JSON do Power Automate.");
                return;
            }

            var payload = Rows
                .Select(row => new PowerAutomateForecastPayload(
                    row.EngagementCode,
                    row.FiscalYearName,
                    row.Rank,
                    row.ForecastHours,
                    row.ActualsHours,
                    row.AvailableHours,
                    row.AvailableToActuals,
                    row.Status))
                .ToList();

            var defaultFileName = $"ForecastPowerAutomate_{DateTime.Now:yyyyMMdd_HHmm}";
            var targetPath = await _filePickerService.SaveFileAsync(
                defaultFileName,
                title: "Salvar JSON",
                defaultExtension: ".json",
                allowedPatterns: new[] { "*.json" });

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                await File.WriteAllTextAsync(targetPath!, JsonSerializer.Serialize(payload, options));
                _loggingService.LogInfo($"JSON do Power Automate salvo em '{targetPath}'.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Falha ao gerar JSON do Power Automate: {ex.Message}");
            }
        }

        private async Task RefreshInternalAsync()
        {
            if (IsBusy)
            {
                return;
            }

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
                _loggingService.LogInfo($"Forecast carregado com {Rows.Count} linhas.");
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

        private sealed record RetainTemplateRow(
            string EngagementCode,
            string EngagementName,
            string FiscalYear,
            string Rank,
            decimal ForecastHours,
            decimal AvailableHours);

        private sealed record PowerAutomateForecastPayload(
            string Engagement,
            string FiscalYear,
            string Rank,
            decimal ForecastHours,
            decimal ActualsHours,
            decimal AvailableHours,
            decimal AvailableToActuals,
            string Status);
    }
}
