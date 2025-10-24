# GRC Financial Control — Functional Overview (vNext)

The GRC Financial Control solution orchestrates budgeting, revenue allocation, invoice planning, and reporting for consulting engagements. This document describes the platform from the **functional perspective**, including the standard (happy) paths and the validation/consolidation rules enforced during file imports and template generation. Each module references the correlated technical specification in [readme_specs.md](readme_specs.md).

---

## 1 · Budget & Allocation Management
**Happy Path**
1. A portfolio manager imports the budget workbook produced by Finance.
2. The importer validates the Excel headers (engagement, closing period, rank, budget and ETC metrics) and resolves the closing period from the report metadata.
3. Engagement totals are loaded into `EngagementRankBudgets` as the active snapshot and mirrored to `EngagementRankBudgetHistory`.
4. Engagement owners adjust incurred hours per rank/fiscal year; locked fiscal years become read-only while open ones remain editable.
5. The workspace recalculates consumed/remaining hours and applies the green/yellow/red traffic-light status per rank.

**Validation & Consolidation Rules**
- Header schema detection requires all mandatory groups (Engagement Id/Name, Closing Period, Budget/ETC metrics). Missing groups block the import with actionable errors.
- Metadata parsing ensures the workbook closing period is populated either from the header row or cell `A4`; the import stops if neither is found.
- Numeric fields accept localized (pt-BR) and invariant formats; parsing fails fast on invalid decimals/percentages to avoid silent truncation.
- During adjustments the system disallows changes on fiscal years flagged as locked and recalculates remaining hours with midpoint rounding to two decimals.
- Rank consolidation aggregates all rows for the same normalized rank, recalculates additional hours, and propagates the computed traffic-light status across the grouped rows.

[See Technical Spec →](readme_specs.md#budget-allocation-management)

---

## 2 · Hours Allocation Workspace
**Happy Path**
1. Users open the allocation workspace for a selected engagement.
2. The service loads fiscal years ordered by lock status, the latest budgets, and available rank options from the Rank Mapping catalog.
3. Users edit consumed hours per cell or add rank-level adjustments for open fiscal years.
4. Saving persists edits, recalculates remaining hours, and refreshes the snapshot view.

**Validation & Consolidation Rules**
- Every update verifies the target budget exists and belongs to the engagement before applying changes.
- Locked fiscal years throw a blocking error if edits or adjustments are attempted.
- Adjustments are aggregated by normalized rank name; the first row acts as the summary while subsequent rows reset additional hours to zero to avoid duplicate totals.
- Remaining hours derive from `BudgetHours + AdditionalHours − (Incurred + Consumed)` and are rounded to two decimals for consistency with reporting.

[See Technical Spec →](readme_specs.md#hours-allocation-workspace)

---

## 3 · Fiscal-Year Revenue Allocation
**Happy Path**
1. Finance imports the fiscal-year allocation sheet that includes the Current FY, Next FY, and backlog columns.
2. The importer maps each row to an engagement and fiscal year, calculating ToGo/ToDate balances.
3. Records are upserted into `EngagementFiscalYearRevenueAllocations`, keyed by Engagement + FiscalYear.
4. Dashboards immediately reflect updated backlog and revenue positions.

**Validation & Consolidation Rules**
- The importer enforces presence of Current FY and Next FY columns; missing fields stop the load.
- ToGo is derived from FYTG and Future FY backlog columns, while ToDate equals `OpeningValue − FYTG − FutureFY`; negative totals are preserved for audit.
- Rows without valid engagement matches are reported and skipped to avoid orphan allocations.

[See Technical Spec →](readme_specs.md#fiscal-year-revenue-allocation)

---

## 4 · Planned Allocation & Forecast Consolidation
**Happy Path**
1. Engagement leads plan future hours per closing period within the Allocation Planner UI.
2. The system loads current allocations alongside closing periods and their fiscal-year lock status.
3. Users edit allocations for open periods and submit the plan.
4. Persisted allocations drive downstream retain template generation and staffing dashboards.

**Validation & Consolidation Rules**
- `EngagementMutationGuard` ensures the engagement is mutable before any write operation.
- Fiscal years marked as locked cannot receive added, removed, or changed allocations. The save logic compares existing vs. incoming rows and blocks modifications on locked periods with clear error messages.
- Deletions and inserts execute only for unlocked closing periods; attempts against unknown closing periods surface an error instructing the user to refresh the data.

[See Technical Spec →](readme_specs.md#planned-allocation-and-forecast)

---

## 5 · Invoice Planner & Notifications
**Happy Path**
1. Financial controllers schedule invoice items against fiscal periods.
2. The nightly job `ev_FillMailOutbox_Daily` invokes stored procedure `sp_FillMailOutboxForDate`.
3. Due invoices produce email payloads in `MailOutbox`, which are later sent by the SMTP worker and tracked in `MailOutboxLog`.
4. Recipients receive reminders with invoice details and due dates.

**Validation & Consolidation Rules**
- Only invoices due on or before the execution date generate notifications, preventing premature reminders.
- Duplicate reminders are avoided by checking `MailOutboxLog` before inserting new entries for the same invoice and period.
- The planner enforces that schedules reference valid engagements and fiscal periods; inconsistent records are rejected during save.

[See Technical Spec →](readme_specs.md#invoice-planner-and-notifications)

---

## 6 · Excel Importers & Data Integrity
**Happy Path**
1. Users choose the desired importer (Full Management Data, Revenue Allocation, Invoice Plan).
2. The system loads the Excel workbook into memory, auto-detects header rows, and validates required columns for the selected importer.
3. Records are transformed into domain models and persisted via bulk operations.
4. Success and error summaries are displayed, highlighting skipped rows.

**Validation & Consolidation Rules**
- Header detection scans the first rows for mandatory header groups and fails fast with guidance when not found.
- Numeric parsing supports localized decimal separators and enforces midpoint rounding for currency/hours.
- The loader sanitizes whitespace, strips non-printable characters, and normalizes ranks/status text for consistent persistence.
- Import summaries include counts of processed rows, warnings (e.g., missing engagements), and accumulated totals to aid reconciliation.

[See Technical Spec →](readme_specs.md#excel-importers-and-validation)

---

## 7 · Retain Template Generation
**Happy Path**
1. Staffing teams request a Retain template based on the latest allocation planning workbook.
2. The generator clones the sanitized template asset (mirroring `DataTemplate/Retain_Template.xlsx`) next to the allocation file and resets the Saturday header row while preserving the workbook instructions.
3. Allocation entries are flattened by engagement/resource and spread across the weekly columns.
4. Sequential rows start at row 4, filling job code, resource GPN, resource name, and weekly hours in the exact columns expected by the Retain importer.
5. The populated Excel file is saved next to the source workbook and logged for traceability.

**Validation & Consolidation Rules**
- The generator validates the input path and ensures both the planning workbook and the embedded template asset exist before proceeding.
- It verifies the template contains the `Data Entry` sheet; absence results in a blocking error.
- Saturday header cells (row 1, columns `E` onward) are cleared before writing to avoid stale dates while the Monday formulas on row 2 remain intact.
- All data rows from row 4 onward are cleared prior to writing new values, and hours per week are rounded to two decimals (skipping zeros) to keep the template lean.

[See Technical Spec →](readme_specs.md#retain-template-generation)

---

## 8 · Reporting Dashboards
**Happy Path**
1. Users open the reporting dashboards within the Avalonia client.
2. KPI widgets query MySQL views for budgets, allocation by rank, and invoice status.
3. Data visualizations render in real time, reflecting the latest imports and planner updates.

**Validation & Consolidation Rules**
- Reporting relies on read-only MySQL views to guarantee consistent aggregation logic.
- EF projections fetch only the required columns and map them to view models, ensuring dashboards remain responsive.
- Cached lookups are refreshed when imports complete to prevent stale dimension data.

[See Technical Spec →](readme_specs.md#reporting-dashboards)

---

## 9 · UI Architecture
- Avalonia + MVVM pattern with one View ↔ one ViewModel.
- Commands leverage `RelayCommand`/`AsyncRelayCommand` to encapsulate interactions.
- Modal dialogs use a centralized overlay with an opaque background to maintain focus.
- Validation summaries surface inline errors from importers and planners to reduce back-and-forth.

[See Technical Spec →](readme_specs.md#ui-architecture)
