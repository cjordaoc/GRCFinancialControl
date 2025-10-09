using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Data;
using GRCFinancialControl.Core.Parsing;
using GRCFinancialControl.Core.Services;
using GRCFinancialControl.Core.Uploads;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Avalonia.Views;
using MySql.Data.MySqlClient;
using ReactiveUI;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly LocalAppRepository _repository;
        private readonly IFileDialogService _fileDialogService;
        private readonly ParametersService _parametersService;
        private ConnectionDefinition? _defaultConnection;
        private bool _defaultConnectionHealthy;
        private string _statusText = string.Empty;
        private DateTime _reconciliationDate = DateTime.Today;

        public ObservableCollection<UploadFileSummary> UploadSummaries { get; } = new();

        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        public DateTime ReconciliationDate
        {
            get => _reconciliationDate;
            set => this.RaiseAndSetIfChanged(ref _reconciliationDate, value);
        }

        public ReactiveCommand<Unit, Unit> UploadPlanCommand { get; }
        public ReactiveCommand<Unit, Unit> UploadEtcCommand { get; }
        public ReactiveCommand<Unit, Unit> UploadMarginDataCommand { get; }
        public ReactiveCommand<Unit, Unit> UploadErpCommand { get; }
        public ReactiveCommand<Unit, Unit> UploadRetainCommand { get; }
        public ReactiveCommand<Unit, Unit> UploadChargesCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenMeasurementPeriodCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenEngagementsCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenFiscalYearMaintenanceCommand { get; }
        public ReactiveCommand<Unit, Unit> ReconcileCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportAuditCommand { get; }

        public MainWindowViewModel(IFileDialogService fileDialogService)
        {
            _repository = new LocalAppRepository();
            _fileDialogService = fileDialogService;
            _parametersService = new ParametersService(() => DbContextFactory.CreateLocalContext(_repository.DatabasePath));

            UploadPlanCommand = ReactiveCommand.CreateFromTask(UploadPlan);
            UploadEtcCommand = ReactiveCommand.CreateFromTask(UploadEtc);
            UploadMarginDataCommand = ReactiveCommand.CreateFromTask(UploadMarginData);
            UploadErpCommand = ReactiveCommand.CreateFromTask(() => UploadWeeklyDeclarations(true));
            UploadRetainCommand = ReactiveCommand.CreateFromTask(() => UploadWeeklyDeclarations(false));
            UploadChargesCommand = ReactiveCommand.CreateFromTask(UploadCharges);
            OpenMeasurementPeriodCommand = ReactiveCommand.Create(OpenMeasurementPeriod);
            OpenEngagementsCommand = ReactiveCommand.Create(OpenEngagements);
            OpenFiscalYearMaintenanceCommand = ReactiveCommand.Create(OpenFiscalYearMaintenance);
            ReconcileCommand = ReactiveCommand.Create(Reconcile);
            ExportAuditCommand = ReactiveCommand.CreateFromTask(ExportAudit);

            LoadDefaultConnection(reportStatus: true);
        }

        private void OpenMeasurementPeriod()
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            var context = DbContextFactory.CreateMySqlContext(config);
            var service = new MeasurementPeriodService(context);
            var viewModel = new MeasurementPeriodViewModel(service, _parametersService);
            var window = new MeasurementPeriodWindow
            {
                DataContext = viewModel
            };
            window.Show();
        }

        private void OpenEngagements()
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            var viewModel = new EngagementViewModel(config);
            var window = new EngagementWindow
            {
                DataContext = viewModel
            };
            window.Show();
        }

        private void OpenFiscalYearMaintenance()
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            var viewModel = new FiscalYearMaintenanceViewModel(config);
            var window = new FiscalYearMaintenanceWindow
            {
                DataContext = viewModel
            };
            window.Show();
        }

        private async Task UploadPlan()
        {
            var files = await _fileDialogService.GetFiles(new FileDialogRequest
            {
                Title = "Select budget files",
                AllowMultiple = true,
                Filters = new[]
                {
                    new FileFilter { Name = "Excel Files", Extensions = new[] { "xlsx" } },
                    new FileFilter { Name = "All Files", Extensions = new[] { "*" } }
                }
            });

            if (files.Count == 0)
            {
                return;
            }

            if (!TryBuildConfig(out var config))
            {
                return;
            }

            if (!TryGetSelectedMeasurementPeriod(config, out var period))
            {
                return;
            }

            AppendStatus($"Active period: {period.ToDisplayString()}.");

            var runner = CreateUploadRunner(config);
            var works = new List<UploadFileWork>();

            foreach (var file in files)
            {
                AppendStatus($"Parsing plan file '{file}'.");
                ExcelParseResult<PlanRow> parseResult;
                try
                {
                    parseResult = new PlanExcelParser().Parse(file);
                }
                catch (Exception ex)
                {
                    AppendStatus($"ERROR: Failed to parse plan file '{Path.GetFileName(file)}': {ex.Message}");
                    continue;
                }

                ReportParseResult(parseResult, file);
                works.Add(new UploadFileWork(
                    file,
                    parseResult.Rows.Count,
                    parseResult.Warnings,
                    parseResult.Errors,
                    ctx =>
                    {
                        var service = new PlanUploadService(ctx);
                        return service.Load(period.PeriodId, parseResult.Rows);
                    }));
            }

            if (works.Count == 0)
            {
                AppendStatus("No plan files were queued for upload.");
                return;
            }

            var batch = runner.Run(works);
            DisplayBatchSummary(batch);
        }

        private async Task UploadEtc()
        {
            var files = await _fileDialogService.GetFiles(new FileDialogRequest
            {
                Title = "Select ETC snapshot files",
                AllowMultiple = true,
                Filters = new[]
                {
                    new FileFilter { Name = "Excel or CSV Files", Extensions = new[] { "xlsx", "csv" } },
                    new FileFilter { Name = "All Files", Extensions = new[] { "*" } }
                }
            });

            if (files.Count == 0)
            {
                return;
            }

            if (!TryBuildConfig(out var config))
            {
                return;
            }

            if (!TryGetSelectedMeasurementPeriod(config, out var period))
            {
                return;
            }

            AppendStatus($"Active period: {period.ToDisplayString()}.");
            var snapshotLabel = period.Description.Trim();

            var runner = CreateUploadRunner(config);
            var works = new List<UploadFileWork>();

            foreach (var file in files)
            {
                AppendStatus($"Parsing ETC file '{file}'.");
                EtcParseResult parseResult;
                try
                {
                    parseResult = new EtcExcelParser().Parse(file);
                }
                catch (Exception ex)
                {
                    AppendStatus($"ERROR: Failed to parse ETC file '{Path.GetFileName(file)}': {ex.Message}");
                    continue;
                }

                ReportParseResult(parseResult, file);
                works.Add(new UploadFileWork(
                    file,
                    parseResult.Rows.Count,
                    parseResult.Warnings,
                    parseResult.Errors,
                    ctx =>
                    {
                        var service = new EtcUploadService(ctx);
                        return service.Load(period.PeriodId, snapshotLabel, parseResult.Rows);
                    }));
            }

            if (works.Count == 0)
            {
                AppendStatus("No ETC files were queued for upload.");
                return;
            }

            var batch = runner.Run(works);
            DisplayBatchSummary(batch);
        }

        private async Task UploadMarginData()
        {
            var files = await _fileDialogService.GetFiles(new FileDialogRequest
            {
                Title = "Select margin data files",
                AllowMultiple = true,
                Filters = new[]
                {
                    new FileFilter { Name = "Excel Files", Extensions = new[] { "xlsx" } },
                    new FileFilter { Name = "All Files", Extensions = new[] { "*" } }
                }
            });

            if (files.Count == 0)
            {
                return;
            }

            if (!TryBuildConfig(out var config))
            {
                return;
            }

            if (!TryGetSelectedMeasurementPeriod(config, out var period))
            {
                return;
            }

            AppendStatus($"Active period: {period.ToDisplayString()}.");

            var runner = CreateUploadRunner(config);
            var works = new List<UploadFileWork>();

            foreach (var file in files)
            {
                AppendStatus($"Parsing margin data file '{file}'.");
                MarginDataParseResult parseResult;
                try
                {
                    parseResult = new MarginDataExcelParser().Parse(file);
                }
                catch (Exception ex)
                {
                    AppendStatus($"ERROR: Failed to parse margin data file '{Path.GetFileName(file)}': {ex.Message}");
                    continue;
                }

                ReportParseResult(parseResult, file);
                works.Add(new UploadFileWork(
                    file,
                    parseResult.Rows.Count,
                    parseResult.Warnings,
                    parseResult.Errors,
                    ctx =>
                    {
                        var service = new MarginDataUploadService(ctx);
                        return service.Load(period.PeriodId, parseResult.Rows);
                    }));
            }

            if (works.Count == 0)
            {
                AppendStatus("No margin data files were queued for upload.");
                return;
            }

            var batch = runner.Run(works);
            DisplayBatchSummary(batch);
        }

        private async Task UploadWeeklyDeclarations(bool isErp)
        {
            var files = await _fileDialogService.GetFiles(new FileDialogRequest
            {
                Title = $"Select {(isErp ? "ERP" : "Retain")} weekly declaration file",
                AllowMultiple = false,
                Filters = new[]
                {
                    new FileFilter { Name = "Excel Files", Extensions = new[] { "xlsx" } },
                    new FileFilter { Name = "All Files", Extensions = new[] { "*" } }
                }
            });

            if (files.Count == 0)
            {
                return;
            }

            if (!TryBuildConfig(out var config))
            {
                return;
            }

            if (!TryGetSelectedMeasurementPeriod(config, out var period))
            {
                return;
            }

            AppendStatus($"Active period: {period.ToDisplayString()}.");

            var filePath = files[0];
            AppendStatus($"Loading {(isErp ? "ERP" : "Retain")} weekly declarations from '{filePath}'.");

            ExcelParseResult<WeeklyDeclarationRow> parseResult;
            try
            {
                parseResult = new WeeklyDeclarationExcelParser().Parse(filePath);
            }
            catch (Exception ex)
            {
                AppendStatus($"ERROR: Failed to parse {(isErp ? "ERP" : "Retain")} file '{Path.GetFileName(filePath)}': {ex.Message}");
                return;
            }

            ReportParseResult(parseResult, filePath);

            var runner = CreateUploadRunner(config);
            var works = new List<UploadFileWork>
            {
                new UploadFileWork(
                    filePath,
                    parseResult.Rows.Count,
                    parseResult.Warnings,
                    parseResult.Errors,
                    ctx =>
                    {
                        var service = new WeeklyDeclarationUploadService(ctx, isErp);
                        return service.Upsert(period.PeriodId, parseResult.Rows);
                    })
            };

            var batch = runner.Run(works);
            DisplayBatchSummary(batch);
        }

        private async Task UploadCharges()
        {
            var files = await _fileDialogService.GetFiles(new FileDialogRequest
            {
                Title = "Select charges file",
                AllowMultiple = false,
                Filters = new[]
                {
                    new FileFilter { Name = "Excel Files", Extensions = new[] { "xlsx" } },
                    new FileFilter { Name = "All Files", Extensions = new[] { "*" } }
                }
            });

            if (files.Count == 0)
            {
                return;
            }

            if (!TryBuildConfig(out var config))
            {
                return;
            }

            if (!TryGetSelectedMeasurementPeriod(config, out var period))
            {
                return;
            }

            AppendStatus($"Active period: {period.ToDisplayString()}.");

            var filePath = files[0];
            AppendStatus($"Loading charges from '{filePath}'.");

            ExcelParseResult<ChargeRow> parseResult;
            try
            {
                parseResult = new ChargesExcelParser().Parse(filePath);
            }
            catch (Exception ex)
            {
                AppendStatus($"ERROR: Failed to parse charges file '{Path.GetFileName(filePath)}': {ex.Message}");
                return;
            }

            ReportParseResult(parseResult, filePath);

            var runner = CreateUploadRunner(config);
            var works = new List<UploadFileWork>
            {
                new UploadFileWork(
                    filePath,
                    parseResult.Rows.Count,
                    parseResult.Warnings,
                    parseResult.Errors,
                    ctx =>
                    {
                        var service = new ChargesUploadService(ctx);
                        return service.Insert(period.PeriodId, parseResult.Rows);
                    })
            };

            var batch = runner.Run(works);
            DisplayBatchSummary(batch);
        }

        private void Reconcile()
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            var lastWeekEnd = DateOnly.FromDateTime(ReconciliationDate);
            AppendStatus($"Reconciling ETC vs charges through {lastWeekEnd:yyyy-MM-dd}.");

            if (!TryGetSelectedMeasurementPeriod(config, out var period))
            {
                return;
            }

            AppendStatus($"Active period: {period.ToDisplayString()}.");

            try
            {
                using var context = DbContextFactory.CreateMySqlContext(config);
                var service = new ReconciliationService(context);
                var summary = service.Reconcile(period.PeriodId, period.Description.Trim(), lastWeekEnd);
                AppendStatus(summary.ToString());
                WriteMessages("INFO", summary.InfoMessages);
                WriteMessages("WARN", summary.WarningMessages);
                WriteMessages("ERROR", summary.ErrorMessages);
            }
            catch (Exception ex)
            {
                AppendStatus($"ERROR: {ex.Message}");
            }
        }

        private async Task ExportAudit()
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            if (!TryGetSelectedMeasurementPeriod(config, out var period))
            {
                return;
            }

            AppendStatus($"Exporting audit data for {period.ToDisplayString()}.");

            List<AuditEtcVsCharges> rows;
            try
            {
                using var context = DbContextFactory.CreateMySqlContext(config);
                rows = context.AuditEtcVsCharges
                    .Where(a => a.MeasurementPeriodId == period.PeriodId)
                    .OrderBy(a => a.SnapshotLabel)
                    .ThenBy(a => a.EngagementId)
                    .ThenBy(a => a.EmployeeId)
                    .ToList();
            }
            catch (Exception ex)
            {
                AppendStatus($"ERROR: Failed to query audit table: {ex.Message}");
                return;
            }

            if (rows.Count == 0)
            {
                AppendStatus($"No audit records available for export for {period.Description}.");
                return;
            }

            var filePath = await _fileDialogService.SaveFile(new SaveFileDialogRequest
            {
                Title = "Save Audit Export",
                SuggestedFileName = $"audit_etc_vs_charges_{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
                Filters = new[]
                {
                    new FileFilter { Name = "CSV Files", Extensions = new[] { "csv" } }
                }
            });

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                WriteAuditCsv(filePath, rows);
                AppendStatus($"Exported {rows.Count} audit rows for {period.ToDisplayString()} to '{filePath}'.");
            }
            catch (Exception ex)
            {
                AppendStatus($"ERROR: Failed to export audit CSV: {ex.Message}");
            }
        }

        private void WriteAuditCsv(string filePath, IReadOnlyList<AuditEtcVsCharges> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine("measurement_period_id,snapshot_label,engagement_id,employee_id,last_week_end,etc_hours_incurred,charges_sum_hours,diff_hours");
            foreach (var row in rows)
            {
                builder.Append(row.MeasurementPeriodId.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(EscapeCsv(row.SnapshotLabel));
                builder.Append(',');
                builder.Append(EscapeCsv(row.EngagementId));
                builder.Append(',');
                builder.Append(row.EmployeeId.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(row.LastWeekEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(row.EtcHoursIncurred.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(row.ChargesSumHours.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(row.DiffHours.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine();
            }

            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
        {
            if (value.IndexOfAny(new[] { '"', ',', '\\', '\n', '\r' }) >= 0)
            {
                return '"' + value.Replace("\"", "\"\"") + '"';
            }

            return value;
        }

        private UploadRunner CreateUploadRunner(AppConfig config)
        {
            return new UploadRunner(() => DbContextFactory.CreateMySqlContext(config), new StatusUploadLogger(AppendStatus));
        }

        private void DisplayBatchSummary(UploadBatchSummary? batch)
        {
            UploadSummaries.Clear();
            if (batch == null)
            {
                return;
            }

            foreach (var summary in batch.Files)
            {
                UploadSummaries.Add(summary);
            }
        }

        private void ReportParseResult<TRow>(ExcelParseResult<TRow> parseResult, string? filePath = null)
        {
            var fileTag = string.IsNullOrWhiteSpace(filePath) ? string.Empty : $"[{Path.GetFileName(filePath)}] ";
            AppendStatus(fileTag + parseResult.BuildSummary());
            WriteMessages($"{fileTag}WARN", parseResult.Warnings);
            WriteMessages($"{fileTag}ERROR", parseResult.Errors);
        }

        private void WriteMessages(string prefix, IReadOnlyList<string> messages)
        {
            const int max = 10;
            for (var i = 0; i < messages.Count && i < max; i++)
            {
                AppendStatus($"{prefix}: {messages[i]}");
            }

            if (messages.Count > max)
            {
                AppendStatus($"{prefix}: {messages.Count - max} additional messages omitted ...");
            }
        }

        private void LoadDefaultConnection(bool reportStatus)
        {
            _defaultConnection = null;
            _defaultConnectionHealthy = false;

            var defaultId = _repository.GetDefaultConnectionId();
            if (!defaultId.HasValue)
            {
                if (reportStatus)
                {
                    AppendStatus("No default database connection selected.");
                }
                return;
            }

            var definition = _repository.GetConnection(defaultId.Value);
            if (definition == null)
            {
                _repository.SetDefaultConnectionId(null);
                AppendStatus("The saved default connection could not be found. Please select a new default connection.");
                return;
            }

            if (!TestConnection(definition, logDetails: reportStatus))
            {
                AppendStatus($"Default connection '{definition.Name}' failed connectivity. Uploads remain disabled.");
                return;
            }

            _defaultConnection = definition;
            _defaultConnectionHealthy = true;
            if (reportStatus)
            {
                AppendStatus($"Default connection '{definition.Name}' is ready.");
            }
        }

        private bool TestConnection(ConnectionDefinition definition, bool logDetails)
        {
            try
            {
                var config = definition.ToAppConfig();
                using var connection = new MySqlConnection(config.BuildConnectionString());
                connection.Open();
                connection.Close();
                return true;
            }
            catch (Exception ex)
            {
                if (logDetails)
                {
                    AppendStatus($"Connection test failed for '{definition.Name}': {ex.Message}");
                }
                return false;
            }
        }

        private bool TryBuildConfig(out AppConfig config)
        {
            if (_defaultConnection == null || !_defaultConnectionHealthy)
            {
                AppendStatus("ERROR: Default database connection is not available. Please select a working default connection.");
                config = null!;
                return false;
            }

            config = _defaultConnection.ToAppConfig();
            return true;
        }

        private bool TryGetSelectedMeasurementPeriod(AppConfig config, out MeasurementPeriod period)
        {
            period = null!;
            var selectedId = _parametersService.GetSelectedMeasurePeriodId();
            if (string.IsNullOrWhiteSpace(selectedId))
            {
                AppendStatus("ERROR: No measurement period is selected. Please open Measurement Period… and activate one.");
                return false;
            }

            if (!long.TryParse(selectedId, out var periodId))
            {
                _parametersService.ClearSelectedMeasurePeriod();
                AppendStatus("ERROR: The stored measurement period selection is invalid. Please activate a measurement period again.");
                return false;
            }

            using var context = DbContextFactory.CreateMySqlContext(config);
            var service = new MeasurementPeriodService(context);
            var fetched = service.LoadPeriod(periodId);
            if (fetched == null)
            {
                _parametersService.ClearSelectedMeasurePeriod();
                AppendStatus("ERROR: The previously selected measurement period no longer exists. Please activate a measurement period again.");
                return false;
            }

            period = fetched;
            return true;
        }

        private void AppendStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var builder = new StringBuilder(StatusText);
            builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
            StatusText = builder.ToString();
        }

        private sealed class StatusUploadLogger : IUploadLogger
        {
            private readonly Action<string> _writer;

            public StatusUploadLogger(Action<string> writer)
            {
                _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            }

            public void LogStart(UploadFileSummary summary)
            {
                _writer($"[{summary.FileName}] processing started.");
            }

            public void LogSuccess(UploadFileSummary summary)
            {
                _writer($"[{summary.FileName}] {summary.Details}");
            }

            public void LogSkipped(UploadFileSummary summary)
            {
                var message = summary.Infos.FirstOrDefault() ?? "No rows to process.";
                _writer($"[{summary.FileName}] skipped: {message}");
            }

            public void LogFailure(UploadFileSummary summary, Exception exception)
            {
                _writer($"[{summary.FileName}] failed: {exception.Message}");
            }
        }
    }
}