using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Invoices.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class TasksViewModel : ViewModelBase
    {
        private readonly FilePickerService _filePickerService;
        private readonly IRetainTemplateGenerator _retainTemplateGenerator;
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

        [ObservableProperty]
        private string? _statusMessage;

        public TasksViewModel(
            FilePickerService filePickerService,
            IRetainTemplateGenerator retainTemplateGenerator,
            IDbContextFactory<ApplicationDbContext> dbContextFactory)
        {
            _filePickerService = filePickerService;
            _retainTemplateGenerator = retainTemplateGenerator;
            _dbContextFactory = dbContextFactory;
            GenerateTasksFileCommand = new AsyncRelayCommand(GenerateTasksFileAsync);
            GenerateRetainTemplateCommand = new AsyncRelayCommand(GenerateRetainTemplateAsync);
        }

        public IAsyncRelayCommand GenerateTasksFileCommand { get; }

        public IAsyncRelayCommand GenerateRetainTemplateCommand { get; }

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
                var notifyDate = scheduledLocalDate.Date;

                await using var context = await _dbContextFactory.CreateDbContextAsync();

                var invoices = await LoadInvoiceEntriesAsync(context, notifyDate);
                var etcs = await LoadEtcEntriesAsync(context, notifyDate);

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
                            invoices,
                            etcs
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

            var allocationFilePath = await _filePickerService.OpenFileAsync(
                title: LocalizationRegistry.Get("Tasks.Dialog.GenerateRetainTemplateTitle"),
                defaultExtension: ".xlsx",
                allowedPatterns: new[] { "*.xlsx" });

            if (string.IsNullOrWhiteSpace(allocationFilePath))
            {
                StatusMessage = LocalizationRegistry.Get("Tasks.Status.RetainTemplateCancelled");
                return;
            }

            var defaultFileName = $"RetainTemplate_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var destinationFilePath = await _filePickerService.SaveFileAsync(
                defaultFileName,
                title: LocalizationRegistry.Get("Tasks.Dialog.SaveRetainTemplateTitle"),
                defaultExtension: ".xlsx",
                allowedPatterns: new[] { "*.xlsx" });

            if (string.IsNullOrWhiteSpace(destinationFilePath))
            {
                StatusMessage = LocalizationRegistry.Get("Tasks.Status.RetainTemplateCancelled");
                return;
            }

            try
            {
                var generatedFilePath = await _retainTemplateGenerator.GenerateRetainTemplateAsync(
                    allocationFilePath,
                    destinationFilePath);
                StatusMessage = LocalizationRegistry.Format("Tasks.Status.RetainTemplateSuccess", generatedFilePath);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationRegistry.Format("Tasks.Status.RetainTemplateFailure", ex.Message);
            }
        }

        private static IReadOnlyList<string> SplitEmails(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IReadOnlyList<string> BuildRecipientEmails(InvoiceNotificationPreview preview)
        {
            var recipients = new List<string>();

            var focal = preview.CustomerFocalPointEmail?.Trim();
            if (!string.IsNullOrWhiteSpace(focal))
            {
                recipients.Add(focal);
            }

            foreach (var email in SplitEmails(preview.ExtraEmails))
            {
                recipients.Add(email);
            }

            foreach (var email in SplitEmails(preview.ManagerEmails))
            {
                recipients.Add(email);
            }

            return recipients
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IReadOnlyList<object> BuildManagerContacts(InvoiceNotificationPreview preview)
        {
            var emails = SplitEmails(preview.ManagerEmails);
            if (emails.Count == 0)
            {
                return Array.Empty<object>();
            }

            var names = SplitEmails(preview.ManagerNames);
            var contacts = new List<object>(emails.Count);

            for (var index = 0; index < emails.Count; index++)
            {
                var email = emails[index];
                var name = index < names.Count ? names[index] : string.Empty;
                contacts.Add(new { name, email });
            }

            return contacts;
        }

        private static IReadOnlyList<object> BuildRankHours(Engagement engagement)
        {
            if (engagement.RankBudgets.Count == 0)
            {
                return Array.Empty<object>();
            }

            return engagement.RankBudgets
                .Where(budget => budget.FiscalYear != null)
                .OrderBy(budget => budget.FiscalYear!.StartDate)
                .ThenBy(budget => budget.RankName, StringComparer.OrdinalIgnoreCase)
                .Select(budget => new
                {
                    fiscalYear = budget.FiscalYear!.Name,
                    rank = budget.RankName,
                    horasIncurridas = Math.Round(budget.ConsumedHours, 2, MidpointRounding.AwayFromZero),
                    horasRestantes = Math.Round(budget.RemainingHours, 2, MidpointRounding.AwayFromZero)
                })
                .ToList<object>();
        }

        private static IReadOnlyList<Manager> ResolveManagers(Engagement engagement)
        {
            if (engagement.ManagerAssignments.Count == 0)
            {
                return Array.Empty<Manager>();
            }

            return engagement.ManagerAssignments
                .Select(assignment => assignment.Manager)
                .Where(manager => manager != null)
                .GroupBy(manager => manager!.Id)
                .Select(group => group.First())
                .OfType<Manager>()
                .ToArray();
        }

        private async Task<IReadOnlyList<object>> LoadInvoiceEntriesAsync(ApplicationDbContext context, DateTime notifyDate)
        {
            var previews = await context.InvoiceNotificationPreviews
                .AsNoTracking()
                .Where(entry => entry.NotifyDate == notifyDate)
                .OrderBy(entry => entry.EngagementId)
                .ThenBy(entry => entry.SeqNo)
                .ToListAsync()
                .ConfigureAwait(false);

            if (previews.Count == 0)
            {
                return Array.Empty<object>();
            }

            var invoiceItemIds = previews
                .Select(preview => preview.InvoiceItemId)
                .Distinct()
                .ToArray();

            var invoiceItems = await context.InvoiceItems
                .AsNoTracking()
                .Where(item => invoiceItemIds.Contains(item.Id))
                .Select(item => new
                {
                    item.Id,
                    item.PayerCnpj,
                    item.CustomerTicket,
                    item.AdditionalInfo,
                    item.DeliveryDescription
                })
                .ToDictionaryAsync(item => item.Id)
                .ConfigureAwait(false);

            var planIds = previews
                .Select(preview => preview.PlanId)
                .Distinct()
                .ToArray();

            var plans = await context.InvoicePlans
                .AsNoTracking()
                .Where(plan => planIds.Contains(plan.Id))
                .Select(plan => new
                {
                    plan.Id,
                    plan.CustomInstructions
                })
                .ToDictionaryAsync(plan => plan.Id)
                .ConfigureAwait(false);

            var engagementIds = previews
                .Select(preview => preview.EngagementIntId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToArray();

            var engagements = engagementIds.Length == 0
                ? new Dictionary<int, Engagement>()
                : await context.Engagements
                    .AsNoTracking()
                    .Include(engagement => engagement.Customer)
                    .Where(engagement => engagementIds.Contains(engagement.Id))
                    .ToDictionaryAsync(engagement => engagement.Id)
                    .ConfigureAwait(false);

            var invoices = new List<object>(previews.Count);

            foreach (var preview in previews)
            {
                invoiceItems.TryGetValue(preview.InvoiceItemId, out var item);
                plans.TryGetValue(preview.PlanId, out var plan);

                Engagement? engagement = null;
                if (preview.EngagementIntId.HasValue)
                {
                    engagements.TryGetValue(preview.EngagementIntId.Value, out engagement);
                }

                var currency = engagement?.Currency ?? "BRL";
                var amountValue = Math.Round(preview.Amount, 2, MidpointRounding.AwayFromZero);
                var competence = preview.EmissionDate.ToString("MM/yyyy", CultureInfo.InvariantCulture);
                var formaPagamento = preview.PaymentTermDays > 0
                    ? $"{preview.PaymentTermDays} dias"
                    : string.Empty;

                var recipients = BuildRecipientEmails(preview);
                var managers = BuildManagerContacts(preview);

                invoices.Add(new
                {
                    cliente = preview.CustomerName ?? string.Empty,
                    cnpj = item?.PayerCnpj ?? string.Empty,
                    codigoProjeto = preview.EngagementId,
                    descricaoProjeto = engagement?.Description ?? string.Empty,
                    dataEmissao = preview.EmissionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    competencia = competence,
                    po = preview.PoNumber ?? string.Empty,
                    frs = preview.FrsNumber ?? string.Empty,
                    chamado = preview.RitmNumber ?? string.Empty,
                    montante = new { value = amountValue, currency },
                    formaPagamento,
                    textoFatura = plan?.CustomInstructions ?? string.Empty,
                    observacoesEmissao = item?.AdditionalInfo ?? string.Empty,
                    descricaoEntrega = item?.DeliveryDescription ?? string.Empty,
                    ticketCliente = item?.CustomerTicket ?? string.Empty,
                    contatoCliente = new
                    {
                        name = (preview.CustomerFocalPointName ?? string.Empty).Trim(),
                        email = (preview.CustomerFocalPointEmail ?? string.Empty).Trim()
                    },
                    destinatarios = recipients,
                    gestores = managers
                });
            }

            return invoices;
        }

        private async Task<IReadOnlyList<object>> LoadEtcEntriesAsync(ApplicationDbContext context, DateTime thresholdDate)
        {
            var engagements = await context.Engagements
                .AsNoTracking()
                .Include(engagement => engagement.Customer)
                .Include(engagement => engagement.ManagerAssignments)
                    .ThenInclude(assignment => assignment.Manager)
                .Include(engagement => engagement.RankBudgets)
                    .ThenInclude(budget => budget.FiscalYear)
                .Where(engagement =>
                    engagement.Status == EngagementStatus.Active &&
                    engagement.ProposedNextEtcDate.HasValue &&
                    engagement.ProposedNextEtcDate.Value.Date <= thresholdDate)
                .ToListAsync()
                .ConfigureAwait(false);

            if (engagements.Count == 0)
            {
                return Array.Empty<object>();
            }

            var managerEngagements = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);
            var managerInfo = new Dictionary<string, (string Name, string Email)>(StringComparer.OrdinalIgnoreCase);

            foreach (var engagement in engagements)
            {
                var rankHours = BuildRankHours(engagement);

                var engagementEntry = new
                {
                    codigoProjeto = engagement.EngagementId,
                    descricaoProjeto = engagement.Description,
                    cliente = engagement.Customer?.Name ?? string.Empty,
                    ultimoEtc = engagement.LastEtcDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    proximoEtc = engagement.ProposedNextEtcDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    horas = rankHours
                };

                var managers = ResolveManagers(engagement);
                if (managers.Count == 0)
                {
                    const string key = "__sem_gerente__";
                    if (!managerEngagements.TryGetValue(key, out var list))
                    {
                        list = new List<object>();
                        managerEngagements[key] = list;
                        managerInfo[key] = ("Sem gerente", string.Empty);
                    }

                    list.Add(engagementEntry);
                    continue;
                }

                foreach (var manager in managers)
                {
                    var emailKey = string.IsNullOrWhiteSpace(manager.Email)
                        ? $"__id_{manager.Id}"
                        : manager.Email.Trim();

                    if (!managerEngagements.TryGetValue(emailKey, out var list))
                    {
                        list = new List<object>();
                        managerEngagements[emailKey] = list;
                        var name = string.IsNullOrWhiteSpace(manager.Name) ? "Sem nome" : manager.Name.Trim();
                        var email = string.IsNullOrWhiteSpace(manager.Email) ? string.Empty : manager.Email.Trim();
                        managerInfo[emailKey] = (name, email);
                    }

                    list.Add(engagementEntry);
                }
            }

            return managerEngagements
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry =>
                {
                    managerInfo.TryGetValue(entry.Key, out var info);
                    var managerName = string.IsNullOrWhiteSpace(info.Name) ? string.Empty : info.Name;
                    var managerEmail = string.IsNullOrWhiteSpace(info.Email) ? string.Empty : info.Email;

                    return new
                    {
                        manager = new
                        {
                            name = managerName,
                            email = managerEmail
                        },
                        engagements = entry.Value
                    };
                })
                .ToList<object>();
        }

    }
}
