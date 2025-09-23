using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GRCFinancialControl.Configuration;
using GRCFinancialControl.Data;
using GRCFinancialControl.Forms;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Common;
using GRCFinancialControl.Parsing;
using GRCFinancialControl.Uploads;
using GRCFinancialControl.Services;
using MySql.Data.MySqlClient;


namespace GRCFinancialControl
{
    public partial class Form1 : Form
    {
        private const string PlanFileName = "EY_WEEKLY_PRICING_BUDGETING169202518718608_Excel Export.xlsx";
        private const string EtcFileName = "EY_PERSON_ETC_LAST_TRANSFERRED_v1.xlsx";
        private const string ChargesFileName = "2500829220250916220434.xlsx";
        private const string ErpFileName = "20140812 - Programaçao ERP_v14.xlsx";
        private const string RetainFileName = "Programação Retain Platforms GRC (1).xlsx";
        private const string MarginDataFileName = "MarginData.xlsx";

        private readonly LocalAppRepository _repository;
        private readonly FileDialogService _fileDialogService;
        private readonly ParametersService _parametersService;
        private readonly BindingList<UploadFileSummary> _uploadSummaries = new();
        private ConnectionDefinition? _defaultConnection;
        private bool _defaultConnectionHealthy;

        public Form1()
        {
            InitializeComponent();
            _repository = new LocalAppRepository();
            _fileDialogService = new FileDialogService();
            _parametersService = new ParametersService(() => DbContextFactory.CreateLocalContext(_repository.DatabasePath));
            DataGridViewStyler.ConfigureUploadSummaryGrid(gridUploadSummary);
            gridUploadSummary.DataSource = _uploadSummaries;
            SetDataMenusEnabled(false);
            var today = DateOnly.FromDateTime(DateTime.Today);
            var weekEnd = WeekHelper.ToWeekEnd(today);
            dtpWeekEnd.Value = weekEnd.ToDateTime(TimeOnly.MinValue);
            LoadDefaultConnection(reportStatus: true);
        }

        private void maintenanceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var form = new ConnectionMaintenanceForm(_repository);
            form.ShowDialog(this);
            LoadDefaultConnection(reportStatus: true);
        }

        private void selectDefaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var connections = _repository.GetConnections();
            if (connections.Count == 0)
            {
                MessageBox.Show(this, "No saved connections are available. Please add one first.", "Select Default", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var currentId = _defaultConnection?.Id ?? _repository.GetDefaultConnectionId();
            using var form = new SelectDefaultConnectionForm(connections, currentId);
            if (form.ShowDialog(this) != DialogResult.OK || form.SelectedConnection == null)
            {
                return;
            }

            if (!TestConnection(form.SelectedConnection, logDetails: true))
            {
                MessageBox.Show(this, "Unable to connect to the selected database. The default connection was not updated.", "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _repository.SetDefaultConnectionId(form.SelectedConnection.Id);
            LoadDefaultConnection(reportStatus: true);
        }

        private void measurementPeriodToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            using var form = new MeasurementPeriodForm(config, _parametersService);
            form.ShowDialog(this);
        }

        private void engagementsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            using var form = new EngagementForm(config);
            form.ShowDialog(this);
        }

        private void fiscalYearsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            using var form = new FiscalYearMaintenanceForm(config);
            form.ShowDialog(this);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void uploadPlanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadPlan();
        }

        private void uploadEtcToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadEtc();
        }

        private void uploadMarginDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadMarginData();
        }

        private void uploadErpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadWeeklyDeclarations(isErp: true);
        }

        private void uploadRetainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadWeeklyDeclarations(isErp: false);
        }

        private void uploadChargesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadCharges();
        }

        private void reconcileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Reconcile();
        }

        private void exportAuditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportAudit();
        }

        private void viewHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Application Version: {Application.ProductVersion}");
            if (_defaultConnection != null && _defaultConnectionHealthy)
            {
                builder.AppendLine($"Default Connection: {_defaultConnection.Name}");
                builder.AppendLine($"Server: {_defaultConnection.Server}:{_defaultConnection.Port}");
                builder.AppendLine($"Database: {_defaultConnection.Database}");
                builder.AppendLine($"Username: {_defaultConnection.Username}");
                builder.AppendLine($"SSL Enabled: {_defaultConnection.UseSsl}");
            }
            else
            {
                builder.AppendLine("Default Connection: Not configured or unavailable.");
            }

            var readmeText = LoadReadmeText();
            using var form = new HelpForm(builder.ToString(), readmeText);
            form.ShowDialog(this);
        }

        private void SetDataMenusEnabled(bool enabled)
        {
            uploadsToolStripMenuItem.Enabled = enabled;
            reportsToolStripMenuItem.Enabled = enabled;
            masterDataToolStripMenuItem.Enabled = enabled;
        }

        private void LoadDefaultConnection(bool reportStatus)
        {
            _defaultConnection = null;
            _defaultConnectionHealthy = false;
            SetDataMenusEnabled(false);

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
            SetDataMenusEnabled(true);
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

        private string LoadReadmeText()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            for (var i = 0; i < 5 && !string.IsNullOrEmpty(current); i++)
            {
                var candidate = Path.Combine(current, "README.md");
                if (File.Exists(candidate))
                {
                    try
                    {
                        return File.ReadAllText(candidate);
                    }
                    catch (Exception ex)
                    {
                        return $"Failed to load README.md: {ex.Message}";
                    }
                }

                var parent = Directory.GetParent(current);
                current = parent?.FullName ?? string.Empty;
            }

            return "README.md not found.";
        }

        private void LoadPlan()
        {
            var files = _fileDialogService.GetFiles(new FileDialogRequest
            {
                Owner = this,
                Title = "Select budget files",
                SuggestedFileName = PlanFileName,
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                AllowMultiple = true
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

            AppendStatus($"Active period: {FormatMeasurementPeriod(period)}.");

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
                    ShowError($"Failed to parse plan file '{Path.GetFileName(file)}': {ex.Message}");
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

            DisplayBatchSummary(runner.Run(works));
        }

        private void LoadEtc()
        {
            var files = _fileDialogService.GetFiles(new FileDialogRequest
            {
                Owner = this,
                Title = "Select ETC snapshot files",
                SuggestedFileName = EtcFileName,
                Filter = "Excel or CSV Files (*.xlsx;*.csv)|*.xlsx;*.csv|All Files (*.*)|*.*",
                AllowMultiple = true
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

            AppendStatus($"Active period: {FormatMeasurementPeriod(period)}.");
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
                    ShowError($"Failed to parse ETC file '{Path.GetFileName(file)}': {ex.Message}");
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

            DisplayBatchSummary(runner.Run(works));
        }

        private void LoadMarginData()
        {
            var files = _fileDialogService.GetFiles(new FileDialogRequest
            {
                Owner = this,
                Title = "Select margin data files",
                SuggestedFileName = MarginDataFileName,
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                AllowMultiple = true,
                EnforceExactName = false
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

            AppendStatus($"Active period: {FormatMeasurementPeriod(period)}.");

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
                    ShowError($"Failed to parse margin data file '{Path.GetFileName(file)}': {ex.Message}");
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

            DisplayBatchSummary(runner.Run(works));
        }

        private void LoadWeeklyDeclarations(bool isErp)
        {
            var expectedFile = isErp ? ErpFileName : RetainFileName;
            var files = _fileDialogService.GetFiles(new FileDialogRequest
            {
                Owner = this,
                Title = $"Select {(isErp ? "ERP" : "Retain")} weekly declaration file",
                SuggestedFileName = expectedFile,
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                AllowMultiple = false
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

            AppendStatus($"Active period: {FormatMeasurementPeriod(period)}.");

            var filePath = files[0];
            AppendStatus($"Loading {(isErp ? "ERP" : "Retain")} weekly declarations from '{filePath}'.");

            ExcelParseResult<WeeklyDeclarationRow> parseResult;
            try
            {
                parseResult = new WeeklyDeclarationExcelParser().Parse(filePath);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to parse {(isErp ? "ERP" : "Retain")} file '{Path.GetFileName(filePath)}': {ex.Message}");
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

            DisplayBatchSummary(runner.Run(works));
        }

        private void LoadCharges()
        {
            var files = _fileDialogService.GetFiles(new FileDialogRequest
            {
                Owner = this,
                Title = "Select charges file",
                SuggestedFileName = ChargesFileName,
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                AllowMultiple = false
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

            AppendStatus($"Active period: {FormatMeasurementPeriod(period)}.");

            var filePath = files[0];
            AppendStatus($"Loading charges from '{filePath}'.");

            ExcelParseResult<ChargeRow> parseResult;
            try
            {
                parseResult = new ChargesExcelParser().Parse(filePath);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to parse charges file '{Path.GetFileName(filePath)}': {ex.Message}");
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

            DisplayBatchSummary(runner.Run(works));
        }

        private void Reconcile()
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            var lastWeekEnd = DateOnly.FromDateTime(dtpWeekEnd.Value.Date);
            AppendStatus($"Reconciling ETC vs charges through {lastWeekEnd:yyyy-MM-dd}.");

            if (!TryGetSelectedMeasurementPeriod(config, out var period))
            {
                return;
            }

            AppendStatus($"Active period: {FormatMeasurementPeriod(period)}.");

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
                ShowError(ex.Message);
            }
        }

        private void ExportAudit()
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            if (!TryGetSelectedMeasurementPeriod(config, out var period))
            {
                return;
            }

            AppendStatus($"Exporting audit data for {FormatMeasurementPeriod(period)}.");

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
                ShowError($"Failed to query audit table: {ex.Message}");
                return;
            }

            if (rows.Count == 0)
            {
                MessageBox.Show(this, $"No audit records available for export for {period.Description}.", "Export Audit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"audit_etc_vs_charges_{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                WriteAuditCsv(dialog.FileName, rows);
                AppendStatus($"Exported {rows.Count} audit rows for {FormatMeasurementPeriod(period)} to '{dialog.FileName}'.");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to export audit CSV: {ex.Message}");
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

        private void DisplayBatchSummary(UploadBatchSummary batch)
        {
            _uploadSummaries.Clear();
            foreach (var summary in batch.Files)
            {
                _uploadSummaries.Add(summary);
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

        private bool TryBuildConfig(out AppConfig config)
        {
            if (_defaultConnection == null || !_defaultConnectionHealthy)
            {
                ShowError("Default database connection is not available. Please select a working default connection.");
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
                MessageBox.Show(this, "No measurement period is selected. Please open Measurement Period… and activate one.", "Measurement Period Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!long.TryParse(selectedId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var periodId))
            {
                _parametersService.ClearSelectedMeasurePeriod();
                MessageBox.Show(this, "The stored measurement period selection is invalid. Please activate a measurement period again.", "Measurement Period Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            using var context = DbContextFactory.CreateMySqlContext(config);
            var service = new MeasurementPeriodService(context);
            var fetched = service.LoadPeriod(periodId);
            if (fetched == null)
            {
                _parametersService.ClearSelectedMeasurePeriod();
                MessageBox.Show(this, "The previously selected measurement period no longer exists. Please activate a measurement period again.", "Measurement Period Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            period = fetched;
            return true;
        }

        private static string FormatMeasurementPeriod(MeasurementPeriod period)
        {
            return period.ToDisplayString();
        }

        private void AppendStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            txtStatus.AppendText(line + Environment.NewLine);
            txtStatus.SelectionStart = txtStatus.TextLength;
            txtStatus.ScrollToCaret();
        }

        private void ShowError(string message)
        {
            AppendStatus($"ERROR: {message}");
            MessageBox.Show(this, message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
