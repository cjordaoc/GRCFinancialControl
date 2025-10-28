
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using App.Presentation.Localization;
using App.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Core.Payments;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public partial class TasksViewModel : ViewModelBase
    {
        private const string TimeZoneId = "America/Sao_Paulo";

        private readonly FilePickerService _filePickerService;
        private readonly IRetainTemplateGenerator _retainTemplateGenerator;
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private string? _statusMessage;

        public TasksViewModel(
            FilePickerService filePickerService,
            IRetainTemplateGenerator retainTemplateGenerator,
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            ISettingsService settingsService)
        {
            _filePickerService = filePickerService;
            _retainTemplateGenerator = retainTemplateGenerator;
            _dbContextFactory = dbContextFactory;
            _settingsService = settingsService;

            GenerateTasksFileCommand = new AsyncRelayCommand(GenerateTasksFileAsync);
            GenerateRetainTemplateCommand = new AsyncRelayCommand(GenerateRetainTemplateAsync);
        }

        public IAsyncRelayCommand GenerateTasksFileCommand { get; }

        public IAsyncRelayCommand GenerateRetainTemplateCommand { get; }

        private async Task GenerateTasksFileAsync()
        {
            StatusMessage = null;

            var defaultFileName = $"WeeklyTasks_{DateTime.Now:yyyyMMdd}.xml";
            var initialDirectory = await GetLastExportDirectoryAsync().ConfigureAwait(false);

            var filePath = await _filePickerService.SaveFileAsync(
                defaultFileName,
                title: LocalizationRegistry.Get("Tasks.Dialog.SaveTitle"),
                defaultExtension: ".xml",
                allowedPatterns: new[] { "*.xml" },
                initialDirectory: initialDirectory).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                StatusMessage = LocalizationRegistry.Get("Tasks.Status.Cancelled");
                return;
            }

            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
                var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
                var baseDate = now.Date;
                var daysUntilMonday = ((int)DayOfWeek.Monday - (int)baseDate.DayOfWeek + 7) % 7;
                if (daysUntilMonday == 0)
                {
                    daysUntilMonday = 7;
                }

                var scheduledLocalDate = baseDate.AddDays(daysUntilMonday).AddHours(10);
                var notifyDate = scheduledLocalDate.Date;

                await using var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

                var invoiceWindowStartDate = baseDate;
                var invoiceWindowEndDate = baseDate.AddDays(11);

                var invoices = await LoadInvoiceEntriesAsync(context, invoiceWindowStartDate, invoiceWindowEndDate).ConfigureAwait(false);
                var etcs = await LoadEtcEntriesAsync(context, notifyDate).ConfigureAwait(false);

                var messageBuckets = BuildMessageBuckets(invoices, etcs);
                await WriteXmlAsync(filePath, now, notifyDate, messageBuckets).ConfigureAwait(false);
                await PersistExportDirectoryAsync(Path.GetDirectoryName(filePath)).ConfigureAwait(false);

                StatusMessage = LocalizationRegistry.Format("Tasks.Status.FileSaved", Path.GetFileName(filePath));
            }
            catch (TimeZoneNotFoundException)
            {
                StatusMessage = LocalizationRegistry.Format("Tasks.Status.TimeZoneMissing", TimeZoneId);
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationRegistry.Format("Tasks.Status.GenerationFailure", ex.Message);
            }
        }

        private async Task WriteXmlAsync(string filePath, DateTimeOffset generatedAt, DateTime referenceDate, IReadOnlyList<MessageBucket> buckets)
        {
            var settings = new XmlWriterSettings
            {
                Async = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace
            };

            await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await using var writer = XmlWriter.Create(stream, settings);

            await writer.WriteStartDocumentAsync().ConfigureAwait(false);
            await writer.WriteStartElementAsync(null, "WeeklyTasks", null).ConfigureAwait(false);
            writer.WriteAttributeString("version", "1.3");

            await writer.WriteStartElementAsync(null, "Meta", null).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "GeneratedAt", null, generatedAt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "Timezone", null, TimeZoneId).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "Locale", null, "pt-BR").ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);

            await writer.WriteStartElementAsync(null, "Messages", null).ConfigureAwait(false);

            foreach (var (bucket, index) in buckets.Select((bucket, index) => (bucket, index)))
            {
                await WriteMessageAsync(writer, bucket, index + 1, referenceDate).ConfigureAwait(false);
            }

            await writer.WriteEndElementAsync().ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
            await writer.WriteEndDocumentAsync().ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }

        private static async Task WriteMessageAsync(XmlWriter writer, MessageBucket bucket, int messageIndex, DateTime referenceDate)
        {
            await writer.WriteStartElementAsync(null, "Message", null).ConfigureAwait(false);
            writer.WriteAttributeString("MessageID", messageIndex.ToString(CultureInfo.InvariantCulture));

            await writer.WriteStartElementAsync(null, "Recipients", null).ConfigureAwait(false);
            foreach (var recipient in bucket.GetOrderedRecipients())
            {
                await writer.WriteStartElementAsync(null, "Recipient", null).ConfigureAwait(false);
                writer.WriteAttributeString("role", recipient.DominantRole == ManagerPosition.SeniorManager ? "SeniorManager" : "Manager");
                if (!string.IsNullOrWhiteSpace(recipient.Name))
                {
                    await writer.WriteElementStringAsync(null, "Name", null, recipient.Name).ConfigureAwait(false);
                }

                await writer.WriteElementStringAsync(null, "Email", null, recipient.Email).ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }

            await writer.WriteEndElementAsync().ConfigureAwait(false);

            await writer.WriteStartElementAsync(null, "Body", null).ConfigureAwait(false);
            await writer.WriteStartElementAsync(null, "Text", null).ConfigureAwait(false);
            await writer.WriteCDataAsync(BuildBodyText(bucket, referenceDate)).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);

            if (bucket.Invoices.Count > 0)
            {
                await writer.WriteStartElementAsync(null, "Invoices", null).ConfigureAwait(false);
                foreach (var invoice in bucket.GetOrderedInvoices())
                {
                    await WriteInvoiceAsync(writer, invoice).ConfigureAwait(false);
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }

            await writer.WriteElementStringAsync(null, "HasETCs", null, bucket.Etcs.Count > 0 ? "true" : "false").ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "ETCMessage", null, BuildEtcMessage(bucket)).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);

            if (bucket.Etcs.Count > 0)
            {
                await writer.WriteStartElementAsync(null, "ETCs", null).ConfigureAwait(false);
                foreach (var etc in bucket.GetOrderedEtcs())
                {
                    await WriteEtcAsync(writer, etc).ConfigureAwait(false);
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }

            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        private static async Task WriteInvoiceAsync(XmlWriter writer, InvoiceExport invoice)
        {
            await writer.WriteStartElementAsync(null, "Invoice", null).ConfigureAwait(false);
            writer.WriteAttributeString("InvoiceID", invoice.InvoiceItemId.ToString(CultureInfo.InvariantCulture));

            await writer.WriteElementStringAsync(null, "EngagementID", null, invoice.EngagementId).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "Customer", null, invoice.Customer).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(invoice.FocalPointName))
            {
                await writer.WriteElementStringAsync(null, "FocalPointName", null, invoice.FocalPointName).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(invoice.Cnpj))
            {
                await writer.WriteElementStringAsync(null, "CNPJ", null, invoice.Cnpj).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(invoice.Po))
            {
                await writer.WriteElementStringAsync(null, "PO", null, invoice.Po).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(invoice.Frs))
            {
                await writer.WriteElementStringAsync(null, "FRS", null, invoice.Frs).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(invoice.CustomerTicket))
            {
                await writer.WriteElementStringAsync(null, "CustomerTicket", null, invoice.CustomerTicket).ConfigureAwait(false);
            }

            await writer.WriteStartElementAsync(null, "PaymentType", null).ConfigureAwait(false);
            writer.WriteAttributeString("code", invoice.PaymentTypeCode);
            await writer.WriteStringAsync(invoice.PaymentTypeName).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);

            await writer.WriteElementStringAsync(null, "InvoiceValue", null, invoice.InvoiceValue.ToString("0.00", CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "DateOfEmission", null, invoice.DateOfEmission.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "InvoiceDueDate", null, invoice.InvoiceDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(invoice.EmailsToSend))
            {
                await writer.WriteElementStringAsync(null, "EmailsToSend", null, invoice.EmailsToSend).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(invoice.InvoiceDescription))
            {
                await writer.WriteStartElementAsync(null, "InvoiceDescription", null).ConfigureAwait(false);
                await writer.WriteCDataAsync(invoice.InvoiceDescription).ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(invoice.AdditionalNotes))
            {
                await writer.WriteElementStringAsync(null, "AdditionalNotes", null, invoice.AdditionalNotes).ConfigureAwait(false);
            }

            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        private static async Task WriteEtcAsync(XmlWriter writer, EtcExport etc)
        {
            await writer.WriteStartElementAsync(null, "ETC", null).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "EngagementID", null, etc.EngagementId).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "EngagementName", null, etc.EngagementName).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "Customer", null, etc.Customer).ConfigureAwait(false);

            await writer.WriteStartElementAsync(null, "Details", null).ConfigureAwait(false);

            var columns = BuildEtcColumns(etc);
            await writer.WriteStartElementAsync(null, "Columns", null).ConfigureAwait(false);
            foreach (var column in columns)
            {
                await writer.WriteStartElementAsync(null, "Column", null).ConfigureAwait(false);
                writer.WriteAttributeString("Name", column.Name);
                writer.WriteAttributeString("TableHeader", column.Header);
                if (column.IsDynamic)
                {
                    writer.WriteAttributeString("DynamicHeader", "true");
                }

                if (!string.IsNullOrWhiteSpace(column.FiscalYear))
                {
                    writer.WriteAttributeString("FiscalYear", column.FiscalYear);
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }

            await writer.WriteEndElementAsync().ConfigureAwait(false);

            var rows = BuildEtcRows(etc, columns);
            await writer.WriteStartElementAsync(null, "Rows", null).ConfigureAwait(false);
            foreach (var row in rows)
            {
                await writer.WriteStartElementAsync(null, "Row", null).ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "Rank", null, row.Rank).ConfigureAwait(false);

                foreach (var cell in row.Cells)
                {
                    await writer.WriteStartElementAsync(null, "Cell", null).ConfigureAwait(false);
                    writer.WriteAttributeString("ColumnName", cell.ColumnName);
                    if (!string.IsNullOrWhiteSpace(cell.FiscalYear))
                    {
                        writer.WriteAttributeString("FiscalYear", cell.FiscalYear);
                    }

                    await writer.WriteStringAsync(cell.Value).ConfigureAwait(false);
                    await writer.WriteEndElementAsync().ConfigureAwait(false);
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }

            await writer.WriteEndElementAsync().ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        private async Task<IReadOnlyList<InvoiceExport>> LoadInvoiceEntriesAsync(ApplicationDbContext context, DateTime startDate, DateTime endDate)
        {
            var inclusiveStart = startDate.Date;
            var exclusiveEnd = endDate.Date.AddDays(1);

            var previews = await context.InvoiceNotificationPreviews
                .AsNoTracking()
                .Where(entry => entry.EmissionDate >= inclusiveStart && entry.EmissionDate < exclusiveEnd)
                .OrderBy(entry => entry.EngagementId)
                .ThenBy(entry => entry.SeqNo)
                .ToListAsync()
                .ConfigureAwait(false);

            if (previews.Count == 0)
            {
                return Array.Empty<InvoiceExport>();
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
                    item.PlanId,
                    item.PayerCnpj,
                    item.CustomerTicket,
                    item.AdditionalInfo,
                    item.DeliveryDescription,
                    item.CoeResponsible,
                    item.PaymentTypeCode
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
                    plan.Type,
                    plan.NumInvoices,
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
                    .Include(engagement => engagement.ManagerAssignments)
                        .ThenInclude(assignment => assignment.Manager)
                    .Where(engagement => engagementIds.Contains(engagement.Id))
                    .ToDictionaryAsync(engagement => engagement.Id)
                    .ConfigureAwait(false);

            var invoices = new List<InvoiceExport>(previews.Count);

            foreach (var preview in previews)
            {
                invoiceItems.TryGetValue(preview.InvoiceItemId, out var item);
                plans.TryGetValue(preview.PlanId, out var plan);

                Engagement? engagement = null;
                if (preview.EngagementIntId.HasValue)
                {
                    engagements.TryGetValue(preview.EngagementIntId.Value, out engagement);
                }

                var recipients = BuildRecipients(engagement?.ManagerAssignments);
                if (recipients.Count == 0)
                {
                    recipients = BuildPreviewRecipients(preview);
                }
                if (recipients.Count == 0)
                {
                    continue;
                }

                var customerEmails = BuildCustomerEmails(preview.CustomerFocalPointEmail, preview.ExtraEmails);
                var currency = string.IsNullOrWhiteSpace(engagement?.Currency) ? "BRL" : engagement!.Currency.Trim();
                var amountValue = Math.Round(preview.Amount, 2, MidpointRounding.AwayFromZero);
                var deliveryDescription = item?.DeliveryDescription;
                var customerTicket = item?.CustomerTicket;
                var planType = plan?.Type;

                var description = BuildInvoiceDescription(
                    preview,
                    engagement,
                    deliveryDescription,
                    customerEmails,
                    currency,
                    amountValue,
                    item?.CoeResponsible,
                    planType,
                    customerTicket);

                var paymentType = PaymentTypeCatalog.GetByCode(item?.PaymentTypeCode);
                var emailsCsv = customerEmails.Count == 0 ? null : string.Join("; ", customerEmails);

                invoices.Add(new InvoiceExport
                {
                    InvoiceItemId = preview.InvoiceItemId,
                    EngagementId = preview.EngagementId,
                    Customer = preview.CustomerName ?? string.Empty,
                    FocalPointName = preview.CustomerFocalPointName,
                    FocalPointEmail = preview.CustomerFocalPointEmail,
                    Cnpj = item?.PayerCnpj,
                    Po = preview.PoNumber,
                    Frs = preview.FrsNumber,
                    CustomerTicket = customerTicket,
                    PaymentTypeCode = paymentType.Code,
                    PaymentTypeName = paymentType.DisplayName,
                    InvoiceValue = amountValue,
                    DateOfEmission = preview.EmissionDate,
                    InvoiceDueDate = preview.ComputedDueDate,
                    EmailsToSend = emailsCsv,
                    InvoiceDescription = description,
                    AdditionalNotes = plan?.CustomInstructions ?? item?.AdditionalInfo,
                    Recipients = recipients
                });
            }

            return invoices;
        }

        private async Task<IReadOnlyList<EtcExport>> LoadEtcEntriesAsync(ApplicationDbContext context, DateTime thresholdDate)
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
                return Array.Empty<EtcExport>();
            }

            var exports = new List<EtcExport>(engagements.Count);

            foreach (var engagement in engagements)
            {
                var recipients = BuildRecipients(engagement.ManagerAssignments);
                if (recipients.Count == 0)
                {
                    continue;
                }

                var fiscalYears = BuildEtcFiscalYears(engagement);

                exports.Add(new EtcExport
                {
                    EngagementId = engagement.EngagementId,
                    EngagementName = string.IsNullOrWhiteSpace(engagement.Description)
                        ? engagement.EngagementId
                        : engagement.Description.Trim(),
                    Customer = engagement.Customer?.Name ?? string.Empty,
                    LastEtcDate = engagement.LastEtcDate?.Date,
                    ProposedNextEtcDate = engagement.ProposedNextEtcDate?.Date,
                    Recipients = recipients,
                    FiscalYears = fiscalYears
                });
            }

            return exports;
        }

        private async Task<string?> GetLastExportDirectoryAsync()
        {
            try
            {
                var settings = await _settingsService.GetAllAsync().ConfigureAwait(false);
                if (settings.TryGetValue(SettingKeys.TasksExportDirectory, out var directory)
                    && !string.IsNullOrWhiteSpace(directory)
                    && Directory.Exists(directory))
                {
                    return directory;
                }
            }
            catch
            {
                // Ignore persistence failures and fall back to the default dialog location.
            }

            return null;
        }

        private async Task PersistExportDirectoryAsync(string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            try
            {
                var absoluteDirectory = Path.GetFullPath(directory);
                var settings = await _settingsService.GetAllAsync().ConfigureAwait(false);
                settings[SettingKeys.TasksExportDirectory] = absoluteDirectory;
                await _settingsService.SaveAllAsync(settings).ConfigureAwait(false);
            }
            catch
            {
                // Persisting the directory is a convenience; failures are non-blocking.
            }
        }

        private async Task GenerateRetainTemplateAsync()
        {
            StatusMessage = null;

            var allocationFilePath = await _filePickerService.OpenFileAsync(
                title: LocalizationRegistry.Get("Tasks.Dialog.GenerateRetainTemplateTitle"),
                defaultExtension: ".xlsx",
                allowedPatterns: new[] { "*.xlsx" }).ConfigureAwait(false);

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
                allowedPatterns: new[] { "*.xlsx" }).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(destinationFilePath))
            {
                StatusMessage = LocalizationRegistry.Get("Tasks.Status.RetainTemplateCancelled");
                return;
            }

            try
            {
                var generatedFilePath = await _retainTemplateGenerator.GenerateRetainTemplateAsync(
                    allocationFilePath,
                    destinationFilePath).ConfigureAwait(false);
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

        private static IReadOnlyList<string> BuildCustomerEmails(string? primaryEmail, string? extraEmails)
        {
            var emails = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddEmail(string? candidate)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return;
                }

                var trimmed = candidate.Trim();
                if (trimmed.Length == 0)
                {
                    return;
                }

                if (seen.Add(trimmed))
                {
                    emails.Add(trimmed);
                }
            }

            AddEmail(primaryEmail);

            foreach (var email in SplitEmails(extraEmails))
            {
                AddEmail(email);
            }

            return emails;
        }

        private static IReadOnlyList<RecipientAssignment> BuildRecipients(IEnumerable<EngagementManagerAssignment>? assignments)
        {
            if (assignments == null)
            {
                return Array.Empty<RecipientAssignment>();
            }

            var recipients = new List<RecipientAssignment>();
            var indexByEmail = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var assignment in assignments)
            {
                var manager = assignment.Manager;
                if (manager == null || string.IsNullOrWhiteSpace(manager.Email))
                {
                    continue;
                }

                var email = manager.Email.Trim();
                var name = string.IsNullOrWhiteSpace(manager.Name) ? null : manager.Name.Trim();

                if (!indexByEmail.TryGetValue(email, out var index))
                {
                    recipients.Add(new RecipientAssignment(email, name, manager.Position));
                    indexByEmail[email] = recipients.Count - 1;
                    continue;
                }

                var existing = recipients[index];
                var position = existing.Position == ManagerPosition.SeniorManager || manager.Position == ManagerPosition.SeniorManager
                    ? ManagerPosition.SeniorManager
                    : ManagerPosition.Manager;

                var resolvedName = existing.Name;
                if (string.IsNullOrWhiteSpace(resolvedName) && !string.IsNullOrWhiteSpace(name))
                {
                    resolvedName = name;
                }

                recipients[index] = existing with { Name = resolvedName, Position = position };
            }

            return recipients;
        }

        private static IReadOnlyList<RecipientAssignment> BuildPreviewRecipients(InvoiceNotificationPreview preview)
        {
            var emails = SplitEmails(preview.ManagerEmails);
            if (emails.Count == 0)
            {
                return Array.Empty<RecipientAssignment>();
            }

            var names = SplitNames(preview.ManagerNames);
            var recipients = new List<RecipientAssignment>(emails.Count);
            for (var index = 0; index < emails.Count; index++)
            {
                var email = emails[index];
                string? name = null;
                if (index < names.Count)
                {
                    name = names[index];
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    name = name.Trim();
                }
                else
                {
                    name = null;
                }

                recipients.Add(new RecipientAssignment(email, name, ManagerPosition.Manager));
            }

            return recipients;
        }

        private static IReadOnlyList<string?> SplitNames(string? value)
        {
            if (value == null)
            {
                return Array.Empty<string?>();
            }

            var parts = value.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                return Array.Empty<string?>();
            }

            var names = new string?[parts.Length];
            for (var index = 0; index < parts.Length; index++)
            {
                names[index] = string.IsNullOrWhiteSpace(parts[index]) ? null : parts[index];
            }

            return names;
        }

        private static IReadOnlyList<EtcFiscalYear> BuildEtcFiscalYears(Engagement engagement)
        {
            if (engagement.RankBudgets.Count == 0)
            {
                return Array.Empty<EtcFiscalYear>();
            }

            var groups = engagement.RankBudgets
                .Where(budget => budget.FiscalYear != null)
                .GroupBy(budget => new
                {
                    budget.FiscalYear!.Id,
                    budget.FiscalYear!.Name,
                    budget.FiscalYear!.StartDate
                })
                .OrderBy(group => group.Key.StartDate)
                .ThenBy(group => group.Key.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groups.Count == 0)
            {
                return Array.Empty<EtcFiscalYear>();
            }

            var fiscalYears = new List<EtcFiscalYear>(groups.Count);

            foreach (var group in groups)
            {
                var ranks = group
                    .OrderBy(budget => budget.RankName, StringComparer.OrdinalIgnoreCase)
                    .Select(budget => new EtcRankHours
                    {
                        Rank = budget.RankName,
                        ConsumedHours = Math.Round(budget.ConsumedHours, 2, MidpointRounding.AwayFromZero),
                        RemainingHours = Math.Round(budget.RemainingHours, 2, MidpointRounding.AwayFromZero)
                    })
                    .Where(rank => rank.ConsumedHours != 0m || rank.RemainingHours != 0m)
                    .ToList();

                if (ranks.Count == 0)
                {
                    continue;
                }

                fiscalYears.Add(new EtcFiscalYear
                {
                    Name = group.Key.Name,
                    StartDate = group.Key.StartDate,
                    Ranks = ranks
                });
            }

            return fiscalYears;
        }

        private static string BuildBodyText(MessageBucket bucket, DateTime referenceDate)
        {
            var builder = new StringBuilder();
            var greetingTarget = bucket.PrimaryName ?? bucket.GetOrderedRecipients().FirstOrDefault()?.Email ?? string.Empty;

            if (string.IsNullOrWhiteSpace(greetingTarget))
            {
                builder.AppendLine("Olá,");
            }
            else
            {
                var firstName = greetingTarget.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? greetingTarget;
                builder.Append("Olá ").Append(firstName).AppendLine(",");
            }

            builder.AppendLine();
            builder.AppendLine($"Segue o resumo das tarefas administrativas com referência à semana de {referenceDate:dd/MM/yyyy}.");
            builder.AppendLine();
            builder.Append("• Faturas planejadas: ").AppendLine(bucket.Invoices.Count.ToString(CultureInfo.InvariantCulture));
            builder.Append("• ETCs pendentes: ").AppendLine(bucket.Etcs.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine();
            builder.AppendLine("Qualquer dúvida, estamos à disposição.");
            builder.AppendLine();
            builder.AppendLine("Atenciosamente,");
            builder.Append("Equipe GRC Financial Control");

            return builder.ToString();
        }

        private static string BuildEtcMessage(MessageBucket bucket)
        {
            if (bucket.Etcs.Count == 0)
            {
                return "Nenhum ETC pendente para esta semana.";
            }

            return bucket.Etcs.Count == 1
                ? "Há 1 ETC previsto para acompanhamento nesta semana."
                : $"Há {bucket.Etcs.Count} ETCs previstos para acompanhamento nesta semana.";
        }

        private static string BuildInvoiceDescription(
            InvoiceNotificationPreview preview,
            Engagement? engagement,
            string? deliveryDescription,
            IReadOnlyList<string> customerEmails,
            string currency,
            decimal invoiceAmount,
            string? coeResponsible,
            InvoicePlanType? planType,
            string? customerTicket)
        {
            var builder = new StringBuilder();

            var serviceParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(engagement?.Description))
            {
                serviceParts.Add(engagement.Description.Trim());
            }
            else
            {
                serviceParts.Add(preview.EngagementId);
            }

            if (planType == InvoicePlanType.ByDelivery && !string.IsNullOrWhiteSpace(deliveryDescription))
            {
                serviceParts.Add(deliveryDescription.Trim());
            }

            builder.Append("Serviço: ").AppendLine(string.Join(" – ", serviceParts));

            if (!string.IsNullOrWhiteSpace(preview.PoNumber))
            {
                builder.Append("PO: ").AppendLine(preview.PoNumber.Trim());
            }

            if (!string.IsNullOrWhiteSpace(preview.FrsNumber))
            {
                builder.Append("FRS: ").AppendLine(preview.FrsNumber.Trim());
            }

            if (!string.IsNullOrWhiteSpace(customerTicket))
            {
                builder.Append("Ticket Cliente: ").AppendLine(customerTicket.Trim());
            }

            builder.Append("Parcela ")
                .Append(preview.SeqNo)
                .Append(" de ")
                .Append(preview.NumInvoices)
                .AppendLine();

            builder.Append("Valor da Parcela: ")
                .AppendLine(FormatCurrency(invoiceAmount, currency));

            builder.Append("Vencimento: ")
                .AppendLine(preview.ComputedDueDate.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")));

            var contactParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(preview.CustomerName))
            {
                contactParts.Add(preview.CustomerName.Trim());
            }

            if (!string.IsNullOrWhiteSpace(preview.CustomerFocalPointName))
            {
                contactParts.Add(preview.CustomerFocalPointName.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(preview.CustomerFocalPointEmail))
            {
                contactParts.Add(preview.CustomerFocalPointEmail.Trim());
            }

            if (!string.IsNullOrWhiteSpace(coeResponsible))
            {
                contactParts.Add(coeResponsible.Trim());
            }

            if (contactParts.Count > 0)
            {
                builder.Append("Contato: ").AppendLine(string.Join(" – ", contactParts));
            }

            if (customerEmails.Count > 0)
            {
                builder.Append("E-mails para envio: ").AppendLine(string.Join("; ", customerEmails));
            }

            return builder.ToString().TrimEnd();
        }

        private static string FormatCurrency(decimal amount, string currencyCode)
        {
            var culture = CultureInfo.GetCultureInfo("pt-BR");
            var formatted = amount.ToString("N2", culture);

            return string.Equals(currencyCode, "BRL", StringComparison.OrdinalIgnoreCase)
                ? $"R$ {formatted}"
                : string.Concat(currencyCode.ToUpperInvariant(), " ", formatted);
        }

        private static IReadOnlyList<EtcColumnDefinition> BuildEtcColumns(EtcExport etc)
        {
            var columns = new List<EtcColumnDefinition>
            {
                new("Rank", "Rank", false, null, EtcColumnKind.Rank)
            };

            var orderedFiscalYears = etc.FiscalYears
                .OrderBy(fy => fy.StartDate ?? DateTime.MaxValue)
                .ThenBy(fy => fy.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var index = 1;
            foreach (var fiscalYear in orderedFiscalYears)
            {
                var consumedName = $"FY{index}_Consumed";
                var remainingName = $"FY{index}_Remaining";
                columns.Add(new EtcColumnDefinition(consumedName, $"{fiscalYear.Name} - Horas incorridas", true, fiscalYear.Name, EtcColumnKind.Consumed));
                columns.Add(new EtcColumnDefinition(remainingName, $"{fiscalYear.Name} - Horas restantes", true, fiscalYear.Name, EtcColumnKind.Remaining));
                index++;
            }

            if (columns.Count == 1)
            {
                columns.Add(new EtcColumnDefinition("TotalHours", "Horas totais", false, null, EtcColumnKind.Total));
            }

            return columns;
        }

        private static IReadOnlyList<EtcRow> BuildEtcRows(EtcExport etc, IReadOnlyList<EtcColumnDefinition> columns)
        {
            var rankNames = etc.FiscalYears
                .SelectMany(fy => fy.Ranks)
                .Select(rank => rank.Rank)
                .Where(rank => !string.IsNullOrWhiteSpace(rank))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(rank => rank, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (rankNames.Count == 0)
            {
                rankNames.Add("Sem dados");
            }

            var rows = new List<EtcRow>(rankNames.Count);

            foreach (var rankName in rankNames)
            {
                var row = new EtcRow { Rank = rankName };
                foreach (var column in columns)
                {
                    if (column.Kind == EtcColumnKind.Rank)
                    {
                        continue;
                    }

                    var value = 0m;

                    if (!string.IsNullOrWhiteSpace(column.FiscalYear))
                    {
                        var fiscalYear = etc.FiscalYears.FirstOrDefault(fy => string.Equals(fy.Name, column.FiscalYear, StringComparison.OrdinalIgnoreCase));
                        if (fiscalYear != null)
                        {
                            var rankData = fiscalYear.Ranks.FirstOrDefault(rank => string.Equals(rank.Rank, rankName, StringComparison.OrdinalIgnoreCase));
                            if (rankData != null)
                            {
                                value = column.Kind == EtcColumnKind.Consumed
                                    ? rankData.ConsumedHours
                                    : rankData.RemainingHours;
                            }
                        }
                    }

                    var formattedValue = value.ToString("0.##", CultureInfo.InvariantCulture);
                    row.Cells.Add(new EtcCell(column.Name, formattedValue, column.FiscalYear));
                }

                rows.Add(row);
            }

            return rows;
        }

        private static IReadOnlyList<MessageBucket> BuildMessageBuckets(
            IReadOnlyList<InvoiceExport> invoices,
            IReadOnlyList<EtcExport> etcs)
        {
            var buckets = new Dictionary<string, MessageBucket>(StringComparer.OrdinalIgnoreCase);

            void Append(RecipientAssignment assignment, Action<MessageBucket> apply)
            {
                if (string.IsNullOrWhiteSpace(assignment.Email))
                {
                    return;
                }

                var email = assignment.Email.Trim();
                if (!buckets.TryGetValue(email, out var bucket))
                {
                    bucket = new MessageBucket(email);
                    buckets[email] = bucket;
                }

                bucket.AddRecipient(assignment);
                apply(bucket);
            }

            foreach (var invoice in invoices)
            {
                foreach (var recipient in invoice.Recipients)
                {
                    Append(recipient, bucket => bucket.AddInvoice(invoice));
                }
            }

            foreach (var etc in etcs)
            {
                foreach (var recipient in etc.Recipients)
                {
                    Append(recipient, bucket => bucket.AddEtc(etc));
                }
            }

            return buckets.Values
                .OrderBy(bucket => bucket.PrimaryName ?? bucket.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private sealed record RecipientAssignment(string Email, string? Name, ManagerPosition Position);

        private sealed class InvoiceExport
        {
            public int InvoiceItemId { get; init; }

            public string EngagementId { get; init; } = string.Empty;

            public string Customer { get; init; } = string.Empty;

            public string? FocalPointName { get; init; }

            public string? FocalPointEmail { get; init; }

            public string? Cnpj { get; init; }

            public string? Po { get; init; }

            public string? Frs { get; init; }

            public string? CustomerTicket { get; init; }

            public string PaymentTypeCode { get; init; } = PaymentTypeCatalog.TransferenciaBancariaCode;

            public string PaymentTypeName { get; init; } = PaymentTypeCatalog.GetByCode(PaymentTypeCatalog.TransferenciaBancariaCode).DisplayName;

            public decimal InvoiceValue { get; init; }

            public DateTime DateOfEmission { get; init; }

            public DateTime InvoiceDueDate { get; init; }

            public string? EmailsToSend { get; init; }

            public string InvoiceDescription { get; init; } = string.Empty;

            public string? AdditionalNotes { get; init; }

            public IReadOnlyList<RecipientAssignment> Recipients { get; init; } = Array.Empty<RecipientAssignment>();
        }

        private sealed class EtcExport
        {
            public string EngagementId { get; init; } = string.Empty;

            public string EngagementName { get; init; } = string.Empty;

            public string Customer { get; init; } = string.Empty;

            public DateTime? LastEtcDate { get; init; }

            public DateTime? ProposedNextEtcDate { get; init; }

            public IReadOnlyList<RecipientAssignment> Recipients { get; init; } = Array.Empty<RecipientAssignment>();

            public IReadOnlyList<EtcFiscalYear> FiscalYears { get; init; } = Array.Empty<EtcFiscalYear>();
        }

        private sealed class EtcFiscalYear
        {
            public string Name { get; init; } = string.Empty;

            public DateTime? StartDate { get; init; }

            public IReadOnlyList<EtcRankHours> Ranks { get; init; } = Array.Empty<EtcRankHours>();
        }

        private sealed class EtcRankHours
        {
            public string Rank { get; init; } = string.Empty;

            public decimal ConsumedHours { get; init; }

            public decimal RemainingHours { get; init; }
        }

        private sealed class MessageBucket
        {
            private readonly Dictionary<string, RecipientProfile> _recipients = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<int> _invoiceIds = new();
            private readonly HashSet<string> _etcKeys = new(StringComparer.OrdinalIgnoreCase);

            public MessageBucket(string key)
            {
                Key = key;
            }

            public string Key { get; }

            public List<InvoiceExport> Invoices { get; } = new();

            public List<EtcExport> Etcs { get; } = new();

            public string? PrimaryName => _recipients.Values.Select(r => r.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

            public void AddRecipient(RecipientAssignment assignment)
            {
                var email = assignment.Email.Trim();
                if (!_recipients.TryGetValue(email, out var profile))
                {
                    profile = new RecipientProfile(email, assignment.Name, assignment.Position);
                    _recipients[email] = profile;
                }
                else
                {
                    profile.Merge(assignment);
                }
            }

            public void AddInvoice(InvoiceExport invoice)
            {
                if (_invoiceIds.Add(invoice.InvoiceItemId))
                {
                    Invoices.Add(invoice);
                }
            }

            public void AddEtc(EtcExport etc)
            {
                var key = etc.EngagementId;
                if (etc.ProposedNextEtcDate.HasValue)
                {
                    key = string.Concat(key, "|", etc.ProposedNextEtcDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }

                if (_etcKeys.Add(key))
                {
                    Etcs.Add(etc);
                }
            }

            public IReadOnlyCollection<RecipientProfile> GetOrderedRecipients()
                => _recipients.Values
                    .OrderBy(recipient => recipient.Name ?? recipient.Email, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            public IReadOnlyCollection<InvoiceExport> GetOrderedInvoices()
                => Invoices
                    .OrderBy(invoice => invoice.EngagementId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(invoice => invoice.DateOfEmission)
                    .ThenBy(invoice => invoice.InvoiceItemId)
                    .ToArray();

            public IReadOnlyCollection<EtcExport> GetOrderedEtcs()
                => Etcs
                    .OrderBy(etc => etc.EngagementId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(etc => etc.ProposedNextEtcDate ?? DateTime.MaxValue)
                    .ToArray();
        }

        private sealed class RecipientProfile
        {
            private readonly HashSet<ManagerPosition> _positions = new();

            public RecipientProfile(string email, string? name, ManagerPosition position)
            {
                Email = email;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    Name = name.Trim();
                }

                _positions.Add(position);
            }

            public string Email { get; }

            public string? Name { get; private set; }

            public bool HasManagerRole => _positions.Contains(ManagerPosition.Manager);

            public bool HasSeniorRole => _positions.Contains(ManagerPosition.SeniorManager);

            public ManagerPosition DominantRole => HasSeniorRole ? ManagerPosition.SeniorManager : ManagerPosition.Manager;

            public void Merge(RecipientAssignment assignment)
            {
                if (!string.IsNullOrWhiteSpace(assignment.Name) && string.IsNullOrWhiteSpace(Name))
                {
                    Name = assignment.Name.Trim();
                }

                _positions.Add(assignment.Position);
            }
        }

        private sealed class EtcColumnDefinition
        {
            public EtcColumnDefinition(string name, string header, bool isDynamic, string? fiscalYear, EtcColumnKind kind)
            {
                Name = name;
                Header = header;
                IsDynamic = isDynamic;
                FiscalYear = fiscalYear;
                Kind = kind;
            }

            public string Name { get; }

            public string Header { get; }

            public bool IsDynamic { get; }

            public string? FiscalYear { get; }

            public EtcColumnKind Kind { get; }
        }

        private sealed class EtcRow
        {
            public string Rank { get; init; } = string.Empty;

            public List<EtcCell> Cells { get; } = new();
        }

        private sealed class EtcCell
        {
            public EtcCell(string columnName, string value, string? fiscalYear)
            {
                ColumnName = columnName;
                Value = value;
                FiscalYear = fiscalYear;
            }

            public string ColumnName { get; }

            public string Value { get; }

            public string? FiscalYear { get; }
        }

        private enum EtcColumnKind
        {
            Rank,
            Consumed,
            Remaining,
            Total
        }
    }
}
