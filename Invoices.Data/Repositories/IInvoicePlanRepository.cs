using System;
using Invoices.Core.Models;

namespace Invoices.Data.Repositories;

public interface IInvoicePlanRepository
{
    InvoicePlan? GetPlan(int planId);

    IReadOnlyList<InvoicePlan> ListPlansForEngagement(string engagementId);

    RepositorySaveResult SavePlan(InvoicePlan plan);

    RepositorySaveResult MarkItemsAsRequested(int planId, IReadOnlyCollection<InvoiceRequestUpdate> updates);

    RepositorySaveResult UndoRequest(int planId, IReadOnlyCollection<int> itemIds);

    RepositorySaveResult CloseItems(int planId, IReadOnlyCollection<InvoiceEmissionUpdate> updates);

    RepositorySaveResult CancelAndReissue(int planId, IReadOnlyCollection<InvoiceReissueRequest> requests);

    InvoiceSummaryResult SearchSummary(InvoiceSummaryFilter filter);

    IReadOnlyList<InvoiceNotificationPreview> PreviewNotifications(DateTime notificationDate);
}
