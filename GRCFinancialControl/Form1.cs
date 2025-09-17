using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GRCFinancialControl.Configuration;
using GRCFinancialControl.Data;
using GRCFinancialControl.Forms;
using GRCFinancialControl.Persistence;
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

        private readonly LocalAppRepository _repository;
        private ConnectionDefinition? _defaultConnection;
        private bool _defaultConnectionHealthy;

        public Form1()
        {
            InitializeComponent();
            _repository = new LocalAppRepository();
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

            builder.AppendLine($"Dry Run Mode: {(chkDryRun.Checked ? "Enabled" : "Disabled")}");

            var readmeText = LoadReadmeText();
            using var form = new HelpForm(builder.ToString(), readmeText);
            form.ShowDialog(this);
        }

        private void SetDataMenusEnabled(bool enabled)
        {
            uploadsToolStripMenuItem.Enabled = enabled;
            reportsToolStripMenuItem.Enabled = enabled;
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
            var filePath = PromptForFile(PlanFileName);
            if (filePath == null)
            {
                return;
            }

            if (!TryBuildConfig(out var config))
            {
                return;
            }

            var engagementId = GetEngagementId();
            if (engagementId == null)
            {
                return;
            }

            AppendStatus($"Loading plan from '{filePath}'.");

            ExcelParseResult<PlanRow> parseResult;
            try
            {
                parseResult = new PlanExcelParser().Parse(filePath);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to parse plan file: {ex.Message}");
                return;
            }

            ReportParseResult(parseResult);
            if (parseResult.Rows.Count == 0)
            {
                AppendStatus("No plan rows parsed; skipping load.");
                return;
            }

            ExecuteWithContext(config, orchestrator =>
            {
                var tuples = parseResult.Rows.Select(r => (r.RawLevel, r.PlannedHours, r.PlannedRate));
                orchestrator.LoadPlan(engagementId, tuples);
            });
        }

        private void LoadEtc()
        {
            var filePath = PromptForFile(EtcFileName);
            if (filePath == null)
            {
                return;
            }

            if (!TryBuildConfig(out var config))
            {
                return;
            }

            var engagementId = GetEngagementId();
            if (engagementId == null)
            {
                return;
            }

            var snapshotLabel = GetSnapshotLabel();
            if (snapshotLabel == null)
            {
                return;
            }

            AppendStatus($"Loading ETC snapshot from '{filePath}'.");

            EtcParseResult parseResult;
            try
            {
                parseResult = new EtcExcelParser().Parse(filePath);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to parse ETC file: {ex.Message}");
                return;
            }

            ReportParseResult(parseResult);
            if (parseResult.ProjectedMarginPct.HasValue)
            {
                var marginText = parseResult.ProjectedMarginPct.Value.ToString("P2", CultureInfo.InvariantCulture);
                AppendStatus($"Projected margin detected: {marginText}.");
            }

            if (parseResult.Rows.Count == 0)
            {
                AppendStatus("No ETC rows parsed; skipping load.");
                return;
            }

            ExecuteWithContext(config, orchestrator =>
            {
                var tuples = parseResult.Rows.Select(r => (r.EmployeeName, r.RawLevel, r.HoursIncurred, r.EtcRemaining));
                orchestrator.LoadEtc(snapshotLabel, engagementId, tuples, parseResult.ProjectedMarginPct);
            });
        }

        private void LoadWeeklyDeclarations(bool isErp)
        {
            var expectedFile = isErp ? ErpFileName : RetainFileName;
            var filePath = PromptForFile(expectedFile);
            if (filePath == null)
            {
                return;
            }

            if (!TryBuildConfig(out var config))
            {
                return;
            }

            var engagementId = GetEngagementId();
            if (engagementId == null)
            {
                return;
            }

            AppendStatus($"Loading {(isErp ? "ERP" : "Retain")} weekly declarations from '{filePath}'.");

            ExcelParseResult<WeeklyDeclarationRow> parseResult;
            try
            {
                parseResult = new WeeklyDeclarationExcelParser().Parse(filePath);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to parse {(isErp ? "ERP" : "Retain")} file: {ex.Message}");
                return;
            }

            ReportParseResult(parseResult);
            if (parseResult.Rows.Count == 0)
            {
                AppendStatus("No weekly declaration rows parsed; skipping load.");
                return;
            }

            ExecuteWithContext(config, orchestrator =>
            {
                var tuples = parseResult.Rows.Select(r => (r.WeekStart, r.EmployeeName, r.DeclaredHours));
                if (isErp)
                {
                    orchestrator.UpsertErp(engagementId, tuples);
                }
                else
                {
                    orchestrator.UpsertRetain(engagementId, tuples);
                }
            });
        }

        private void LoadCharges()
        {
            var filePath = PromptForFile(ChargesFileName);
            if (filePath == null)
            {
                return;
            }

            if (!TryBuildConfig(out var config))
            {
                return;
            }

            var engagementId = GetEngagementId();
            if (engagementId == null)
            {
                return;
            }

            AppendStatus($"Loading charges from '{filePath}'.");

            ExcelParseResult<ChargeRow> parseResult;
            try
            {
                parseResult = new ChargesExcelParser().Parse(filePath);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to parse charges file: {ex.Message}");
                return;
            }

            ReportParseResult(parseResult);
            if (parseResult.Rows.Count == 0)
            {
                AppendStatus("No charge rows parsed; skipping load.");
                return;
            }

            ExecuteWithContext(config, orchestrator =>
            {
                var tuples = parseResult.Rows.Select(r => (r.ChargeDate, r.EmployeeName, r.Hours, r.CostAmount));
                orchestrator.InsertCharges(engagementId, tuples);
            });
        }

        private void Reconcile()
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            var snapshotLabel = GetSnapshotLabel();
            if (snapshotLabel == null)
            {
                return;
            }

            var lastWeekEnd = DateOnly.FromDateTime(dtpWeekEnd.Value.Date);
            AppendStatus($"Reconciling ETC vs charges for '{snapshotLabel}' through {lastWeekEnd:yyyy-MM-dd}.");

            ExecuteWithContext(config, orchestrator =>
            {
                orchestrator.ReconcileEtcVsCharges(snapshotLabel, lastWeekEnd);
            });
        }

        private void ExportAudit()
        {
            if (!TryBuildConfig(out var config))
            {
                return;
            }

            using var context = DbContextFactory.Create(config);
            List<AuditEtcVsCharges> rows;
            try
            {
                rows = context.AuditEtcVsCharges
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
                MessageBox.Show(this, "No audit records available for export.", "Export Audit", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                AppendStatus($"Exported {rows.Count} audit rows to '{dialog.FileName}'.");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to export audit CSV: {ex.Message}");
            }
        }

        private void WriteAuditCsv(string filePath, IReadOnlyList<AuditEtcVsCharges> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine("snapshot_label,engagement_id,employee_id,last_week_end,etc_hours_incurred,charges_sum_hours,diff_hours");
            foreach (var row in rows)
            {
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

        private void ExecuteWithContext(AppConfig config, Action<IngestionOrchestrator> action)
        {
            using var context = DbContextFactory.Create(config);
            var orchestrator = new IngestionOrchestrator(context)
            {
                DryRun = chkDryRun.Checked
            };

            try
            {
                action(orchestrator);
                AppendSummary(orchestrator.LastResult);
            }
            catch (Exception ex)
            {
                AppendSummary(orchestrator.LastResult);
                ShowError(ex.Message);
            }
        }

        private void AppendSummary(OperationSummary? summary)
        {
            if (summary == null)
            {
                AppendStatus("No summary returned from operation.");
                return;
            }

            AppendStatus(summary.ToString());
            const int maxMessages = 10;
            if (summary.Messages.Count > 0)
            {
                for (var i = 0; i < summary.Messages.Count && i < maxMessages; i++)
                {
                    AppendStatus(summary.Messages[i]);
                }

                if (summary.Messages.Count > maxMessages)
                {
                    AppendStatus($"... {summary.Messages.Count - maxMessages} additional messages omitted ...");
                }
            }
        }

        private void ReportParseResult<TRow>(ExcelParseResult<TRow> parseResult)
        {
            AppendStatus(parseResult.BuildSummary());
            WriteMessages("WARN", parseResult.Warnings);
            WriteMessages("ERROR", parseResult.Errors);
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

        private string? GetEngagementId()
        {
            var engagementId = txtEngagementId.Text?.Trim();
            if (string.IsNullOrWhiteSpace(engagementId))
            {
                ShowError("Engagement ID is required.");
                return null;
            }

            return engagementId;
        }

        private string? GetSnapshotLabel()
        {
            var snapshot = txtSnapshotLabel.Text?.Trim();
            if (string.IsNullOrWhiteSpace(snapshot))
            {
                ShowError("Snapshot label is required.");
                return null;
            }

            return snapshot;
        }

        private string? PromptForFile(string expectedFileName)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = $"Select {expectedFileName}",
                FileName = expectedFileName,
                CheckFileExists = true,
                CheckPathExists = true
            };

            var result = dialog.ShowDialog(this);
            if (result != DialogResult.OK)
            {
                return null;
            }

            var actualName = Path.GetFileName(dialog.FileName);
            if (!string.Equals(actualName, expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, $"Please select the exact file '{expectedFileName}'.", "Incorrect File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            return dialog.FileName;
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
