# Technical Specification — GRC Financial Control (vNext)

This document details the implementation for every functional capability described in [README.md](README.md). Use it to locate the code paths, data stores, and validation rules that back each experience.

---

## Budget Allocation Management
- **Primary Services:** `FullManagementDataImporter`, `ImportService`, `HoursAllocationService`
- **Domain Models:** `Engagement`, `EngagementRankBudget`, `EngagementRankBudgetHistory`, `FiscalYear`, `TrafficLightStatus`
- **Persistence:** Tables `EngagementRankBudgets`, `EngagementRankBudgetHistory`; EF Core via `ApplicationDbContext`
- **Import Pipeline:**
  1. `ImportService` routes "Full Management Data" uploads to `FullManagementDataImporter`.
  2. The importer scans the worksheet to locate the header row (`HeaderGroups`) and resolves report metadata (`ExtractReportMetadata`).
  3. Required columns are validated; missing headers trigger `InvalidDataException` with friendly messages.
  4. Rows are parsed into engagement aggregates (hours, value, margin, expenses, ETC) with culture-aware decimal parsing.
  5. The service upserts rank budgets, records change history, and recalculates incurred/remaining totals via `EngagementRankBudget` domain methods.
- **Validation Mechanics:**
  - Header detection searches the first 12 rows and requires each mandatory header group.
  - Closing period metadata must be present either in the worksheet metadata or the first data row; otherwise import halts.
  - Numeric parsing supports pt-BR and invariant formats; invalid decimals/percentages raise descriptive errors.
  - Import result aggregates processed/skipped counts and exposes warnings for unresolved engagements.

---

## Hours Allocation Workspace
- **Primary Service:** `HoursAllocationService`
- **Domain Models:** `HoursAllocationSnapshot`, `HoursAllocationCellUpdate`, `HoursAllocationRowAdjustment`, `RankOption`
- **Persistence:** `EngagementRankBudgets`, `FiscalYears`, `RankMappings`
- **Workflow:**
  1. `GetAllocationAsync` loads engagement budgets with fiscal-year metadata and builds snapshot rows (`BuildRows`).
  2. `SaveAsync` validates requested updates and applies them to tracked budgets, recalculating consumed/remaining hours and traffic-light status through domain methods such as `UpdateConsumedHours`, `UpdateAdditionalHours`, and `CalculateIncurredHours`.
  3. Adjustment requests are grouped by normalized rank and persisted with midpoint rounding.
- **Validation Mechanics:**
  - Ensures the engagement exists and has budgets; missing records throw `InvalidOperationException`.
  - Fiscal years flagged `IsLocked` are guarded against edits.
  - Remaining hours follow the formula `Budget + Additional − (Incurred + Consumed)` with `Math.Round(..., MidpointRounding.AwayFromZero)`.

---

## Fiscal-Year Revenue Allocation
- **Primary Components:** `FullManagementDataImporter` (revenue section), `EngagementFiscalYearRevenueAllocation`
- **Persistence:** Table `EngagementFiscalYearRevenueAllocations`
- **Workflow:**
  1. Allocation worksheets are parsed to identify Current FY and Next FY columns.
  2. For each engagement, `ToGo` and `ToDate` values are computed based on FYTG and Future FY backlog fields.
  3. Records are upserted by composite key (EngagementId, FiscalYearId) using EF bulk operations.
- **Validation Mechanics:**
  - Missing FY columns or invalid numeric values trigger descriptive `InvalidDataException`s.
  - Rows with unknown engagements are logged in the import summary and excluded from persistence.

---

## Planned Allocation and Forecast
- **Primary Service:** `PlannedAllocationService`
- **Supporting Infrastructure:** `EngagementMutationGuard`, `ApplicationDbContext`
- **Domain Models:** `PlannedAllocation`, `ClosingPeriod`
- **Persistence:** Table `PlannedAllocations`
- **Workflow:**
  1. `GetAllocationsForEngagementAsync` retrieves existing allocations with associated closing periods/fiscal years.
  2. `SaveAllocationsForEngagementAsync` ensures the engagement is mutable, loads existing rows, and builds period lookups.
  3. Locked fiscal years are compared field-by-field; any attempt to add/remove/change allocations raises `InvalidOperationException` with guidance.
  4. Unlocked allocations are deleted and re-inserted with normalized rounding prior to `SaveChangesAsync`.
- **Validation Mechanics:**
  - Unknown closing periods result in blocking exceptions instructing the user to refresh the planner.
  - Locked fiscal years cannot be modified; diffs are evaluated before persistence.

---

## Invoice Planner and Notifications
- **Database Objects:** Tables `InvoicePlan`, `InvoiceItem`, `InvoiceEmission`, `MailOutbox`, `MailOutboxLog`; event `ev_FillMailOutbox_Daily`; procedure `sp_FillMailOutboxForDate`
- **Services:** `InvoicePlannerService` (planning), SMTP worker (`NotificationWorker`)
- **Workflow:**
  1. Invoice plans define schedule metadata per engagement/period.
  2. Daily event triggers stored procedure execution, which selects due invoice items and inserts messages into `MailOutbox`.
  3. Background worker dequeues `MailOutbox` entries, sends e-mails, and archives results in `MailOutboxLog`.
  4. The Invoice Planner's Add / Edit action loads the most recent plan for the selected engagement (or starts a new draft) and displays invoice lines in a flat grid with confirmed items locked for editing.
  5. The Confirm Request workspace keeps plans and line editing on a single screen: `RequestConfirmationViewModel` loads plan summaries, surfaces invoice lines in a read-only grid, and binds the inline form to `SelectedLine`. `SavePlanDetailsCommand` calls `MarkItemsAsRequested`, while `ReverseSelectedLineCommand` invokes `UndoRequest` to reset the invoice back to Planned.
  6. The Confirm Emission workspace keeps the full workflow inline: controllers load the plan, select a requested line, and provide BZ code plus emission date before invoking `CloseItems`, which persists the new `InvoiceEmission` entry and flips the status to Emitted. `CancelEmissions` captures a cancel reason from the same editor, marks the latest emission as canceled, and returns the invoice to Planned without deleting history.
  7. Controllers can delete the active plan from the editor dialog, removing the header and all invoice items and refreshing the engagement list for a clean restart.
- **Invoice Line Grid Rules:**
  - Plan type **By Date** auto-generates emission dates from the first emission date plus payment term days; the delivery column is hidden. Plan type **By Milestone** leaves emission dates and delivery descriptions fully manual.
  - Emitted/requested/canceled items remain visible but read-only; only planned items can be recalculated or removed when the invoice count changes (at least one editable line is always present).
  - Changing the **# Invoices** field enforces that the grid shows exactly that number of editable rows in addition to any emitted items already stored for the engagement.
  - Editing the percentage or amount on a planned line recalculates the companion value and redistributes the remaining editable lines evenly so that total percentage stays at 100% and total amount matches the engagement total. Rounding differences flow into the last editable line.
  - Monetary inputs and totals display the engagement currency symbol; percentages show two decimals plus the `%` suffix. Emission dates use short-date formatting.
  - Totals highlight in red and the Save action is disabled whenever the aggregated percentage or amount diverges from the required targets (100.00% / engagement total).
- **Validation Mechanics:**
  - Stored procedure filters by due date and checks `MailOutboxLog` to prevent duplicates.
  - Emission confirmation requires a non-empty BZ code and emission date before closing, and cancellation requests must supply a reason before the invoice reopens.
  - Planner services validate references to engagements and closing periods prior to saving schedules.

---

## Excel Importers and Validation
- **Shared Infrastructure:** `ImportService`, `FullManagementDataImporter`, `SimplifiedStaffAllocationParser`, `ImportSummaryFormatter`, `WorksheetValueHelper`
- **Libraries:** ExcelDataReader, ClosedXML (for template interactions)
- **Workflow:**
  1. Uploads are routed to the appropriate importer based on action context.
  2. Header discovery uses predefined header groups per importer; helper functions normalize whitespace and accentuation.
  3. Values are parsed with culture-aware numeric/date parsing helpers (`ParseDecimal`, `ParsePercent`, `ParseDate`).
  4. Summaries collate totals, warnings, and success counts for UI feedback.
- **Validation Mechanics:**
  - Missing required headers or malformed workbooks raise `InvalidDataException` with user-centric instructions (e.g., "clear filters").
  - Decimal parsing sanitizes numeric strings, strips thousand separators, and enforces rounding precision.
  - Status/rank columns are normalized via `NormalizeWhitespace` to maintain consistent storage.

---

## Retain Template Generation
- **Primary Components:** `RetainTemplateGenerator`, `RetainTemplatePlanningWorkbook`, `RetainTemplatePlanningSnapshot`
- **Libraries:** ClosedXML for Excel manipulation
- **Workflow:**
  1. `GenerateRetainTemplateAsync` validates both the allocation workbook path and the caller-provided destination, then loads the sanitized template asset that mirrors `DataTemplate/Retain_Template.xlsx`.
  2. `RetainTemplatePlanningWorkbook.Load` reads the planning sheet, producing a `RetainTemplatePlanningSnapshot` with entries grouped by resource/job and reference week.
  3. Saturday headers are built from the reference date to cover all detected weeks and written into row 1 while the Monday formulas on row 2 remain untouched.
  4. `PopulateTemplate` clears data rows from row 4 onward, writes job code/resource identifiers plus weekly hours, and saves the workbook copy to the requested destination.
- **Validation Mechanics:**
  - Template asset presence and format are validated; invalid Base64 raises `InvalidDataException`.
  - Missing `Data Entry` worksheet halts processing with a clear error.
  - Saturday header cells (row 1) are cleared before writing to prevent stale dates.
  - Hours per week are rounded to two decimals, zero values omitted, and data rows reset from row 4 onward to keep the template compact.

---

## Reporting Dashboards
- **UI Layer:** Avalonia dashboards leveraging MVVM view models
- **Data Layer:** MySQL views surfaced through EF Core projections
- **Workflow:**
  1. Dashboards request KPI data via repository/services that query read-only views.
  2. Data is projected into lightweight DTOs for visualization and cached when appropriate.
- **Validation Mechanics:**
  - Views encapsulate aggregation rules to ensure consistent metrics across the application.
  - Services refresh caches after imports complete to avoid stale dimension data.

---

## UI Architecture
- **Pattern:** MVVM enforced across Avalonia projects (`App.Presentation`, `GRCFinancialControl.Avalonia`)
- **Key Components:** View models leverage `RelayCommand`/`AsyncRelayCommand`; modal interactions flow through a centralized ModalService.
- **Guidelines:**
  - Views contain declarative layout only; logic belongs in view models or services.
  - Modal overlays use opaque backgrounds to focus attention.
  - Validation feedback is surfaced via bound properties fed by importer/planner summaries.


---

## Master Data Editors
- **Primary Components:** `CustomerEditorView`, `CustomerEditorViewModel`, `DialogEditorViewModel`
- **Validation Mechanics:**
  - `ViewModelBase` now derives from `ObservableValidator`, enabling dialog editors to participate in `INotifyDataErrorInfo` workflows.
  - `CustomerEditorViewModel` annotates Name and Customer Code with `[Required]` attributes and exposes `NameError`/`CustomerCodeError` accessors for inline messaging.
  - `DialogEditorViewModel` listens to `ErrorsChanged` and disables the Save command while any validation errors remain, ensuring only complete records are persisted.
