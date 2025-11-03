using System.Globalization;
using System.Net.Mail;
using Invoices.Core.Enums;
using Invoices.Core.Models;
using Invoices.Core.Resources;
using Invoices.Core.Utilities;

namespace Invoices.Core.Validation;

public class InvoicePlanValidator : IInvoicePlanValidator
{
    public IReadOnlyList<string> Validate(InvoicePlan plan, decimal planningBaseValue)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(plan.EngagementId))
        {
            errors.Add(ValidationStrings.Get("Global_EngagementIdRequired"));
        }

        if (plan.NumInvoices <= 0)
        {
            errors.Add(ValidationStrings.Get("Global_NumInvoicesMinimum"));
        }

        if (plan.PaymentTermDays < 0)
        {
            errors.Add(ValidationStrings.Get("Global_PaymentTermsNegative"));
        }

        if (string.IsNullOrWhiteSpace(plan.CustomerFocalPointName))
        {
            errors.Add(ValidationStrings.Get("Global_CustomerFocalPointNameRequired"));
        }

        if (string.IsNullOrWhiteSpace(plan.CustomerFocalPointEmail))
        {
            errors.Add(ValidationStrings.Get("Global_CustomerFocalPointEmailRequired"));
        }
        else if (!IsValidEmail(plan.CustomerFocalPointEmail))
        {
            errors.Add(ValidationStrings.Get("Global_CustomerFocalPointEmailInvalid"));
        }

        if (plan.Type == InvoicePlanType.ByDate && plan.FirstEmissionDate is null)
        {
            errors.Add(ValidationStrings.Get("Global_FirstEmissionRequired"));
        }

        if (plan.Items.Count == 0)
        {
            errors.Add(ValidationStrings.Get("Global_ItemsRequired"));
        }

        if (plan.Items.Count != plan.NumInvoices)
        {
            errors.Add(ValidationStrings.Get("Global_ItemsCountMismatch"));
        }

        var totalPercent = Math.Round(plan.Items.Sum(item => item.Percentage), 4, MidpointRounding.AwayFromZero);
        if (Math.Abs(totalPercent - 100m) > 0.0001m)
        {
            errors.Add(ValidationStrings.Format("Global_TotalPercentageInvalid", totalPercent.ToString("F4", CultureInfo.CurrentCulture)));
        }

        var totalAmount = Math.Round(plan.Items.Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(totalAmount - Math.Round(planningBaseValue, 2, MidpointRounding.AwayFromZero)) > 0.01m)
        {
            errors.Add(ValidationStrings.Format("Global_TotalAmountInvalid", planningBaseValue.ToString("F2", CultureInfo.CurrentCulture)));
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
                errors.Add(ValidationStrings.Get("Global_SequenceContiguous"));
                break;
            }

            if (item.EmissionDate is null)
            {
                errors.Add(ValidationStrings.Format("Global_EmissionDateRequired", expectedSeq));
            }

            if (plan.PaymentTermDays > 0 && item.EmissionDate is not null)
            {
                var expectedDue = BusinessDayCalculator.AdjustToNextBusinessDay(
                    item.EmissionDate.Value.AddDays(plan.PaymentTermDays));
                if (item.DueDate != expectedDue)
                {
                    errors.Add(ValidationStrings.Format("Global_DueDateMustMatch", expectedSeq));
                }
            }

            if (plan.Type == InvoicePlanType.ByDelivery && string.IsNullOrWhiteSpace(item.DeliveryDescription))
            {
                errors.Add(ValidationStrings.Format("Global_DeliveryDescriptionRequired", expectedSeq));
            }

            if (string.IsNullOrWhiteSpace(item.PayerCnpj))
            {
                errors.Add(ValidationStrings.Format("Global_PayerCnpjRequired", expectedSeq));
            }

            if (string.IsNullOrWhiteSpace(item.PoNumber))
            {
                errors.Add(ValidationStrings.Format("Global_PoNumberRequired", expectedSeq));
            }

            if (string.IsNullOrWhiteSpace(item.FrsNumber))
            {
                errors.Add(ValidationStrings.Format("Global_FrsNumberRequired", expectedSeq));
            }

            if (string.IsNullOrWhiteSpace(item.CustomerTicket))
            {
                errors.Add(ValidationStrings.Format("Global_CustomerTicketRequired", expectedSeq));
            }

        }

        foreach (var email in plan.AdditionalEmails)
        {
            if (string.IsNullOrWhiteSpace(email.Email))
            {
                errors.Add(ValidationStrings.Get("Global_AdditionalEmailBlank"));
                continue;
            }

            if (!IsValidEmail(email.Email))
            {
                errors.Add(ValidationStrings.Format("Global_AdditionalEmailInvalid", email.Email));
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
