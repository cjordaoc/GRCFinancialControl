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
  4. Rows are parsed into engagement aggregates (Original Budget Hours/TER/Margin/Expenses, Mercury projections, Unbilled Revenue Days) with culture-aware decimal parsing.
  5. The service upserts rank budgets, records change history, and recalculates incurred/remaining totals via `EngagementRankBudget` domain methods.
- **Validation Mechanics:**
  - Header detection searches the first 12 rows and requires each mandatory header group.
  - Closing period metadata must be present either in the worksheet metadata or the first data row; missing metadata raises an `ImportWarningException` that the UI surfaces as a warning before aborting the import.
  - Numeric parsing supports pt-BR and invariant formats; invalid decimals/percentages raise descriptive errors.
  - Only existing engagements are updated; unknown IDs are skipped with "Engagement not found" warnings in the import summary.
  - Import result aggregates processed/skipped counts and exposes warnings for unresolved engagements.
  - Rows that reach the importer without a closing period still update opening budget columns but skip ETC metrics; these rows are listed in the `MissingClosingPeriodSkips` summary collection.
  - `ImportViewModel` keeps the Full Management Data import disabled until `HasClosingPeriodSelected` resolves `true`, which only happens after `ApplicationParametersChangedMessage` persists a closing period from the Home dashboard.

---

## Hours Allocation Workspace
- **Primary Service:** `HoursAllocationService`
- **Domain Models:** `HoursAllocationSnapshot`, `HoursAllocationCellUpdate`, `HoursAllocationRowAdjustment`, `RankOption`
- **Persistence:** `EngagementRankBudgets`, `FiscalYears`, `RankMappings`
- **Workflow:**
  1. `GetAllocationAsync` loads engagement budgets with fiscal-year metadata and builds snapshot rows (`BuildRows`).
  2. `SaveAsync` validates requested updates and applies them to tracked budgets, recalculating consumed/remaining hours and traffic-light status through domain methods such as `UpdateConsumedHours`, `UpdateAdditionalHours`, and `CalculateIncurredHours`.
  3. Adjustment requests are grouped by normalized rank and persisted with midpoint rounding.
  4. `AllocationEditorViewModel` resolves each row's display currency via `CurrencyDisplayHelper`, showing target/current/variance amounts with the engagement currency or the default fallback configured in Settings.
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
  2. `SaveAllocationsForEngagementAsync` ensures the engagement is mutable (not manual-only and not Closed), loads existing rows, and builds period lookups.
  3. Locked fiscal years are compared field-by-field; any attempt to add/remove/change allocations raises `InvalidOperationException` with guidance.
  4. Unlocked allocations are deleted and re-inserted with normalized rounding prior to `SaveChangesAsync`.
- **Validation Mechanics:**
  - Unknown closing periods result in blocking exceptions instructing the user to refresh the planner.
  - Locked fiscal years cannot be modified; diffs are evaluated before persistence.

---

## Closing Period Management
- **Primary Components:** `ClosingPeriodService`, `ClosingPeriodsViewModel`, `ClosingPeriodEditorViewModel`
- **Domain Models:** `ClosingPeriod`, `FiscalYear`
- **Persistence:** Table `ClosingPeriods` (column `IsLocked` defaults to `0`)
- **Workflow:**
  1. `ClosingPeriodService.GetAllAsync` returns periods ordered by `PeriodStart` with fiscal-year navigation properties so the view model can perform in-memory filtering.
  2. `ClosingPeriodsViewModel.LoadDataAsync` caches all periods in `_allClosingPeriods`, loads fiscal years, and builds filter options through `UpdateFiscalYearFilters`, adding a localized **All fiscal years** entry and selecting persisted preferences when present.
  3. `ApplyFiscalYearFilter` performs client-side filtering, repopulating the observable collection and preserving the previously selected period when possible.
  4. Lock/unlock requests flow through `ToggleLockAsync`, which invokes `ClosingPeriodService.SetLockStateAsync` to toggle `IsLocked`, refreshes the filtered collections, and broadcasts `RefreshDataMessage` so dependent view models reload their cached lists.
  5. Editor dialogs (`ClosingPeriodEditorViewModel.SaveAsync`) persist changes via `IClosingPeriodService` and emit standardized toast feedback (`ClosingPeriods.Toast.SaveSuccess` or `ClosingPeriods.Toast.OperationFailed`).
- **UI Feedback:**
  - Save, Delete, and Delete Data commands share localized toast keys (`*.Toast.SaveSuccess`, `*.Toast.DeleteSuccess`, `*.Toast.ReverseSuccess`, `*.Toast.OperationFailed`) while Lock/Unlock retains dedicated lock-state messages.
  - The filter ComboBox binds to `FiscalYearFilters` with `DisplayName` values, while the lock button label reflects `SelectedClosingPeriod.IsLocked` using localized `ClosingPeriods.Button.Lock`/`Unlock` strings.

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
  - Monetary inputs and totals display the engagement currency symbol, falling back to the default currency configured in Settings when an engagement lacks a code; percentages show two decimals plus the `%` suffix. Emission dates use short-date formatting.
  - Totals highlight in red and the Save action is disabled whenever the aggregated percentage or amount diverges from the required targets (100.00% / engagement total).
- **Validation Mechanics:**
  - Stored procedure filters by due date and checks `MailOutboxLog` to prevent duplicates.
  - Emission confirmation requires a non-empty BZ code and emission date before closing, and cancellation requests must supply a reason before the invoice reopens.
  - Planner services validate references to engagements and closing periods prior to saving schedules.

- **UI Feedback:**
  - `PlanEditorViewModel.SavePlan` and `DeletePlan` emit success/warning/error toasts (`InvoicePlan.Toast.PlanSaved`, `InvoicePlan.Toast.PlanDeleted`, etc.) via `ToastService` alongside detailed status messages.
  - `RequestConfirmationViewModel` surfaces toasts (`Request.Toast.*`) when inserting or reversing invoice requests, keeping inline actions responsive without modal dialogs.

---

## Power Automate Tasks Export
- **Primary Component:** `TasksViewModel`
- **Dependencies:** `IDbContextFactory<ApplicationDbContext>`, view entity `InvoiceNotificationPreview`, tables `InvoiceItem`, `InvoicePlan`, `Engagements`, `EngagementRankBudgets`, `FiscalYears`.
- **Workflow:**
  1. `GenerateTasksFileAsync` computes the next Monday at 10:00 in the `America/Sao_Paulo` timezone; this date becomes the notification pivot for both invoices and ETCs and drives the file name `WeeklyTasks_YYYYMMDD.xml`.
  2. `LoadInvoiceEntriesAsync` filters `InvoiceNotificationPreviews` by the pivot date, resolves planner items/plans to surface CNPJ, payment type code/name, PO/FRS, share number/total, engagement totals, delivery names (when the plan type is `ByDelivery`), COE notes, customer contact emails, and manager/senior-manager assignments.
  3. `BuildMessageBuckets` groups recipients by email and role (Manager/Senior Manager), deduplicates contacts, and writes `<Message>` nodes that contain `<Recipients>`, `<Body>` text (with counts), `<Invoices>` (each carrying the computed “Texto de Faturamento” description) and optional `<ETCs>` collections.
  4. `LoadEtcEntriesAsync` loads active engagements whose `ProposedNextEtcDate` is on or before the pivot, skips those without manager/senior-manager assignments, and projects rank budgets into dynamic fiscal-year column sets (consumed/remaining) per engagement.
- **Validation Mechanics:**
  - Missing timezone data surfaces `Tasks.Status.TimeZoneMissing`; other failures bubble to `Tasks.Status.GenerationFailure` so the UI reports the issue.
  - Payment types are constrained to `PaymentTypeCatalog` (`TRANSFERENCIA_BANCARIA`, `BOLETOS`) and default to transfer if the database does not specify a code.
  - Email lists are trimmed and deduplicated prior to serialization so Power Automate connectors receive valid CDATA strings and XPath selectors.
- Attachments are no longer emitted; Power BI/Power Automate consume the structured XML payload to build per-recipient tables at runtime.
- Engagements without manager or senior-manager assignments are ignored, and fiscal-year nodes with zero incurred/remaining hours are filtered before serialization so downstream flows receive only actionable data.

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
- Importers call `EngagementImportSkipEvaluator` to ignore rows targeting S/4 Project engagements or engagements with status `Closed`, emitting the warnings `⚠ Values for S/4 Projects must be inserted manually. Data import was skipped for Engagement {EngagementID}.` and `⚠ Engagement {EngagementID} skipped – status is Closed.`; the S/4 pathway now only refreshes metadata (description + customer link), creating missing customers and replacing placeholder codes when the workbook finally includes the official identifier. Whenever at least one S/4 engagement changes, `ImportViewModel` triggers `ToastService.ShowSuccess("Import.Toast.S4MetadataSuccess")` so users receive immediate confirmation.
- `ImportViewModel` listens for `ApplicationParametersChangedMessage` to toggle `HasClosingPeriodSelected`, keeping the Full Management import command disabled until a closing period is selected on the Home dashboard.

---

## Retain Template Generation
- **Primary Components:** `RetainTemplateGenerator`, `RetainTemplatePlanningWorkbook`, `RetainTemplatePlanningSnapshot`
- **Libraries:** ClosedXML for Excel manipulation
- **Workflow:**
  1. `GenerateRetainTemplateAsync` validates both the allocation workbook path and the caller-provided destination, then loads the sanitized template asset that mirrors `DataTemplate/Retain_Template.xlsx`.
  2. `RetainTemplatePlanningWorkbook.Load` reads the planning sheet, producing a `RetainTemplatePlanningSnapshot` with entries grouped by resource/job and reference week.
  3. Saturday headers are derived by taking the Saturday preceding each detected planning week; only cell `E1` is populated so that built-in formulas propagate the remaining header values.
  4. `PopulateTemplate` clears data rows from row 4 onward, writes job code/resource identifiers plus weekly hours, and saves the workbook copy to the requested destination.
- **Validation Mechanics:**
  - Template asset presence and format are validated; invalid Base64 raises `InvalidDataException`.
  - Missing `Data Entry` worksheet halts processing with a clear error.
  - Hours post to the Saturday that immediately precedes each allocation week (e.g., 03/11/2025 maps to 01/11/2025), with only `E1` being overwritten.
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

## Home Dashboard
- **Primary Components:** `HomeViewModel`, `HomeView`
- **Workflow:**
  1. `HomeViewModel.LoadDataAsync` retrieves fiscal years and closing periods, orders them chronologically, and stores `_allClosingPeriods` so filtering is performed client-side.
  2. Default selections load from `ISettingsService` (`GetDefaultFiscalYearIdAsync`/`GetDefaultClosingPeriodIdAsync`); lacking persisted values, the first available fiscal year/period is preselected.
  3. `UpdateClosingPeriodsForSelectedFiscalYear` rebuilds the closing-period list whenever a fiscal year changes, while `ConfirmSelectionAsync` persists the defaults and sends `ApplicationParametersChangedMessage` to refresh dependent modules.
  4. `EnsureReadmeLoadedAsync` executes once per session (`_readmeLoaded` flag), reads the embedded `README.md` resource through `GetManifestResourceStream`, and populates `ReadmeContent` (falling back to `Home.Markdown.LoadFailed` when the file cannot be read).
- **UI Details:**
  - `HomeView.axaml` places the fiscal-year ComboBox and Confirm button in a three-column grid alongside an indeterminate `ProgressBar` so alignment stays horizontal on wide displays.
  - README content renders inside `MarkdownScrollViewer` hosted in a rounded `Border` positioned immediately below the fiscal-year selection stack, capped at `MaxHeight="420"` with auto vertical scrolling to keep onboarding documentation accessible without leaving the dashboard.

---

## Settings and Application Data Backup
- **Primary Components:** `ApplicationDataBackupService`, `IApplicationDataBackupService`, `SettingsViewModel`
- **Dependencies:** `IDbContextFactory<ApplicationDbContext>`, `ISettingsService`, `FilePickerService`, `DialogService`
- **Workflow:**
  1. `SettingsViewModel` loads persisted preferences (language, environment, and `SettingKeys.DefaultCurrency`), applying `CurrencyDisplayHelper.SetDefaultCurrency` during initialization so Avalonia layers render consistent currency symbols.
  2. Users adjust the Default Currency selector, which persists the new value, updates the settings service, and immediately invokes `CurrencyDisplayHelper.SetDefaultCurrency` so both desktop apps share the fallback symbol without requiring a restart.
  3. Export requests call `ApplicationDataBackupService.ExportAsync`, which opens a database connection, enumerates tables from the EF model, and writes an XML document with column metadata and serialized values.
  4. Import requests prompt the user for confirmation and delegate to `ImportAsync`, which parses the XML, disables foreign-key checks, deletes existing rows, inserts the payload with typed parameters, and re-enables constraints within a transaction.
  5. After a connection package import, data restore, or language change, `SettingsViewModel` sends `ApplicationRestartRequestedMessage`; both main window view models persist their last navigation key (`SettingKeys.LastGrcNavigationItemKey`/`SettingKeys.LastInvoicePlannerSectionKey`) so the restarted app returns to the previously selected workspace with the saved language.
  6. `ClearAllDataAsync` deletes transactional tables only (`ActualsEntries`, `PlannedAllocations`, `EngagementFiscalYearRevenueAllocations`, `EngagementRankBudgets`, `FinancialEvolution`, `Exceptions`) and skips master-data/team tables after verifying their existence in `INFORMATION_SCHEMA`.
- **Validation Mechanics:**
  - Both export/import paths fall back to building a context from saved settings if the DI factory lacks a configured provider; missing credentials raise `InvalidOperationException` with guidance.
  - XML serialization captures type information (including binary data) using invariant formatting to ensure round-trippable values on restore.
  - Imports roll back on failure, guaranteeing foreign-key checks are re-enabled even when errors occur.
- **UI Feedback:**
  - `SaveSettingsAsync`, `SaveLocalizationSettingsAsync`, and `SavePowerBiSettingsAsync` persist updated dictionaries asynchronously and call `ToastService.ShowSuccess`/`ShowError` with `Settings.Toast.*` keys while keeping the tab content active (no forced reloads).

---

## UI Architecture
- **Pattern:** MVVM enforced across Avalonia projects (`App.Presentation`, `GRCFinancialControl.Avalonia`)
- **Key Components:** View models leverage `RelayCommand`/`AsyncRelayCommand`; modal interactions flow through a centralized ModalService.
- **Guidelines:**
- Views contain declarative layout only; logic belongs in view models or services.
- Modal overlays use opaque backgrounds to focus attention.
- Validation feedback is surfaced via bound properties fed by importer/planner summaries.
- Dialog editors expose an `IsReadOnlyMode` flag; `DialogEditorViewModel` and specialized editors (closing periods, fiscal years, allocations) bind text inputs via `IsReadOnly` and disable interactive controls when the View command opens them.
- Numeric editors opt into `App.Presentation.Behaviors.NumericInputNullSafety`; the attached handler listens for focus loss and rewrites empty values to `0` (or `0.00` via binding formats) so Avalonia never raises binding exceptions when users clear numeric fields.
- `EngagementEditorViewModel` evaluates the selected status text and, when it resolves to `Closed` for persisted records, forces the editor into read-only mode (papd/manager sections, source selector, and financial evolution grid) while leaving the status ComboBox and Save command enabled so users can intentionally reopen the engagement.
- `EngagementEditorView` renders the dialog as a tabbed layout (Engagement Data, Financial Data, Financial Evolution, Assignments) so controllers can jump between detail categories without scrolling long forms.
- `App.Presentation.Services.ToastService` exposes a shared observable toast queue consumed by both desktop shells; view models call `ShowSuccess`/`ShowWarning`/`ShowError` with localized resource keys so button actions surface consistent success/warning/error banners that auto-dismiss after three seconds.


---

## Master Data Editors
- **Primary Components:** `CustomerEditorView`, `CustomerEditorViewModel`, `DialogEditorViewModel`
- **Validation Mechanics:**
  - `ViewModelBase` now derives from `ObservableValidator`, enabling dialog editors to participate in `INotifyDataErrorInfo` workflows.
  - `CustomerEditorViewModel` annotates Name and Customer Code with `[Required]` attributes and exposes `NameError`/`CustomerCodeError` accessors for inline messaging.
  - `DialogEditorViewModel` listens to `ErrorsChanged` and disables the Save command while any validation errors remain, ensuring only complete records are persisted.
  - `CustomersViewModel`, `ManagersViewModel`, `PapdViewModel`, `ClosingPeriodsViewModel`, `FiscalYearsViewModel`, `EngagementsViewModel`, `ManagerAssignmentsViewModel`, and `PapdAssignmentsViewModel` pass `isReadOnlyMode: true` when executing the View command so their dialogs render in read-only state (text boxes bind `IsReadOnlyMode`, combo/date pickers use `AllowEditing`).

---

## Global Compliance & Verification
- **Startup Logging:** `GRCFinancialControl.Avalonia.App` and `InvoicePlanner.Avalonia.App` resolve `ILogger<App>` after building their service providers and log the restored language, default currency, and whether connection settings were detected. Restart attempts log the executable plus argument payload before relaunching, helping support teams confirm that language/currency persistence and environment selection survived the restart trigger.
- **Restart Messaging:** Both desktop shells emit informational log entries whenever `ApplicationRestartRequestedMessage` is processed so telemetry captures which operations (language change, package import, data restore) required a relaunch.
- **Import Safeguards:** `EngagementImportSkipEvaluator` continues to emit structured metadata used by `ImportService`/`FullManagementDataImporter`; warning messages are added to the per-import summaries and written to the central logger so audit trails show each skipped S/4 Project or Closed engagement.
- **Read-Only Enforcement:** Dialog view models refresh editing flags when `IsReadOnlyMode` or the engagement status changes, keeping Closed engagements locked while still allowing status updates and ensuring View-mode dialogs remain non-editable until reopened via Add/Edit flows.
- **Currency & Numeric Stability:** `CurrencyDisplayHelper` centralizes formatting (symbol resolution, localized separators, fixed decimal places) while `NumericInputNullSafety` attaches to numeric controls in both apps to rewrite blank values to zero on focus loss, preventing Avalonia parse exceptions and ensuring totals remain accurate.
- **Assignment Parity:** Manager and PAPD assignment tabs reuse the same anchoring logic (Add/Edit/View/Delete pipelines); the manager workspace now mirrors PAPD behavior by anchoring on the selected manager before editing engagement links, keeping validation and persistence paths aligned.
