using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class TasksViewModel : ViewModelBase
    {
        private readonly FilePickerService _filePickerService;
        private readonly IRetainTemplateGenerator _retainTemplateGenerator;

        [ObservableProperty]
        private string? _statusMessage;

        public TasksViewModel(FilePickerService filePickerService, IRetainTemplateGenerator retainTemplateGenerator)
        {
            _filePickerService = filePickerService;
            _retainTemplateGenerator = retainTemplateGenerator;
            GenerateTasksFileCommand = new AsyncRelayCommand(GenerateTasksFileAsync);
            GenerateRetainTemplateCommand = new AsyncRelayCommand(GenerateRetainTemplateAsync);
            ExportRetainFileCommand = new AsyncRelayCommand(ExportRetainFileAsync);
        }

        public IAsyncRelayCommand GenerateTasksFileCommand { get; }

        public IAsyncRelayCommand GenerateRetainTemplateCommand { get; }

        public IAsyncRelayCommand ExportRetainFileCommand { get; }

        private async Task GenerateTasksFileAsync()
        {
            StatusMessage = null;

            var defaultFileName = $"Tasks_{DateTime.Now:yyyyMMdd}.json";
            var filePath = await _filePickerService.SaveFileAsync(
                defaultFileName,
                title: LocalizationRegistry.Get("Tasks.Dialog.SaveTitle"),
                defaultExtension: ".json",
                allowedPatterns: new[] { "*.json" });
            if (string.IsNullOrWhiteSpace(filePath))
            {
                StatusMessage = LocalizationRegistry.Get("Tasks.Status.Cancelled");
                return;
            }

            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
                var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
                var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilMonday == 0)
                {
                    daysUntilMonday = 7;
                }

                var scheduledLocalDate = now.Date.AddDays(daysUntilMonday).AddHours(10);
                var scheduledAt = new DateTimeOffset(scheduledLocalDate, timeZone.GetUtcOffset(scheduledLocalDate));
                var invoiceEmissionDate = scheduledLocalDate.Date.AddDays(7);
                var etcDueDate = invoiceEmissionDate.AddDays(1);

                var payload = new
                {
                    version = "1.0",
                    meta = new
                    {
                        scheduledAt = scheduledAt.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                        timezone = "America/Sao_Paulo",
                        locale = "pt-BR"
                    },
                    messages = new[]
                    {
                        new
                        {
                            id = "1",
                            to = new[]
                            {
                                new { name = string.Empty, email = string.Empty },
                                new { name = string.Empty, email = string.Empty }
                            },
                            cc = new[]
                            {
                                new { name = string.Empty, email = string.Empty }
                            },
                            subject = "Suas tarefas Administrativas para essa semana",
                            bodyTemplate = new
                            {
                                type = "html",
                                value = "Olá,<br/><br/>...<br/>{{InvoicesTable}}<br/>{{EtcsTable}}<br/>"
                            },
                            invoices = new[]
                            {
                                new
                                {
                                    cliente = string.Empty,
                                    cnpj = string.Empty,
                                    codigoProjeto = string.Empty,
                                    dataEmissao = invoiceEmissionDate.ToString("yyyy-MM-dd"),
                                    po = string.Empty,
                                    montante = new { value = 0, currency = "BRL" },
                                    formaPagamento = string.Empty,
                                    textoFatura = string.Empty,
                                    observacoesEmissao = string.Empty
                                }
                            },
                            etcs = new[]
                            {
                                new
                                {
                                    cliente = string.Empty,
                                    cnpj = string.Empty,
                                    codigoProjeto = string.Empty,
                                    concluirAte = etcDueDate.ToString("yyyy-MM-dd")
                                }
                            },
                            attachments = new[]
                            {
                                new
                                {
                                    fileName = "ETCs_Sugeridos.xlsx",
                                    source = "SharePoint",
                                    path = "/sites/Finance/Docs/ETCs_Sugeridos.xlsx",
                                    contentId = "etcs-sheet"
                                }
                            }
                        }
                    }
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                await using var stream = File.Create(filePath);
                await JsonSerializer.SerializeAsync(stream, payload, options);

                StatusMessage = LocalizationRegistry.Format("Tasks.Status.FileSaved", Path.GetFileName(filePath));
            }
            catch (TimeZoneNotFoundException)
            {
                StatusMessage = LocalizationRegistry.Format("Tasks.Status.TimeZoneMissing", "America/Sao_Paulo");
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationRegistry.Format("Tasks.Status.GenerationFailure", ex.Message);
            }
        }

        private async Task GenerateRetainTemplateAsync()
        {
            StatusMessage = null;

            var filePath = await _filePickerService.OpenFileAsync(
                title: LocalizationRegistry.Get("Tasks.Dialog.GenerateRetainTemplateTitle"),
                defaultExtension: ".xlsx",
                allowedPatterns: new[] { "*.xlsx" });

            if (string.IsNullOrWhiteSpace(filePath))
            {
                StatusMessage = LocalizationRegistry.Get("Tasks.Status.RetainTemplateCancelled");
                return;
            }

            try
            {
                var generatedFilePath = await _retainTemplateGenerator.GenerateRetainTemplateAsync(filePath);
                StatusMessage = LocalizationRegistry.Format("Tasks.Status.RetainTemplateSuccess", generatedFilePath);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationRegistry.Format("Tasks.Status.RetainTemplateFailure", ex.Message);
            }
        }

        private async Task ExportRetainFileAsync()
        {
            StatusMessage = null;

            var defaultFileName = $"Retain_{DateTime.Now:yyyyMMdd}.xlsx";
            var filePath = await _filePickerService.SaveFileAsync(
                defaultFileName,
                title: LocalizationRegistry.Get("Tasks.Dialog.ExportRetainTitle"),
                defaultExtension: ".xlsx",
                allowedPatterns: new[] { "*.xlsx" });

            if (string.IsNullOrWhiteSpace(filePath))
            {
                StatusMessage = LocalizationRegistry.Get("Tasks.Status.RetainCancelled");
                return;
            }

            try
            {
                var headers = new[]
                {
                    LocalizationRegistry.Get("Tasks.Retain.Header.SubServiceLine"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.Specialty"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.EmpComments2"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.EmpComments3"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.WorkLocation"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.CostCenter"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.ResourceGpn"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.ResourceName"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.Grade"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.Office"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.Engagement"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.Customer"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.EngagementNumber"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.EmployeeStatus"),
                    LocalizationRegistry.Get("Tasks.Retain.Header.EngagementType")
                };

                using var workbook = new XLWorkbook();
                var worksheet = workbook.AddWorksheet(LocalizationRegistry.Get("Tasks.Worksheet.Retain"));

                for (var columnIndex = 0; columnIndex < headers.Length; columnIndex++)
                {
                    worksheet.Cell(1, columnIndex + 1).Value = headers[columnIndex];
                }

                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                worksheet.SheetView.FreezeRows(1);
                worksheet.Columns().AdjustToContents();

                await Task.Run(() => workbook.SaveAs(filePath));

                StatusMessage = LocalizationRegistry.Format("Tasks.Status.RetainSaved", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationRegistry.Format("Tasks.Status.RetainFailure", ex.Message);
            }
        }
    }
}
