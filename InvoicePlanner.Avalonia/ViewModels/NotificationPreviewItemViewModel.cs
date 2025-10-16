using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Invoices.Core.Models;
using InvoicePlanner.Avalonia.Resources;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class NotificationPreviewItemViewModel : ObservableObject
{
    public NotificationPreviewItemViewModel(InvoiceNotificationPreview preview)
    {
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));

        EngagementDisplay = string.IsNullOrWhiteSpace(preview.EngagementDescription)
            ? preview.EngagementId
            : $"{preview.EngagementDescription} ({preview.EngagementId})";

        SequenceDisplay = Strings.Format("NotificationPreviewSequenceFormat", preview.SeqNo, preview.NumInvoices);
        ToRecipient = BuildToRecipient(preview);
        CcRecipients = BuildCcRecipients(preview);
        Subject = BuildSubject(preview);
        Body = BuildBody(preview);
        RecipientEmails = BuildRecipientEmails(preview);
        ManagerNames = NormalizeList(preview.ManagerNames);
        CustomerDisplay = string.IsNullOrWhiteSpace(preview.CustomerName)
            ? string.Empty
            : Strings.Format("NotificationPreviewCustomerFormat", preview.CustomerName);
        PlanDisplay = Strings.Format("NotificationPreviewPlanFormat", preview.PlanId);
        NotifyDisplay = Strings.Format("NotificationPreviewNotifyFormat", preview.NotifyDate);
        EmissionDisplay = Strings.Format("NotificationPreviewEmissionFormat", preview.EmissionDate);
        DueDisplay = Strings.Format("NotificationPreviewDueFormat", preview.ComputedDueDate);
        AmountDisplay = Strings.Format("NotificationPreviewAmountFormat", preview.Amount);
        ToDisplay = Strings.Format("NotificationPreviewToFormat", ToRecipient);
        CcDisplay = Strings.Format("NotificationPreviewCcFormat", string.IsNullOrWhiteSpace(CcRecipients) ? string.Empty : CcRecipients);
        RecipientsDisplay = Strings.Format("NotificationPreviewRecipientsFormat", RecipientEmails);
        ManagersDisplay = Strings.Format("NotificationPreviewManagersFormat", ManagerNames);
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
        return Strings.Format("NotificationPreviewSubjectFormat", preview.EmissionDate, preview.EngagementId, preview.SeqNo, preview.NumInvoices);
    }

    private static string BuildBody(InvoiceNotificationPreview preview)
    {
        var builder = new StringBuilder();
        var managerNames = NormalizeList(preview.ManagerNames);
        var managerEmails = NormalizeList(preview.ManagerEmails);
        var recipientEmails = BuildRecipientEmails(preview);
        var culture = CultureInfo.CurrentUICulture;

        builder.AppendLine(Strings.Get("NotificationPreviewBodyService"));
        builder.AppendLine(Strings.Format("NotificationPreviewBodyCompetenceFormat", preview.EmissionDate.ToString("MMM/yyyy", culture)));
        builder.AppendLine(Strings.Format("NotificationPreviewBodyPoFormat", preview.PoNumber ?? string.Empty));
        builder.AppendLine(Strings.Format("NotificationPreviewBodyFrsFormat", preview.FrsNumber ?? string.Empty));
        builder.AppendLine(Strings.Format("NotificationPreviewBodyTicketFormat", preview.RitmNumber ?? string.Empty));
        builder.AppendLine(Strings.Format("NotificationPreviewBodyInstallmentFormat", preview.SeqNo, preview.NumInvoices));
        builder.AppendLine(Strings.Format("NotificationPreviewBodyAmountFormat", preview.Amount.ToString("C2", CultureInfo.CurrentCulture)));
        builder.AppendLine(Strings.Format("NotificationPreviewBodyDueFormat", preview.ComputedDueDate.ToString("dd/MM/yyyy", culture)));
        builder.AppendLine(Strings.Format("NotificationPreviewBodyContactFormat", preview.CustomerName ?? string.Empty, preview.CustomerFocalPointName ?? string.Empty));
        builder.AppendLine(Strings.Format("NotificationPreviewBodyRecipientsFormat", recipientEmails));
        builder.AppendLine(Strings.Format("NotificationPreviewBodyManagersFormat", managerNames));
        builder.Append(Strings.Format("NotificationPreviewBodyManagerEmailsFormat", managerEmails));

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
