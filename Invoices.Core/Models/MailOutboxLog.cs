namespace Invoices.Core.Models;

public class MailOutboxLog
{
    public int Id { get; set; }

    public int OutboxId { get; set; }

    public DateTime AttemptAt { get; set; }

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public MailOutbox? Outbox { get; set; }
}
