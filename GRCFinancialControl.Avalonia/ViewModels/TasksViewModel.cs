using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Avalonia.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class TasksViewModel : ViewModelBase
    {
        private readonly IFilePickerService _filePickerService;

        [ObservableProperty]
        private string? _statusMessage;

        public TasksViewModel(IFilePickerService filePickerService)
        {
            _filePickerService = filePickerService;
            GenerateTasksFileCommand = new AsyncRelayCommand(GenerateTasksFileAsync);
        }

        public IAsyncRelayCommand GenerateTasksFileCommand { get; }

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
    }
}
