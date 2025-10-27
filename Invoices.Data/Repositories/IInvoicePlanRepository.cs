using System;
using System.Collections.Generic;
using Invoices.Core.Models;

namespace Invoices.Data.Repositories;

public interface IInvoicePlanRepository
{
    InvoicePlan? GetPlan(int planId);

    IReadOnlyList<InvoicePlan> ListPlansForEngagement(string engagementId);

    IReadOnlyList<EngagementLookup> ListEngagementsForPlanning();

    IReadOnlyList<InvoicePlanSummary> ListPlansForRequestStage();

    IReadOnlyList<InvoicePlanSummary> ListPlansForEmissionStage();

    RepositorySaveResult SavePlan(InvoicePlan plan);

    RepositorySaveResult MarkItemsAsRequested(int planId, IReadOnlyCollection<InvoiceRequestUpdate> updates);

    RepositorySaveResult UndoRequest(int planId, IReadOnlyCollection<int> itemIds);

    RepositorySaveResult CloseItems(int planId, IReadOnlyCollection<InvoiceEmissionUpdate> updates);

    RepositorySaveResult CancelEmissions(int planId, IReadOnlyCollection<InvoiceEmissionCancellation> cancellations);

    InvoiceSummaryResult SearchSummary(InvoiceSummaryFilter filter);

    IReadOnlyList<InvoiceNotificationPreview> PreviewNotifications(DateTime notificationDate);

    RepositorySaveResult DeletePlan(int planId);
}
