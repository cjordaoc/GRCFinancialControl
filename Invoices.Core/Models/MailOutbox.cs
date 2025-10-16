namespace Invoices.Core.Models;

public class MailOutbox
{
    public int Id { get; set; }

    public DateTime NotificationDate { get; set; }

    public int InvoiceItemId { get; set; }

    public string ToName { get; set; } = string.Empty;

    public string ToEmail { get; set; } = string.Empty;

    public string? CcCsv { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string BodyText { get; set; } = string.Empty;

    public string? SendToken { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public InvoiceItem? InvoiceItem { get; set; }

    public ICollection<MailOutboxLog> Logs { get; } = new List<MailOutboxLog>();
}
