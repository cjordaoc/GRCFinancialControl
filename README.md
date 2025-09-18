# GRC Financial Control – Functional Specification

## What changed
- 2025-09-18 19:55 UTC — Created baseline functional specification, documented upload behaviors, data flows, environment setup, and maintenance rules.

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
| Margin      | Multi-file (`OpenFileDialog.Multiselect = true`) | `.xlsx` | Process files alphabetically; per-file transaction and summary. |
| ETC         | Multi-file (`Multiselect = true`) | `.xlsx`, `.csv` | Support deterministic iteration with schema validation. |
| Budget      | Multi-file (`Multiselect = true`) | `.xlsx` | Same per-file isolation; report inserted/updated counts. |
| All other upload submenus (e.g., Actuals, Forecast, Adjustments) | Single file (`Multiselect = false`) | `.xlsx` unless specified | Still validate headers, run single transaction per file, and summarize outcome. |

Dialog behavior:
- All submenus launch an `OpenFileDialog` configured via shared helper.
- Multi-file uploads sort selected paths alphabetically before processing.
- Single-file uploads immediately hand the chosen file to the validation pipeline.

### Multi-file Upload Logic (Margin/ETC/Budget)
1. Collect selected files and order alphabetically.
2. For each file:
   - Instantiate a fresh `DbContext` and begin a transaction.
   - Run header/structure validation; abort and log if invalid.
   - Parse rows using culture-invariant numeric/date handling.
   - Map domain values (including `MeasurementPeriodId`).
   - Perform buffered insert/update operations.
   - Commit on success; rollback and log on failure, then continue to next file.
3. Present a consolidated summary (rows read, inserted, updated, warnings, errors) in the UI/log.

### Single-file Upload Logic (other types)
- Execute the same validation/parsing/mapping steps with a single transaction and per-file summary.

### Master Data Forms
- **Measurement Periods**: Maintain ID, description, start date, and end date. Validate contiguous/non-overlapping ranges before saving. Provide CRUD operations synced to MySQL while caching lookups locally.
- **Fiscal Year**: Maintain fiscal year ID, description, start date, and end date. Enforce chronological consistency and guard against overlapping fiscal years.

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

### Upload Procedures
1. Navigate to **Upload** menu and choose the appropriate submenu.
2. Review the dialog filter (e.g., `.xlsx`, `.csv`) and select required files.
   - Margin/ETC/Budget: select multiple files (Shift/Ctrl) as needed.
   - Other uploads: select the single file requested.
3. Confirm measurement period/fiscal year context if prompted.
4. Monitor progress UI for per-file status (rows processed, warnings, errors).
5. On completion, export or review the summary log for audit.

### Error Recovery & Logging
- Each file logs validation and load outcomes to MySQL (central log table) and local text log.
- Failed files remain isolated; correct source data and rerun only the affected files.
- Use Mistake Catalog in `agents.md` to record recurring issues and their mitigations.

## Database Guide
- **SQLite**: Stores local application data (connection strings, cached master data snapshots, user preferences). Never push SQLite-only tables to MySQL.
- **MySQL**: Houses shared fact tables, master data canonical copies, and audit logs.
- Schema evolution:
  - Update EF Core models and generate migration scripts.
  - Place CREATE/ALTER SQL scripts in `DatabaseScripts/` with clear naming (e.g., `mysql/2025-09-18_AddMarginFact.sql`).
  - Document schema diffs in both README and commit messages.

## Maintenance & Extension
### Adding a New Upload Type
1. **UI**: Add menu item and wire to handler using dialog helper (decide single vs multi-file behavior).
2. **Parser**: Implement schema/header validation and culture-invariant parsing.
3. **Mapper**: Convert raw rows to domain entities, resolving `MeasurementPeriodId` and related keys.
4. **Validator**: Enforce business rules (e.g., totals, mandatory columns, allowable ranges).
5. **Writer**: Use bulk writer helper with per-file transaction semantics.
6. **Status Reporting**: Extend shared summary reporting to capture new metrics if needed.
7. **Documentation**: Update both `README.md` and `agents.md` (include `What changed` entry and Mistake Catalog references if applicable).
8. **Testing**: Add smoke tests covering success and failure scenarios; update schema scripts if new tables/columns arise.

### Updating Documentation with Each Change
- Review functional impact, update relevant sections, and refresh the `What changed` log with timestamp.
- Note any new mistakes or lessons learned in the Mistake Catalog (`agents.md`).
- Confirm that operational steps and upload procedures remain accurate after modifications.

