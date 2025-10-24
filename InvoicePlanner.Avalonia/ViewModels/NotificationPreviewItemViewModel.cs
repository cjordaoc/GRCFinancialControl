using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using Invoices.Core.Models;

namespace InvoicePlanner.Avalonia.ViewModels;

public class NotificationPreviewItemViewModel : ObservableObject
{
    public NotificationPreviewItemViewModel(InvoiceNotificationPreview preview)
    {
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));

        EngagementDisplay = string.IsNullOrWhiteSpace(preview.EngagementDescription)
            ? preview.EngagementId
            : $"{preview.EngagementDescription} ({preview.EngagementId})";

        SequenceDisplay = LocalizationRegistry.Format("Notifications.Format.Sequence", preview.SeqNo, preview.NumInvoices);
        ToRecipient = BuildToRecipient(preview);
        CcRecipients = BuildCcRecipients(preview);
        Subject = BuildSubject(preview);
        Body = BuildBody(preview);
        RecipientEmails = BuildRecipientEmails(preview);
        ManagerNames = NormalizeList(preview.ManagerNames);
        CustomerDisplay = string.IsNullOrWhiteSpace(preview.CustomerName)
            ? string.Empty
            : LocalizationRegistry.Format("Notifications.Format.Customer", preview.CustomerName);
        PlanDisplay = LocalizationRegistry.Format("Notifications.Format.Plan", preview.PlanId);
        NotifyDisplay = LocalizationRegistry.Format("Notifications.Format.NotifyDate", preview.NotifyDate);
        EmissionDisplay = LocalizationRegistry.Format("Notifications.Format.EmissionDate", preview.EmissionDate);
        DueDisplay = LocalizationRegistry.Format("Notifications.Format.DueDate", preview.ComputedDueDate);
        AmountDisplay = LocalizationRegistry.Format("Notifications.Format.Amount", preview.Amount);
        ToDisplay = LocalizationRegistry.Format("Notifications.Format.ToRecipients", ToRecipient);
        CcDisplay = LocalizationRegistry.Format("Notifications.Format.CcRecipients", string.IsNullOrWhiteSpace(CcRecipients) ? string.Empty : CcRecipients);
        RecipientsDisplay = LocalizationRegistry.Format("Notifications.Format.Recipients", RecipientEmails);
        ManagersDisplay = LocalizationRegistry.Format("Notifications.Format.Managers", ManagerNames);
    }

    public InvoiceNotificationPreview Preview { get; }

    public string EngagementDisplay { get; }

    public string SequenceDisplay { get; }

    public string ToRecipient { get; }

    public string CcRecipients { get; }

    public string RecipientEmails { get; }

    public string Subject { get; }

    public string Body { get; }

    public string? ManagerNames { get; }

    public string CustomerDisplay { get; }

    public string PlanDisplay { get; }

    public string NotifyDisplay { get; }

    public string EmissionDisplay { get; }

    public string DueDisplay { get; }

    public string AmountDisplay { get; }

    public string ToDisplay { get; }

    public string CcDisplay { get; }

    public string RecipientsDisplay { get; }

    public string ManagersDisplay { get; }

    public bool HasCc => !string.IsNullOrWhiteSpace(CcRecipients);

    public bool HasManagerNames => !string.IsNullOrWhiteSpace(ManagerNames);

    public int Sequence => Preview.SeqNo;

    public int PlanId => Preview.PlanId;

    public DateTime NotifyDate => Preview.NotifyDate;

    public DateTime EmissionDate => Preview.EmissionDate;

    public DateTime DueDate => Preview.ComputedDueDate;

    public decimal Amount => Preview.Amount;

    public string? CustomerName => Preview.CustomerName;

    public bool HasCustomerName => !string.IsNullOrWhiteSpace(CustomerName);

    private static string BuildToRecipient(InvoiceNotificationPreview preview)
    {
        var name = preview.CustomerFocalPointName?.Trim();
        var email = preview.CustomerFocalPointEmail?.Trim();

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
        {
            return $"{name} <{email}>";
        }

        return string.IsNullOrWhiteSpace(email) ? name ?? string.Empty : email;
    }

    private static string BuildCcRecipients(InvoiceNotificationPreview preview)
    {
        var segments = new List<string>();
        var managers = NormalizeList(preview.ManagerEmails);
        var extras = NormalizeList(preview.ExtraEmails);

        if (!string.IsNullOrWhiteSpace(managers))
        {
            segments.Add(managers);
        }

        if (!string.IsNullOrWhiteSpace(extras))
        {
            segments.Add(extras);
        }

        return string.Join(';', segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
    }

    private static string BuildRecipientEmails(InvoiceNotificationPreview preview)
    {
        var list = new List<string>();
        var focal = preview.CustomerFocalPointEmail?.Trim();
        var extras = NormalizeList(preview.ExtraEmails);

        if (!string.IsNullOrWhiteSpace(focal))
        {
            list.Add(focal);
        }

        if (!string.IsNullOrWhiteSpace(extras))
        {
            list.AddRange(extras.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return string.Join(';', list.Where(entry => !string.IsNullOrWhiteSpace(entry)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildSubject(InvoiceNotificationPreview preview)
    {
        return LocalizationRegistry.Format("Notifications.Format.Subject", preview.EmissionDate, preview.EngagementId, preview.SeqNo, preview.NumInvoices);
    }

    private static string BuildBody(InvoiceNotificationPreview preview)
    {
        var builder = new StringBuilder();
        var managerNames = NormalizeList(preview.ManagerNames);
        var managerEmails = NormalizeList(preview.ManagerEmails);
        var recipientEmails = BuildRecipientEmails(preview);
        var culture = CultureInfo.CurrentUICulture;

        builder.AppendLine(LocalizationRegistry.Get("Notifications.Template.Service"));
        builder.AppendLine(LocalizationRegistry.Format("Notifications.Template.Competence", preview.EmissionDate.ToString("MMM/yyyy", culture)));
        builder.AppendLine(LocalizationRegistry.Format("Notifications.Template.Po", preview.PoNumber ?? string.Empty));
        builder.AppendLine(LocalizationRegistry.Format("Notifications.Template.Frs", preview.FrsNumber ?? string.Empty));
        builder.AppendLine(LocalizationRegistry.Format("Notifications.Template.Ticket", preview.RitmNumber ?? string.Empty));
        builder.AppendLine(LocalizationRegistry.Format("Notifications.Template.Installment", preview.SeqNo, preview.NumInvoices));
        builder.AppendLine(LocalizationRegistry.Format("Notifications.Template.Amount", preview.Amount.ToString("C2", CultureInfo.CurrentCulture)));
        builder.AppendLine(LocalizationRegistry.Format("Notifications.Template.DueDate", preview.ComputedDueDate.ToString("dd/MM/yyyy", culture)));
        builder.AppendLine(LocalizationRegistry.Format("Notifications.Template.Contact", preview.CustomerName ?? string.Empty, preview.CustomerFocalPointName ?? string.Empty));
        builder.AppendLine(LocalizationRegistry.Format("Notifications.Template.Recipients", recipientEmails));
        builder.AppendLine(LocalizationRegistry.Format("Notifications.Template.Managers", managerNames));
        builder.Append(LocalizationRegistry.Format("Notifications.Template.ManagerEmails", managerEmails));

        return builder.ToString();
    }

    private static string NormalizeList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var entries = value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return entries.Length == 0 ? string.Empty : string.Join(';', entries);
    }
}
