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
- The planner's Add / Edit action opens the latest plan for the selected engagement (or starts a new one) and renders invoice lines in a tabular editor where confirmed items remain visible but read-only.
- The plan editor dialog now includes a Delete Plan action that removes the current plan (header and all invoice lines) and refreshes the engagement list so controllers can immediately start a new schedule.
- Adjusting the **# Invoices** field keeps exactly that many editable rows in the grid while preserving previously emitted lines alongside them.
- Invoice lines auto-generate emission dates from the first emission date and payment terms, rebalance editable percentages/amounts to keep totals at 100% of the engagement value, and display all monetary fields with the engagement currency symbol.
- Totals are continuously recalculated; mismatches highlight in red and block the Save action until both total percentage (100.00%) and total amount (engagement total) are satisfied.
- The **Confirm Request** workspace keeps the process inline: after choosing **Insert Request Data** the plan invoices appear beneath the selector with a detail form that lets controllers enter RITM, COE responsible, and request date. **Save** marks the current invoice as Requested, while **Reverse** clears the fields and returns the line to Planned without leaving the screen.
- The **Confirm Emission** workspace mirrors the request flow: controllers load a plan, pick a requested invoice, and capture the BZ code plus the actual emission date directly in the inline editor. Saving calls `CloseItems` to persist both the status change and a new `InvoiceEmission` record. Emitted invoices can be canceled from the same panel by supplying a reason, which records the cancellation and reopens the line as Planned without losing the emission history.

**Validation & Consolidation Rules**
- Only invoices due on or before the execution date generate notifications, preventing premature reminders.
- Duplicate reminders are avoided by checking `MailOutboxLog` before inserting new entries for the same invoice and period.
- Emission confirmation enforces that BZ code and emission date are provided before closing, and cancellations require a reason before the invoice is returned to the Planned state.
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
- The Full Management Data importer now owns budget/margin/projection updates, mapping Original Budget, Mercury projections, and the new Unbilled Revenue Days column onto existing engagements while logging "Engagement not found" when an ID is absent.

[See Technical Spec →](readme_specs.md#excel-importers-and-validation)

---

## 7 · Retain Template Generation
**Happy Path**
1. Staffing teams request a Retain template based on the latest allocation planning workbook.
2. The generator clones the sanitized template asset (mirroring `DataTemplate/Retain_Template.xlsx`) into the user-selected destination and seeds cell `E1` with the Saturday that precedes the first planned week while preserving the workbook instructions.
3. Allocation entries are flattened by engagement/resource and spread across the weekly columns.
4. Sequential rows start at row 4, filling job code, resource GPN, resource name, and weekly hours in the exact columns expected by the Retain importer.
5. The populated Excel file is saved in the chosen location and logged for traceability.

**Validation & Consolidation Rules**
- The generator validates the input path and ensures both the planning workbook and the embedded template asset exist before proceeding.
- It verifies the template contains the `Data Entry` sheet; absence results in a blocking error.
- Weekly columns align to the Saturday immediately preceding each allocation week, so hours for 03/11/2025 post to the `01/11/2025` column.
- Only cell `E1` is overwritten with the computed Saturday while downstream header formulas remain intact.
- All data rows from row 4 onward are cleared prior to writing new values, and hours per week are rounded to two decimals (skipping zeros) to keep the template lean.

[See Technical Spec →](readme_specs.md#retain-template-generation)

---

## 8 · Power Automate Tasks Export
**Happy Path**
1. Controllers choose **Generate Tasks File** in the Tasks workspace and select the destination XML file (the dialog defaults to the last-used folder).
2. The exporter resolves the upcoming Monday at 10:00 (America/Sao_Paulo) and queries invoice plans that notify on that date.
3. A `WeeklyTasks_YYYYMMDD.xml` document (schema version 1.3) is produced with `<Meta>` timestamps, one `<Message>` per recipient audience (Manager, Senior Manager, or mixed), and deduplicated `<Recipient>` entries keyed by email/role.
4. Each `<Invoice>` node now carries the normalized payment type (`TRANSFERENCIA_BANCARIA`/`BOLETOS`), the runtime-generated billing description that mirrors the Excel “Texto de Faturamento” pattern, optional PO/FRS/Ticket lines, and the customer email list ready for Power Automate.
5. `<ETC>` sections enumerate dynamic fiscal-year columns for consumed/remaining hours per rank, expanding to any number of fiscal years required while skipping engagements without manager assignments.

**Validation & Consolidation Rules**
- The exporter reads directly from the live database; connection issues or missing timezone information surface localized status messages.
- Payment types are constrained to the catalog defined in `Invoices.Core.Payments.PaymentTypeCatalog` and surfaced through the planner ComboBox before export.
- Invoice and ETC recipients are deduplicated and trimmed before serialization so Power Automate flows receive clean XPath targets and HTML-ready tables.
- Engagements without manager or senior manager assignments are skipped, and fiscal-year nodes with zero incurred and remaining hours are filtered to keep the XML lean for downstream automation.

[See Technical Spec →](readme_specs.md#power-automate-tasks-export)

---

## 9 · Reporting Dashboards
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

## 10 · UI Architecture
- Avalonia + MVVM pattern with one View ↔ one ViewModel.
- Commands leverage `RelayCommand`/`AsyncRelayCommand` to encapsulate interactions.
- Modal dialogs use a centralized overlay with an opaque background to maintain focus.
- Validation summaries surface inline errors from importers and planners to reduce back-and-forth.

[See Technical Spec →](readme_specs.md#ui-architecture)

---

## 11 · Master Data Editors
**Happy Path**
1. Administrators open the master data workspace to maintain supporting catalog entries.
2. Customer records expose a dialog editor with inline validation cues for required fields.
3. Once all mandatory fields pass validation, the Save action becomes available and persists changes through the shared dialog pipeline.

**Validation & Consolidation Rules**
- Customer Name and Customer Code must both contain non-whitespace values; invalid entries surface inline error text with the shared `StatusError` style.
- The dialog Save button remains disabled until all validation errors are cleared, preventing incomplete records from being persisted.

[See Technical Spec →](readme_specs.md#master-data-editors)
