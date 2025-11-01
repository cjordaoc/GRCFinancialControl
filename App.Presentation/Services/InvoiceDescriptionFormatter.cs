using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Invoices.Core.Enums;

namespace App.Presentation.Services;

/// <summary>
/// Provides a shared formatter that mirrors the Power Automate invoice description template.
/// </summary>
public static class InvoiceDescriptionFormatter
{
    /// <summary>
    /// Builds the invoice description text used by Power Automate based on the supplied context.
    /// </summary>
    /// <param name="context">Metadata describing the invoice line and recipients.</param>
    /// <returns>A formatted description string.</returns>
    public static string Format(InvoiceDescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new System.Text.StringBuilder();
        var culture = CultureInfo.GetCultureInfo("pt-BR");

        var serviceParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(context.EngagementDescription))
        {
            serviceParts.Add(context.EngagementDescription.Trim());
        }
        else
        {
            serviceParts.Add(context.EngagementId);
        }

        if (context.PlanType == InvoicePlanType.ByDelivery && !string.IsNullOrWhiteSpace(context.DeliveryDescription))
        {
            serviceParts.Add(context.DeliveryDescription.Trim());
        }

        builder.Append("Serviço: ")
            .AppendLine(string.Join(" – ", serviceParts));

        if (!string.IsNullOrWhiteSpace(context.PoNumber))
        {
            builder.Append("PO: ").AppendLine(context.PoNumber.Trim());
        }

        if (!string.IsNullOrWhiteSpace(context.FrsNumber))
        {
            builder.Append("FRS: ").AppendLine(context.FrsNumber.Trim());
        }

        if (!string.IsNullOrWhiteSpace(context.CustomerTicket))
        {
            builder.Append("Ticket Cliente: ").AppendLine(context.CustomerTicket.Trim());
        }

        builder.Append("Parcela ")
            .Append(context.Sequence)
            .Append(" de ")
            .Append(context.TotalInvoices)
            .AppendLine();

        builder.Append("Valor da Parcela: ")
            .AppendLine(CurrencyDisplayHelper.Format(context.Amount, Normalize(context.CurrencyCode)));

        builder.Append("Vencimento: ")
            .AppendLine(context.DueDate.ToString("dd/MM/yyyy", culture));

        var contactParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(context.CustomerName))
        {
            contactParts.Add(context.CustomerName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(context.CustomerFocalPointName))
        {
            contactParts.Add(context.CustomerFocalPointName.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(context.CustomerFocalPointEmail))
        {
            contactParts.Add(context.CustomerFocalPointEmail.Trim());
        }

        if (!string.IsNullOrWhiteSpace(context.CoeResponsible))
        {
            contactParts.Add(context.CoeResponsible.Trim());
        }

        if (contactParts.Count > 0)
        {
            builder.Append("Contato: ")
                .AppendLine(string.Join(" – ", contactParts));
        }

        var recipients = context.CustomerEmails?.Where(email => !string.IsNullOrWhiteSpace(email)).ToList()
            ?? new List<string>();

        if (recipients.Count > 0)
        {
            builder.Append("E-mails para envio: ")
                .AppendLine(string.Join("; ", recipients));
        }

        return builder.ToString().TrimEnd();
    }

    private static string? Normalize(string? currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode) ? null : currencyCode.Trim();
    }
}

/// <summary>
/// Represents metadata required to build the Power Automate invoice description text.
/// </summary>
public sealed record InvoiceDescriptionContext
{
    public required string EngagementId { get; init; }

    public string? EngagementDescription { get; init; }

    public int Sequence { get; init; }

    public int TotalInvoices { get; init; }

    public DateTime DueDate { get; init; }

    public decimal Amount { get; init; }

    public string? CurrencyCode { get; init; }

    public InvoicePlanType? PlanType { get; init; }

    public string? DeliveryDescription { get; init; }

    public string? PoNumber { get; init; }

    public string? FrsNumber { get; init; }

    public string? CustomerTicket { get; init; }

    public string? CustomerName { get; init; }

    public string? CustomerFocalPointName { get; init; }

    public string? CustomerFocalPointEmail { get; init; }

    public string? CoeResponsible { get; init; }

    public IReadOnlyList<string> CustomerEmails { get; init; } = Array.Empty<string>();
}
