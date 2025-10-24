# Technical Specification — GRC Financial Control (vNext)

## Budget Allocation
**Tables:** EngagementRankBudgets, EngagementRankBudgetHistory
**Classes:** EngagementRankBudgetService, BudgetImporter
**Flow:** Parse Excel → Validate → Upsert → Append History
**Performance:** Dictionary lookups, async bulk inserts.

## Revenue Allocation
**Tables:** EngagementFiscalYearRevenueAllocations
**Classes:** AllocationImporter, AllocationCalculator
**Flow:** Map Excel FY → Upsert allocations.

## Invoice Planner
**Tables:** InvoicePlan, InvoiceItem, MailOutbox, MailOutboxLog
**Classes:** InvoicePlannerService, NotificationWorker
**Flow:** Create plan → Fill MailOutbox → Dispatch emails.

## Excel Importers
**Libraries:** EPPlus / ClosedXML
**Classes:** ExcelReaderBase, ExcelBudgetImporter, ExcelAllocationImporter
**Flow:** Validate headers → Transform → Bulk insert.

## Dashboards
**Views:** Avalonia Charts
**Data:** MySQL Views
**Performance:** Lazy load, cache static data.

## UI Architecture
**Pattern:** MVVM (Avalonia)
**Rules:** One View ↔ One ViewModel. Declarative. Central ModalService.
