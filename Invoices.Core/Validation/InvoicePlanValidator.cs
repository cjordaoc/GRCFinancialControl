using System.Globalization;
using System.Net.Mail;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Core.Resources;

namespace Invoices.Core.Validation;

public class InvoicePlanValidator : IInvoicePlanValidator
{
    public IReadOnlyList<string> Validate(InvoicePlan plan, decimal planningBaseValue)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(plan.EngagementId))
        {
            errors.Add(ValidationStrings.Get("EngagementIdRequired"));
        }

        if (plan.NumInvoices <= 0)
        {
            errors.Add(ValidationStrings.Get("NumInvoicesMinimum"));
        }

        if (plan.PaymentTermDays < 0)
        {
            errors.Add(ValidationStrings.Get("PaymentTermsNegative"));
        }

        if (string.IsNullOrWhiteSpace(plan.CustomerFocalPointName))
        {
            errors.Add(ValidationStrings.Get("CustomerFocalPointNameRequired"));
        }

        if (string.IsNullOrWhiteSpace(plan.CustomerFocalPointEmail))
        {
            errors.Add(ValidationStrings.Get("CustomerFocalPointEmailRequired"));
        }
        else if (!IsValidEmail(plan.CustomerFocalPointEmail))
        {
            errors.Add(ValidationStrings.Get("CustomerFocalPointEmailInvalid"));
        }

        if (plan.Type == InvoicePlanType.ByDate && plan.FirstEmissionDate is null)
        {
            errors.Add(ValidationStrings.Get("FirstEmissionRequired"));
        }

        if (plan.Items.Count == 0)
        {
            errors.Add(ValidationStrings.Get("ItemsRequired"));
        }

        if (plan.Items.Count != plan.NumInvoices)
        {
            errors.Add(ValidationStrings.Get("ItemsCountMismatch"));
        }

        var totalPercent = Math.Round(plan.Items.Sum(item => item.Percentage), 4, MidpointRounding.AwayFromZero);
        if (Math.Abs(totalPercent - 100m) > 0.0001m)
        {
            errors.Add(ValidationStrings.Format("TotalPercentageInvalid", totalPercent.ToString("F4", CultureInfo.CurrentCulture)));
        }

        var totalAmount = Math.Round(plan.Items.Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(totalAmount - Math.Round(planningBaseValue, 2, MidpointRounding.AwayFromZero)) > 0.01m)
        {
            errors.Add(ValidationStrings.Format("TotalAmountInvalid", planningBaseValue.ToString("F2", CultureInfo.CurrentCulture)));
        }

        var orderedItems = plan.Items
            .OrderBy(item => item.SeqNo)
            .ToList();

        for (var index = 0; index < orderedItems.Count; index++)
        {
            var expectedSeq = index + 1;
            var item = orderedItems[index];

            if (item.SeqNo != expectedSeq)
            {
                errors.Add(ValidationStrings.Get("SequenceContiguous"));
                break;
            }

            if (item.EmissionDate is null)
            {
                errors.Add(ValidationStrings.Format("EmissionDateRequired", expectedSeq));
            }

            if (plan.PaymentTermDays > 0 && item.EmissionDate is not null)
            {
                var expectedDue = item.EmissionDate.Value.AddDays(plan.PaymentTermDays);
                if (item.DueDate != expectedDue)
                {
                    errors.Add(ValidationStrings.Format("DueDateMustMatch", expectedSeq));
                }
            }

            if (plan.Type == InvoicePlanType.ByDelivery && string.IsNullOrWhiteSpace(item.DeliveryDescription))
            {
                errors.Add(ValidationStrings.Format("DeliveryDescriptionRequired", expectedSeq));
            }

            if (string.IsNullOrWhiteSpace(item.PayerCnpj))
            {
                errors.Add(ValidationStrings.Format("PayerCnpjRequired", expectedSeq));
            }

            if (string.IsNullOrWhiteSpace(item.PoNumber))
            {
                errors.Add(ValidationStrings.Format("PoNumberRequired", expectedSeq));
            }

            if (string.IsNullOrWhiteSpace(item.FrsNumber))
            {
                errors.Add(ValidationStrings.Format("FrsNumberRequired", expectedSeq));
            }

            if (string.IsNullOrWhiteSpace(item.CustomerTicket))
            {
                errors.Add(ValidationStrings.Format("CustomerTicketRequired", expectedSeq));
            }

        }

        foreach (var email in plan.AdditionalEmails)
        {
            if (string.IsNullOrWhiteSpace(email.Email))
            {
                errors.Add(ValidationStrings.Get("AdditionalEmailBlank"));
                continue;
            }

            if (!IsValidEmail(email.Email))
            {
                errors.Add(ValidationStrings.Format("AdditionalEmailInvalid", email.Email));
            }
        }

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
