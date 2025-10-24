# Class & Interface Catalog — GRC Financial Control (vNext)

Purpose: Single source of truth for all classes/interfaces used by Codex and developers.
This file contains **metadata only**, not code.

---
## Usage Rules
1. Always parse this file before creating or modifying code.
2. Reuse existing classes/interfaces when possible.
3. Extend with *Extension* or *Adapter* suffix if variation is required.
4. If nothing fits, create a new one and update this catalog.
5. Codex must maintain it in-flight when creating new types or refactoring existing ones.
6. This file must never include code — only metadata.

---
## Example Structure

| Type | Namespace | Name | Purpose | Key Members | Dependencies | Status | Notes |
|------|------------|------|----------|--------------|---------------|--------|-------|
| Class | GRCFinancialControl.Domain.Engagements | Engagement | Represents a customer engagement. | Id, Name, FiscalYearId | FiscalYear, Manager | Stable | Core domain entity |
| Interface | GRCFinancialControl.Domain.Interfaces | IEngagementRepository | CRUD contract for Engagements. | Add(), GetById() | ApplicationDbContext | Active | Implemented by EngagementRepository |

---
## Domains

### Engagement Management
- Engagement, IEngagementRepository, EngagementService

### Budget & Allocation
- EngagementRankBudgetService, IBudgetImporter, AllocationImporter

### Invoice & Notifications
- InvoicePlannerService, INotificationSender, NotificationWorker

### Infrastructure
- ExcelReaderBase, IDatabaseManager, ModalService

---
