# GRC Financial Control — Functional Specification (vNext)

Defines all functional behaviors of the GRC Financial Control solution.
Each module section includes a **Happy Path** and links to its technical spec.

---

## 1 · Budget & Allocation Management
**Happy Path:**
1. User imports Budget File.
2. System validates structure and maps FY/Rank/Hours.
3. Data populates EngagementRankBudgets and History.
4. Adjustments apply per FY; closed years lock editing.

**Processing Flow:**
- Validate Excel headers.
- Map to Engagement via GUID.
- Upsert data.
- Refresh totals (Consumed, Remaining).

[See Technical Spec →](readme_specs.md#budget-allocation)

---

## 2 · Fiscal-Year Revenue Allocation
**Happy Path:**
1. On import, detect Current/Next FY columns.
2. Compute ToGo/ToDate for each FY.
3. Upsert to EngagementFiscalYearRevenueAllocations.

**Flow:**
- Parse Excel.
- Map FY values.
- Persist to DB.

[See Technical Spec →](readme_specs.md#revenue-allocation)

---

## 3 · Invoice Planner & Notifications
**Happy Path:**
1. User schedules invoices.
2. Daily event triggers mail procedure.
3. Emails sent automatically.

**Flow:**
- Build plan → sp_FillMailOutboxForDate → MailOutbox → SMTP worker.

[See Technical Spec →](readme_specs.md#invoice-planner)

---

## 4 · Excel Importers
**Happy Path:**
- User selects importer type.
- File validated, transformed, persisted.

[See Technical Spec →](readme_specs.md#excel-importers)

---

## 5 · Reporting Dashboards
**Happy Path:**
- User opens dashboard → KPIs render live from MySQL views.

[See Technical Spec →](readme_specs.md#dashboards)

---

## 6 · UI Architecture
- Avalonia + MVVM.
- Declarative XAML, no code-behind logic.
- Centralized modal overlay.

[See Technical Spec →](readme_specs.md#ui-architecture)

---
