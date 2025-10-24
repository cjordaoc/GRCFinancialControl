using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services.Exporters.Json;

public sealed class PowerAutomateJsonPayloadBuilder : IPowerAutomateJsonPayloadBuilder
{
    public const string DefaultTimezone = "America/Sao_Paulo";
    public const string DefaultLocale = "pt-BR";
    private const string DefaultSubject = "Suas tarefas Administrativas para essa semana";
    private const string DefaultBodyTemplateHtml = "Olá,<br/><br/>...<br/>{{InvoicesTable}}<br/>{{EtcsTable}}<br/>";
    private const string DefaultCurrency = "BRL";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<PowerAutomateJsonPayloadBuilder> _logger;

    public PowerAutomateJsonPayloadBuilder(ILogger<PowerAutomateJsonPayloadBuilder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string BuildPayload(
        IReadOnlyCollection<ManagerEmailData> managerData,
        DateTimeOffset scheduledAt,
        string timezone,
        string locale)
    {
        ArgumentNullException.ThrowIfNull(managerData);

        timezone = string.IsNullOrWhiteSpace(timezone) ? DefaultTimezone : timezone;
        locale = string.IsNullOrWhiteSpace(locale) ? DefaultLocale : locale;

        if (!string.Equals(timezone, DefaultTimezone, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Using custom timezone '{Timezone}' for Power Automate payload (default: {DefaultTimezone}).",
                timezone,
                DefaultTimezone);
        }

        if (!string.Equals(locale, DefaultLocale, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Using custom locale '{Locale}' for Power Automate payload (default: {DefaultLocale}).",
                locale,
                DefaultLocale);
        }

        var validManagers = new List<ManagerEmailData>(managerData.Count);
        foreach (var manager in managerData)
        {
            if (manager == null)
            {
                continue;
            }

            var email = (manager.ManagerEmail ?? string.Empty).Trim();
            if (!IsValidEmail(email))
            {
                _logger.LogError(
                    "Skipping manager {ManagerName} because the email '{ManagerEmail}' is invalid for Power Automate export.",
                    manager.ManagerName,
                    manager.ManagerEmail);
                continue;
            }

            validManagers.Add(manager);
        }

        var messages = BuildMessages(validManagers);
        var warningCount = validManagers.Count(manager => !string.IsNullOrWhiteSpace(manager.WarningBodyHtml));

        _logger.LogInformation(
            "Built Power Automate payload for {MessageCount} managers ({WarningCount} warnings).",
            messages.Count,
            warningCount);

        var payload = new PowerAutomateJsonPayload
        {
            Meta = new PowerAutomateJsonMeta
            {
                ScheduledAt = FormatScheduledAt(scheduledAt),
                Timezone = timezone,
                Locale = locale
            },
            Messages = messages
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static List<PowerAutomateJsonMessage> BuildMessages(IEnumerable<ManagerEmailData> managerData)
    {
        var messages = new List<PowerAutomateJsonMessage>();
        var managerList = managerData
            .OrderBy(manager => manager.ManagerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < managerList.Count; index++)
        {
            var manager = managerList[index];
            var isWarning = !string.IsNullOrWhiteSpace(manager.WarningBodyHtml);

            var message = new PowerAutomateJsonMessage
            {
                Id = (index + 1).ToString(CultureInfo.InvariantCulture),
                To = new[]
                {
                    new PowerAutomateRecipient
                    {
                        Name = manager.ManagerName,
                        Email = (manager.ManagerEmail ?? string.Empty).Trim()
                    }
                },
                Cc = Array.Empty<PowerAutomateRecipient>(),
                Subject = DefaultSubject,
                BodyTemplate = new PowerAutomateBodyTemplate
                {
                    Type = "html",
                    Value = isWarning ? manager.WarningBodyHtml! : DefaultBodyTemplateHtml
                },
                Invoices = isWarning
                    ? Array.Empty<PowerAutomateInvoice>()
                    : BuildInvoices(manager.Invoices),
                Etcs = isWarning
                    ? Array.Empty<PowerAutomateEtc>()
                    : BuildEtcs(manager.Etcs)
            };

            messages.Add(message);
        }

        return messages;
    }

    private static IReadOnlyList<PowerAutomateInvoice> BuildInvoices(IEnumerable<InvoiceEmailData> invoices)
    {
        return invoices
            .OrderByDescending(invoice => invoice.IssueDate ?? DateTime.MinValue)
            .ThenByDescending(invoice => invoice.ParcelNumber)
            .Select(invoice => new PowerAutomateInvoice
            {
                EngagementCode = invoice.EngagementCode,
                EngagementName = invoice.EngagementName,
                CustomerName = invoice.CustomerName,
                ParcelNumber = invoice.ParcelNumber,
                TotalParcels = invoice.TotalParcels,
                IssueDate = FormatDate(invoice.IssueDate),
                DueDate = FormatDate(invoice.DueDate),
                Amount = invoice.Amount,
                Currency = string.IsNullOrWhiteSpace(invoice.Currency) ? DefaultCurrency : invoice.Currency,
                PoNumber = invoice.PoNumber,
                FrsNumber = invoice.FrsNumber,
                RitmNumber = invoice.RitmNumber,
                CustomerFocalPointName = invoice.CustomerFocalPointName,
                CustomerFocalPointEmail = invoice.CustomerFocalPointEmail
            })
            .ToList();
    }

    private static IReadOnlyList<PowerAutomateEtc> BuildEtcs(IEnumerable<EtcEmailData> etcs)
    {
        return etcs
            .OrderBy(etc => etc.ProposedCompletionDate ?? DateTime.MaxValue)
            .ThenBy(etc => etc.RankName, StringComparer.OrdinalIgnoreCase)
            .Select(etc => new PowerAutomateEtc
            {
                EngagementCode = etc.EngagementCode,
                EngagementName = etc.EngagementName,
                CustomerName = etc.CustomerName,
                RankName = etc.RankName,
                BudgetHours = etc.BudgetHours,
                ConsumedHours = etc.ConsumedHours,
                AdditionalHours = etc.AdditionalHours,
                RemainingHours = etc.RemainingHours,
                Status = etc.Status,
                FiscalYearName = etc.FiscalYearName,
                LastEtcDate = FormatDate(etc.LastEtcDate),
                ProposedCompletionDate = FormatDate(etc.ProposedCompletionDate)
            })
            .ToList();
    }

    private static string FormatScheduledAt(DateTimeOffset scheduledAt)
    {
        return scheduledAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    private static string? FormatDate(DateTime? date)
    {
        return date.HasValue
            ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed class PowerAutomateJsonPayload
    {
        [JsonPropertyName("version")]
        public string Version { get; init; } = "2.0";

        [JsonPropertyName("meta")]
        public PowerAutomateJsonMeta Meta { get; init; } = new();

        [JsonPropertyName("messages")]
        public IReadOnlyList<PowerAutomateJsonMessage> Messages { get; init; } = Array.Empty<PowerAutomateJsonMessage>();
    }

    private sealed class PowerAutomateJsonMeta
    {
        [JsonPropertyName("scheduledAt")]
        public string ScheduledAt { get; init; } = string.Empty;

        [JsonPropertyName("timezone")]
        public string Timezone { get; init; } = string.Empty;

        [JsonPropertyName("locale")]
        public string Locale { get; init; } = string.Empty;
    }

    private sealed class PowerAutomateJsonMessage
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("to")]
        public IReadOnlyList<PowerAutomateRecipient> To { get; init; } = Array.Empty<PowerAutomateRecipient>();

        [JsonPropertyName("cc")]
        public IReadOnlyList<PowerAutomateRecipient> Cc { get; init; } = Array.Empty<PowerAutomateRecipient>();

        [JsonPropertyName("subject")]
        public string Subject { get; init; } = string.Empty;

        [JsonPropertyName("bodyTemplate")]
        public PowerAutomateBodyTemplate BodyTemplate { get; init; } = new();

        [JsonPropertyName("invoices")]
        public IReadOnlyList<PowerAutomateInvoice> Invoices { get; init; } = Array.Empty<PowerAutomateInvoice>();

        [JsonPropertyName("etcs")]
        public IReadOnlyList<PowerAutomateEtc> Etcs { get; init; } = Array.Empty<PowerAutomateEtc>();
    }

    private sealed class PowerAutomateRecipient
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; init; } = string.Empty;
    }

    private sealed class PowerAutomateBodyTemplate
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; init; } = string.Empty;
    }

    private sealed class PowerAutomateInvoice
    {
        [JsonPropertyName("engagementCode")]
        public string EngagementCode { get; init; } = string.Empty;

        [JsonPropertyName("engagementName")]
        public string EngagementName { get; init; } = string.Empty;

        [JsonPropertyName("customerName")]
        public string CustomerName { get; init; } = string.Empty;

        [JsonPropertyName("parcelNumber")]
        public int ParcelNumber { get; init; }

        [JsonPropertyName("totalParcels")]
        public int TotalParcels { get; init; }

        [JsonPropertyName("issueDate")]
        public string? IssueDate { get; init; }

        [JsonPropertyName("dueDate")]
        public string? DueDate { get; init; }

        [JsonPropertyName("amount")]
        public decimal Amount { get; init; }

        [JsonPropertyName("currency")]
        public string Currency { get; init; } = string.Empty;

        [JsonPropertyName("poNumber")]
        public string PoNumber { get; init; } = string.Empty;

        [JsonPropertyName("frsNumber")]
        public string FrsNumber { get; init; } = string.Empty;

        [JsonPropertyName("ritmNumber")]
        public string RitmNumber { get; init; } = string.Empty;

        [JsonPropertyName("customerFocalPointName")]
        public string CustomerFocalPointName { get; init; } = string.Empty;

        [JsonPropertyName("customerFocalPointEmail")]
        public string CustomerFocalPointEmail { get; init; } = string.Empty;
    }

    private sealed class PowerAutomateEtc
    {
        [JsonPropertyName("engagementCode")]
        public string EngagementCode { get; init; } = string.Empty;

        [JsonPropertyName("engagementName")]
        public string EngagementName { get; init; } = string.Empty;

        [JsonPropertyName("customerName")]
        public string CustomerName { get; init; } = string.Empty;

        [JsonPropertyName("rankName")]
        public string RankName { get; init; } = string.Empty;

        [JsonPropertyName("budgetHours")]
        public decimal BudgetHours { get; init; }

        [JsonPropertyName("consumedHours")]
        public decimal ConsumedHours { get; init; }

        [JsonPropertyName("additionalHours")]
        public decimal AdditionalHours { get; init; }

        [JsonPropertyName("remainingHours")]
        public decimal RemainingHours { get; init; }

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("fiscalYearName")]
        public string FiscalYearName { get; init; } = string.Empty;

        [JsonPropertyName("lastEtcDate")]
        public string? LastEtcDate { get; init; }

        [JsonPropertyName("proposedCompletionDate")]
        public string? ProposedCompletionDate { get; init; }
    }
}
