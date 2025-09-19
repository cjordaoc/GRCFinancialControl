# GRC Financial Control – Engineering Guidelines

## What changed
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

## Mistake Catalog
Document newly discovered mistakes and their fixes **before each delivery** to avoid regressions.
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
