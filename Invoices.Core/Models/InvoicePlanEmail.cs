namespace Invoices.Core.Models;

public class InvoicePlanEmail
{
    public int Id { get; set; }

    public int PlanId { get; set; }

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public InvoicePlan? Plan { get; set; }
}
