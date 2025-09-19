# GRC Financial Control – Engineering Guidelines

## What changed
- 2025-09-20 23:30 UTC — Added OLE Automation range guards to Excel date parsing and ensured UploadRunner keeps the execution-strategy context alive to stop disposed `MySqlDbContext` failures during ETC/Margin uploads.
- 2025-09-20 21:55 UTC — Added engagement ID extraction helpers, aligned plan and weekly declaration parsers with EY workbook layouts, and wrapped UploadRunner in EF execution strategy retries.
- 2025-09-20 19:55 UTC — Rolled out worksheet selection preferences, resilient header detection, weekly plan aggregation, ETC enrichment, and margin schema coverage across the Excel parsers.
- 2025-09-20 18:40 UTC — Documented the Excel parser strategy review plus the expectation to deliver combined findings and remediation plans in a single response to cut back on iteration loops.
- 2025-09-19 14:09 UTC — Restored the upload summary grid and week-ending picker in Form1 so runtime references match the designer and builds succeed again on non-Windows hosts.
- 2025-09-19 13:57 UTC — Clarified master-data binding practices for refresh reliability, added insert confirmation messaging expectations, and recorded the fix in the Mistake Catalog.
- 2025-09-19 12:58 UTC — Documented MySQL alignment script updates for measurement periods, fact tables, and engagement schema tweaks.
- 2025-09-20 17:10 UTC — Added guidance for the new Engagement and Measurement Period WinForms plus schema alignment reminders for measurement_period_id columns.
- 2025-09-19 21:45 UTC — Documented UploadRunner-based upload pattern, shared Excel parser helpers, and summary grid expectations.
- 2025-09-19 15:30 UTC — Refined the handbook to cover only C#/.NET practices and recorded the adjustment in the Mistake Catalog.

## Language and Runtime Stack
- **Primary UI**: C# 12 on .NET 8 Windows Forms (Win-x64 build target; use `-p:EnableWindowsTargeting=true` when compiling from non-Windows hosts).
- **ORM**: EF Core 8 with the Pomelo MySQL provider for centralized data access.
- **Local data**: SQLite (app configuration, cached lookups, operator preferences).
- **Central data**: MySQL for shared fact tables, audit logs, and master data.

## Project Constraints
- Synchronous execution only—do not introduce `async`/`await` or background threading unless explicitly requested.
- Respect the data split: SQLite remains local-only; MySQL holds centralized/shared facts. Never migrate SQLite tables into MySQL.
- Avoid speculative parallelism or unnecessary indirection; favor clear, imperative flows.
- Ensure each upload batch executes deterministically and idempotently (guard against duplicate loads).

## Process Rules
- Restate and confirm understanding before coding.
- If ambiguous, send numbered questions.
- Start only after explicit "GO".
- When performing design/review tasks, deliver the findings and the end-to-end remediation plan together so stakeholders receive the full context in one response.

## Mistake Catalog
Document newly discovered mistakes and their fixes **before each delivery** to avoid regressions.
- 2025-09-20 23:30 UTC — Parsing — Numeric Excel cells outside the OLE Automation range crashed `DateTime.FromOADate` — Validate the double range and swallow invalid conversions so parsers emit warnings instead of exceptions.
- 2025-09-20 23:30 UTC — Uploads — Disposing the context used to create the EF execution strategy caused `MySqlDbContext` to be reused after disposal — Keep that context alive for the duration of `Execute` and scope per-attempt contexts inside the execution strategy delegate.
- 2025-09-20 21:55 UTC — Uploads — Manual transaction scopes conflicted with Pomelo retry strategy causing “MySqlRetryingExecutionStrategy” failures — Wrap each per-file operation in `CreateExecutionStrategy().Execute(...)` while preserving single-file transactions.
- 2025-09-20 19:55 UTC — Parsing — Worksheet fallbacks and strict header normalization caused repeated upload failures on EY resourcing exports — Added sheet name preferences, broadened normalization (including % → PCT), aggregated multi-row headers, and switched plan/ETC/margin parsers to header-driven extraction with localized synonyms.
- 2025-09-20 18:40 UTC — Parsing — Parsers failed whenever summary worksheets or localized headers appeared first — Record explicit worksheet targeting, expanded normalization, and broader synonym coverage in the remediation plan to prevent repeats.
- 2025-09-19 14:09 UTC — UI — Form1 designer no longer declared the upload summary DataGridView or week-ending DateTimePicker, producing build errors — Reinstated both controls in the designer and restructured the layout to align with existing runtime code.
- 2025-09-19 13:57 UTC — UI — Master-data grids stopped showing rows after refresh because BindingLists were updated without rebinding — Route grid data through BindingSource with ResetBindings and add insert confirmations to surface success.
- 2025-09-19 12:58 UTC — Data Modeling — EF models expected measurement periods and fact foreign keys that production MySQL lacked — Delivered alignment script to create measurement_periods, add measurement_period_id columns, and normalize dim_engagement widths.
- 2025-09-20 17:10 UTC — UI — Menu actions referenced unimplemented master-data forms causing TypeLoadException — Implemented EngagementForm and MeasurementPeriodForm with validation and designer wiring checklist.
- 2025-09-19 21:45 UTC — Architecture — Form1 orchestrated uploads directly, leaving dry-run UI and inconsistent transactions — Adopt UploadRunner/service helpers, remove dry-run toggles, and centralize parsing/log summaries.
- 2025-09-19 15:30 UTC — Documentation — Limited the engineering handbook to C#/.NET scope to reflect project direction.
- Format for new entries: `YYYY-MM-DD HH:MM UTC — [Area] — Issue — Fix/Prevention`

## Naming and Structure Conventions
- Favor human-readable names (e.g., `MarginUploadController`, `MeasurementPeriodForm`).
- Organize source by feature (e.g., `Uploads/Margin`, `MasterData/MeasurementPeriods`). Shared utilities reside under `Common/` (UI helpers, IO, logging).
- One class = one responsibility; extract helpers for dialog configuration, parsing, validation, bulk writes, and progress reporting.
- Use partial classes sparingly (primarily for WinForms designer separation).

## Data Access & Transactions
- Create a fresh `DbContext` instance per logical operation (per file for multi-file batches).
- Wrap each file load in its own transaction. Roll back on validation/load errors while allowing subsequent files to continue.
- Keep the context used to obtain `Database.CreateExecutionStrategy()` alive for the duration of the execution and create new upload contexts inside the strategy delegate so EF Core does not attempt to reuse a disposed instance.
- Use parameterized queries and prepared commands for any raw SQL.
- Persist timestamps in UTC; convert to local time only in the UI.
- Record per-file summaries (rows read/inserted/updated, warnings, errors) for operator review and audit.

## Error Handling & Logging
- Catch exceptions at UI/service boundaries. Surface concise, user-friendly messages while logging full technical details to file and/or MySQL logging tables.
- Log validation failures, schema mismatches, and DB exceptions with enough context to replay (file name, measurement period, user, timestamp).
- Never swallow exceptions silently; always log and notify the operator of the outcome.

## UI Guidance
- Centralize `OpenFileDialog` configuration: expose helpers for single vs. multi-select, expected file filters, and deterministic ordering (alphabetical) of selections.
- Provide shared helpers for `DataGridView` formatting (column widths, numeric/date formats, read-only flags, and culture-invariant display).
- Display per-file outcomes through the upload summary grid (`DataGridViewStyler.ConfigureUploadSummaryGrid`) backed by `UploadFileSummary` models.
- Always validate user inputs (measurement period selection, fiscal year ranges, etc.) before triggering long-running work.
- Offer status/progress feedback (status bar, modal progress window, or log pane) summarizing file-by-file results.

## Parsing & File Handling
- Implement reusable parsing helpers for shared header validation, invariant number/date parsing (`CultureInfo.InvariantCulture`), and consistent error messages using `ExcelParserBase`, `HeaderSchema`, and `ExcelHeaderDetector`.
- Validate numeric Excel dates against the legal OLE Automation range before calling `DateTime.FromOADate`; treat out-of-range values as blanks and surface warnings instead of throwing.
- Prefer `ExcelWorksheetHelper.SelectWorksheet` with ordered sheet name candidates (e.g., `RESOURCING`, `Export`, localized labels) to avoid landing on summary tabs.
- Rely on expanded header schemas with localized synonyms; `ExcelHeaderDetector` now collapses multi-row headers and treats `%` as `PCT`, so match keys against the normalized tokens.
- Use `LevelNormalizer.Normalize` when deriving canonical level codes from plan/ETC rows before resolving level IDs.
- Normalize and sort multi-file selections before processing.
- Validate schema/required columns prior to DB operations; abort file load if required headers are missing.

## Database Bulk Write Practices
- Buffer inserts/updates before committing to reduce transaction time while keeping memory use predictable.
- Use EF Core change tracking judiciously; prefer `AddRange`/`UpdateRange` or batched `ExecuteUpdate/ExecuteDelete` where appropriate.
- Orchestrate uploads through `UploadRunner` to enforce per-file transactions, disable change tracking during bulk inserts, and commit once per file.
- Maintain CREATE/ALTER scripts in `DatabaseScripts/`; update them with any model changes and capture diffs for both SQLite and MySQL schemas.

## Testing & Checklists
Before committing changes:
1. Run `dotnet build -p:EnableWindowsTargeting=true` at solution root to confirm compile health.
2. Execute smoke tests for each affected upload type (Margin, ETC, Budget multi-file; others single-file) against representative fixtures.
3. Validate master-data forms (Measurement Periods, Fiscal Year) for CRUD, validation, and binding.
4. Review schema changes and update CREATE/ALTER scripts when EF models evolve.
5. Ensure logs capture expected entries during failure scenarios.

## Documentation Discipline
- Every functional change must update **both** `agents.md` (guideline impact) and `README.md` (functional spec, flows).
- Include a `What changed` subsection with timestamp in both docs for each revision.
- Reference related Mistake Catalog entries when fixes address known issues.

## Tooling & Environment Notes
- Confirmed container toolchain on 2025-09-18:
  - `.NET SDK 8.0.119` installed via Microsoft package feed.
  - Verification command: `dotnet --info` (see terminal log for full output).
- When building on Linux, invoke `dotnet build -p:EnableWindowsTargeting=true` to satisfy Windows Forms targeting requirements.

## Validation Requirements
- For every critical C# API or package adoption, cite current Microsoft/.NET documentation or release notes when documenting availability or behavior.
