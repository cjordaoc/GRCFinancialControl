# GRC Financial Control – Functional Specification

## What changed
- 2025-09-21 00:45 UTC — Copied `README.md` into the WinForms build output so the Help dialog can display the packaged specification, and documented the Help workflow.
- 2025-09-21 00:15 UTC — Moved historical change tracking and error recovery guidance into dedicated `ChangeLog.md` and `Fixes.md` files and linked them from this specification. Review [`ChangeLog.md`](ChangeLog.md) for previous entries.

## Reference documents
- [`ChangeLog.md`](ChangeLog.md) — consolidated engineering and functional history.
- [`Fixes.md`](Fixes.md) — mistake catalog and error-handling guidance.

## Overview and Goals
GRC Financial Control is a Windows Forms desktop application used by finance and compliance analysts to upload operational workbooks into a centralized MySQL repository while maintaining selected reference data locally in SQLite. The application streamlines ingestion of Margin, ETC, Budget, and other financial datasets, applies deterministic validation/transformation rules, and persists curated facts for enterprise reporting.

Primary users:
- **Financial Controllers** — oversee upload accuracy and reconcile variances.
- **Project Accountants** — provide source spreadsheets for projects/programs.
- **Compliance Analysts** — audit load outcomes, ensuring controls remain effective.

## Information Flow
```
Source Files (.xlsx/.csv)
          |
          v
  File Dialog Helper (single or multi-select)
          |
          v
  Parser & Header Validator -- Culture-invariant conversions
          |
          v
   Domain Mapper (MeasurementPeriod, FiscalYear, Fact targets)
          |
          v
   Transactional Bulk Writer (per file)
          |
          +--> SQLite (local config / cached lookups)
          |
          +--> MySQL (centralized facts, logs)
          |
          v
   Status & Result Reporter (per-file summary)
```

## Feature Specification
### Upload Menu Structure
| Upload Type | File Selection | Expected Extensions | Notes |
|-------------|----------------|---------------------|-------|
| Margin      | Multi-file (`OpenFileDialog.Multiselect = true`) | `.xlsx` | Process files alphabetically; each file runs through `UploadRunner` for its own transaction and summary. |
| ETC         | Multi-file (`Multiselect = true`) | `.xlsx`, `.csv` | Deterministic iteration with schema validation and per-file commits. |
| Budget      | Multi-file (`Multiselect = true`) | `.xlsx` | Same per-file isolation; report inserted/updated counts through the summary grid. |
| All other upload submenus (e.g., Actuals, Forecast, Adjustments) | Single file (`Multiselect = false`) | `.xlsx` unless specified | Still validate headers, run a single `UploadRunner` work item, and summarize outcome. |

Dialog behavior:
- All submenus launch the shared `FileDialogService` helper to enforce filters, deterministic ordering, and optional exact filename checks.
- Multi-file uploads sort selected paths alphabetically before processing.
- Single-file uploads immediately hand the chosen file to the validation pipeline.

### Multi-file Upload Logic (Margin/ETC/Budget)
1. Collect selected files and order alphabetically.
2. For each file (coordinated by `UploadRunner`):
   - Instantiate a fresh `DbContext` and execute the load inside `Database.CreateExecutionStrategy().Execute(...)` so Pomelo’s retry pipeline governs the transaction while a separate context stays alive long enough to supply provider-specific retry state.
   - Run header/structure validation; abort and log if invalid.
   - Parse rows using culture-invariant numeric/date handling.
   - Map domain values (including `MeasurementPeriodId`).
   - Perform buffered insert/update operations.
   - Commit on success; rollback and log on failure, then continue to next file.
3. Present a consolidated summary (rows read, inserted, updated, warnings, errors) in the upload summary grid and status log.

### Single-file Upload Logic (other types)
- Execute the same validation/parsing/mapping steps with a single `UploadRunner` work item and per-file summary entry.

## Excel Parser Strategy Review (2025-09-20)

### Key Findings
1. **Worksheet targeting is brittle.** All Excel parsers call `ExcelWorksheetHelper.FirstVisible`, so they only inspect the first visible sheet. Files such as `EY_WEEKLY_PRICING_BUDGETING169202518718608_Excel Export.xlsx` and `EY_PERSON_ETC_LAST_TRANSFERRED_v1.xlsx` expose summary tabs (`PLAN INFO`, `ETC INFO`) before the required `RESOURCING` worksheet, while `data.xlsx` stores margin facts under `Export`. Because the parsers never reach those tabs, header detection fails even when the proper columns exist.
2. **Header detection cannot accommodate localized or punctuated labels.** `ExcelParsingUtilities.NormalizeHeader` strips spaces, underscores, and hyphens only. Headers like `Actuals: Hours Incurred (through last week)` or `Emp Resource  Name` therefore normalize to tokens with punctuation and double spaces that never match the synonym lists. Multi-row headers and repeated "Unnamed" cells in the EY exports further hide required fields because `ExcelHeaderDetector` demands every header in a single row.
3. **Plan uploads ignore the weekly allocation layout.** `PlanExcelParser` expects a single `HOURS` column plus an optional rate column. The harmonized dictionary shows weekly columns labelled by dates (e.g., `09/08/2025`, `16/08/2025`) with supporting metadata such as resource GPN, level, and engagement identifiers. The current parser discards that structure, making it impossible to aggregate planned hours per employee or engagement.
4. **ETC parsing misses required personnel details.** `EtcExcelParser` captures engagement, employee name, raw level, incurred hours, and ETC remaining, but it does not normalize levels, retain employee identifiers, or surface projected margin metrics that coexist on the resourcing sheet. The parser also assumes the headers live on a single row and ignores cases where localized names or punctuation appear.
5. **Margin ingestion relies on positional columns.** `MarginDataExcelParser` pulls engagement descriptors from column A and then reads columns D, E, and O by index. The raw data shows many additional margin metrics (`Margin % Bud`, `Margin % ETC-P`, `Billing Overrun`, etc.) whose order is not fixed. Without header-driven lookups, the parser silently drops most harmonized fields and risks breakage when spreadsheets add or shift columns.
6. **Additional feeder workbooks remain unsupported.** The raw field tables include `Programação Retain Platforms GRC (1).xlsx` and `20140812 - Programaçao ERP_v14.xlsx`, which supply Retain platform assignments and 40h debt forecasts. No parser currently consumes those patterns (boolean marks, weekly 40-hour flags), leaving the harmonized dictionary entries unfulfilled.

### Remediation Plan
- **Introduce worksheet selection rules per upload type.** Extend `ExcelWorksheetHelper` (or parser constructors) to accept ordered sheet name preferences (e.g., `RESOURCING`, `Export`, localized variants) and fall back to the first visible sheet only if none match. Include integration tests with EY weekly and ETC extracts to confirm the intended tab loads.
- **Broaden header normalization and synonym coverage.** Update `NormalizeHeader` to strip punctuation, collapse repeated whitespace, and optionally split on colons/slashes so `Actuals: Hours Incurred` matches existing synonyms. Expand `HeaderSchema` to register localized phrases (Portuguese labels, double-spaced names) and teach `ExcelHeaderDetector` to consolidate multi-row headers by scanning a bounded range and allowing partial matches for dynamic week/date columns.
- **Refactor plan parsing around dynamic weekly columns.** Detect identifier columns (engagement, customer, employee, level, location) separately from repeating week headers. Convert recognized date headers to `DateOnly`, aggregate hours per employee/week, and compute row totals. Preserve metadata like resource GPN for downstream joins.
- **Enhance ETC parsing for personnel and margin metadata.** Capture employee identifiers, normalize levels via the harmonized mapping, and retrieve additional columns such as projected margin percentage, ETC age, and status. Ensure numeric parsing tolerates blank/zero cells and align the output schema with database expectations.
- **Replace positional margin extraction with header-driven lookups.** Build a `HeaderSchema` for `data.xlsx`/`Export`, covering all margin and overrun columns. Parse decimal percentages robustly (tolerating `%` signs and localized separators) and normalize to decimal fractions. Surface engagement ID/name pairs consistently with the harmonized rules.
- **Plan dedicated parsers for Retain and 40h debt workbooks.** Model the boolean assignment markers and weekly 40-hour forecasts, reuse the improved header detection, and wire outputs into upload services that honor the SQLite/MySQL split.
- **Codify tests and documentation.** Add fixture-based unit tests per parser, update [`Fixes.md`](Fixes.md) with the resolved issues, and document the new behaviors in both this README and `agents.md` so future uploads remain aligned with the harmonized dictionary.

### Parser Enhancements Implemented (2025-09-20)
- **Engagement ID helper.** Centralized `(E-\d+)` extraction to reuse across plan, margin, and weekly declaration parsers while preserving surrounding labels for titles.
- **Worksheet targeting.** `ExcelWorksheetHelper.SelectWorksheet` prioritizes harmonized sheet names (`RESOURCING`, `Export`, `Planilha1`, `Alocações_Staff`, etc.) before falling back to the first visible tab, preventing summary sheets from short-circuiting uploads.
- **Header detection.** `ExcelHeaderDetector` aggregates multi-row captions, removes punctuation (including `%` → `PCT`), and matches localized synonyms so colon-delimited and Portuguese headers resolve to schema keys.
- **Plan parsing.** `PlanExcelParser` reads engagement metadata from the `PLAN INFO` sheet when row-level identifiers are absent, tolerates missing employee columns, detects dynamic weekly date columns (including multi-row headers), aggregates hours, records totals, skips numeric headers that fall outside the legal OLE Automation range, and normalizes level codes via `LevelNormalizer` while logging invalid totals.
- **ETC parsing.** `EtcExcelParser` title-cases employee names, captures employee identifiers, normalizes levels, and extracts projected margin %, ETC age, remaining weeks, and status with resilient numeric parsing.
- **Margin parsing.** `MarginDataExcelParser` reads the `Export` worksheet by header name, extracting client data, all harmonized margin percentages/values, overruns, status, and counts; percentages normalize to decimal fractions for fact loading with warnings on malformed cells. Engagement IDs are derived via the shared regex helper so any `(E-#######)` token is resolved even when not enclosed in parentheses.
- **Retain & ERP weekly parsing.** `WeeklyDeclarationExcelParser` detects Retain layouts (numeric weekly hours per employee) and ERP layouts (40h allocations flagged by engagement ID text), converts Retain’s Friday headers to ISO Mondays, and continues to support legacy tabular uploads for ad-hoc datasets.

### Master Data Forms
- **Engagements**: Maintain engagement ID, name, partner, manager, and opening margin. Edits occur in dedicated text boxes; the grid is read-only and selecting a row hydrates the editors. Validation enforces required fields and keeps opening margin within -100.000 to +100.000 before issuing synchronous insert/update/delete operations.
- **Measurement Periods**: Maintain description, start date, and end date with synchronous CRUD against MySQL. The read-only grid lists all periods, activation writes the selected `period_id` to SQLite (`parameters` table under `SelectedMeasurePeriod`), and deleting an active period prompts the operator to clear the local selection. Start/end validation prevents invalid ranges.
- **Fiscal Year**: Maintain fiscal year ID, description, start date, and end date. Enforce chronological consistency and guard against overlapping fiscal years.

All master-data grids refresh automatically after create/update/delete operations via shared `BindingSource` rebinding, and successful inserts surface a confirmation dialog so operators receive immediate feedback.

### Fact Tables & Measurement Period Linking
- Fact entities (Margin facts, ETC projections, Budget allocations, etc.) must persist a valid `MeasurementPeriodId` foreign key.
- Upload flows derive the `MeasurementPeriodId` from operator selection or file metadata and validate existence before writing.
- Failure to resolve a period aborts the file transaction with a clear message.

## Operational Guide
### Environment & Prerequisites
- .NET SDK 8.0.119 installed (verified via `dotnet --info`).
- Windows targeting build command: `dotnet build -p:EnableWindowsTargeting=true` at solution root.
- MySQL server reachable with credentials defined in environment configuration (e.g., `appsettings.Production.json` or secure secrets store).
- SQLite database file accessible for local configuration data.

### Running Locally
1. Ensure required SDK/runtime installed (see above).
2. Restore dependencies: `dotnet restore` (if needed).
3. Build solution: `dotnet build -p:EnableWindowsTargeting=true`.
4. Launch WinForms app via Visual Studio on Windows or `dotnet run -p GRCFinancialControl/GRCFinancialControl.csproj -p:EnableWindowsTargeting=true` when using a compatible environment.
5. Confirm connectivity to both SQLite and MySQL before attempting uploads.

### Help Menu Reference
- The **Help → View Help** command opens a tabbed dialog that shows runtime context plus the packaged `README.md`. The project copies the root `README.md` into the build output so operators always see the same specification the engineering team maintains. Update this document whenever functionality changes so the in-app help remains accurate.

### Upload Procedures
1. Navigate to **Upload** menu and choose the appropriate submenu.
2. Review the dialog filter (e.g., `.xlsx`, `.csv`) and select required files.
   - Margin/ETC/Budget: select multiple files (Shift/Ctrl) as needed.
   - Other uploads: select the single file requested.
3. Confirm measurement period/fiscal year context if prompted.
4. Monitor the upload summary grid and status log for per-file results (rows processed, inserts, updates, warnings, errors).
5. On completion, export or review the summary log for audit.

### Error Recovery & Logging
See [`Fixes.md`](Fixes.md) for detailed error recovery practices and the accumulated mistake catalog.

## Database Guide
- **SQLite**: Stores local application data (connection strings, cached master data snapshots, user preferences). Never push SQLite-only tables to MySQL.
- **MySQL**: Houses shared fact tables, master data canonical copies, and audit logs.
- Schema evolution:
  - Update EF Core models and generate migration scripts.
  - Place CREATE/ALTER SQL scripts in `DatabaseScripts/` with clear naming (e.g., `mysql/2025-09-18_AddMarginFact.sql`).
  - Run `DatabaseScripts/20250920_master_data_measurement_periods.sql` to create `measurement_periods`, align `dim_engagement`, and add `measurement_period_id` columns plus the lean `fact_engagement_margin` table.
  - Document schema diffs in both README and commit messages.
  - Composite index script `DatabaseScripts/20250919_add_fact_indexes.sql` adds OLTP indexes for ETC, plan, weekly declarations, and charges tables.

## Maintenance & Extension
### Adding a New Upload Type
1. **UI**: Add menu item and wire to handler using `FileDialogService` (choose single vs multi-file behavior).
2. **Parser**: Implement schema/header validation and culture-invariant parsing via `ExcelParserBase` derivatives.
3. **Mapper**: Convert raw rows to domain entities, resolving `MeasurementPeriodId` and related keys.
4. **Validator**: Enforce business rules (e.g., totals, mandatory columns, allowable ranges).
5. **Writer**: Submit an `UploadFileWork` to `UploadRunner` so each file runs in its own transaction with batched `SaveChanges`.
6. **Status Reporting**: Extend shared summary reporting to capture new metrics if needed.
7. **Documentation**: Update both `README.md` and `agents.md` (include `What changed` entry and reference the relevant updates in [`Fixes.md`](Fixes.md)).
8. **Testing**: Add smoke tests covering success and failure scenarios; update schema scripts if new tables/columns arise.

### Updating Documentation with Each Change
- Review functional impact, update relevant sections, and refresh the `What changed` log with timestamp.
- Note any new mistakes or lessons learned in [`Fixes.md`](Fixes.md).
- Confirm that operational steps and upload procedures remain accurate after modifications.

