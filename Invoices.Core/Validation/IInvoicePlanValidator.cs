using Invoices.Core.Models;

namespace Invoices.Core.Validation;

public interface IInvoicePlanValidator
{
    IReadOnlyList<string> Validate(InvoicePlan plan, decimal planningBaseValue);
}
