# GRC Financial Control – Engineering Guidelines

## What changed
- 2025-09-25 18:45 UTC — Documented the ETC upload guard that detaches unintended DimEngagement inserts/updates so the pipeline remains read-only for engagements and highlighted the requirement to log suppressed IDs in upload summaries.
- 2025-09-25 16:10 UTC — Clarified that ETC uploads must not create DimEngagements records, added the TryResolveEngagement helper requirement, and reminded engineers to preload engagement master data before ETC loads.
- 2025-09-24 20:55 UTC — Wired Excel header detection to the File Field Upload Map, added a race-safe employee resolver with source-system code indexing, refreshed the MySQL rebuild script, and logged the .NET SDK verification requirement.
- 2025-09-24 14:40 UTC — Centralized EF entity models under `Data/` with per-entity `IEntityTypeConfiguration` classes, added the schema smoke test to the test suite, and documented the tarball-based .NET SDK installation needed for Windows desktop targeting on Linux.
- 2025-09-22 18:30 UTC — Replaced legacy incremental scripts with the single full-rebuild MySQL script so `DatabaseScripts/` mirrors the current production schema.
- 2025-09-19 19:50 UTC — Captured the BIGINT ID standard plus the `DimSourceSystems` baseline script so upload services share master data lookups.
- 2025-09-21 00:45 UTC — Ensured the WinForms Help dialog ships the current `README.md` by copying it to the build output and reinforced the requirement to keep the specification accurate.
- 2025-09-21 00:15 UTC — Redirected change history and mistake tracking to dedicated `ChangeLog.md` and `Fixes.md`, and clarified that existing fixes must be reviewed before researching externally.

## 1. Change Log
Historical updates now live in [`ChangeLog.md`](ChangeLog.md). Record engineering guideline changes there using the `YYYY-MM-DD HH:MM UTC — [Area] — Summary` format.

## 2. Technology Stack
- **UI:** C# 12, .NET 8 Windows Forms (Win-x64; use `-p:EnableWindowsTargeting=true` when compiling on non-Windows).  
- **ORM:** EF Core 8 with Pomelo MySQL provider.  
- **Local Data:** SQLite for app configuration, cached lookups, and operator preferences.  
- **Central Data:** MySQL for shared fact tables, audit logs, and master data.  

## 3. Core Principles
- Execution is **synchronous only** (no `async`/`await` unless explicitly required).  
- Respect **data boundaries**: SQLite = local; MySQL = centralized.  
- Ensure **deterministic, idempotent** batch uploads.  
- Prefer **clarity over abstraction** — keep flows imperative, no speculative parallelism.  

## 4. Process Rules
- Always **restate and confirm** understanding before coding.  
- If ambiguous, send **numbered clarification questions**.  
- Only start after explicit **“GO”**.  
- For design/review, deliver **findings + remediation plan together**.  

## 5. Fix Reference (`Fixes.md`)
- Document every issue and its prevention steps in [`Fixes.md`](Fixes.md) **before delivery** to avoid regressions.
- Format entries as `YYYY-MM-DD HH:MM UTC — [Area] — Issue — Fix/Prevention`.
- When an error occurs, consult `Fixes.md` first and reuse applicable remedies before searching the web.

## 6. Naming & Structure
- Favor descriptive, human-readable names (e.g., `MarginUploadController`, `MeasurementPeriodForm`).  
- Organize by **feature** (`Uploads/Margin`, `MasterData/Periods`); shared utils under `Common/`.  
- One class = one responsibility. Extract helpers for parsing, validation, logging.  
- Use **partial classes** only for WinForms designer separation.  

## 7. Data Access & Transactions
- Fresh `DbContext` per logical operation.  
- One **transaction per file** — rollback on error, continue with next file.
- Treat ETC loads as read-only for engagement master data: resolve engagements via `IdResolver.TryResolveEngagement` and skip rows when IDs are missing instead of creating new `DimEngagements` entries.
- Keep strategy context alive for retries; create per-attempt contexts inside delegate.
- Always use **parameterized queries** for raw SQL.  
- Persist all timestamps in **UTC** (convert in UI only).  
- Record per-file summaries (rows read, inserted, updated, warnings, errors).  

## 8. Error Handling & Logging
- Catch exceptions at **UI/service boundaries**.  
- Show concise, user-friendly messages; log full technical details.  
- Include file name, measurement period, user, timestamp in logs.  
- Never swallow exceptions silently.  

## 9. UI Guidelines
- Centralize `OpenFileDialog` setup (single vs. multi-select, filters, sort order).  
- Use shared helpers for `DataGridView` styling (columns, formats, read-only flags). Make sure they had a zebra-style row display  
- Show per-file upload results in **summary grid**.  
- Validate inputs (periods, fiscal years) before long-running ops.  
- Provide status/progress feedback (bar, modal, or log pane).  

## 10. Parsing & File Handling
- Base all parsers on `ExcelParserBase`, `HeaderSchema`, and `ExcelHeaderDetector`.  
- Guard against invalid OLE Automation dates.  
- Use ordered sheet candidates (`RESOURCING`, `Export`, localized synonyms).  
- Collapse multi-row headers, normalize `%` → `PCT`.  
- Use `LevelNormalizer` for canonical codes.  
- Validate schema before DB operations; abort file load if required headers missing.  

## 11. Database Bulk Writes
- Buffer writes before committing.
- Use `AddRange`, `UpdateRange`, or batched updates where possible.
- Orchestrate through `UploadRunner` (per-file transaction, disabled tracking, commit once).
- Keep CREATE/ALTER scripts in `DatabaseScripts/`; update on EF model changes.

## 11A. EF Core Model Organization
- Store MySQL POCOs under `GRCFinancialControl/Data/Entities` and map them with per-entity `IEntityTypeConfiguration<T>` classes in `Data/Configurations`.
- `MySqlDbContext` must call `ApplyConfigurationsFromAssembly` with the configuration namespace filter so only MySQL entities attach to the model.
- `LocalSqliteContext` uses the analogous `SqliteConfigurations` namespace; keep SQLite-only entities out of the MySQL model to preserve provider boundaries.
- When adding entities, update both the fluent configuration (table, columns, defaults, indexes) and the MySQL rebuild script so the `Tables.csv` contract stays authoritative.

## 12. Testing & Pre-Commit Checklist
1. `dotnet build -p:EnableWindowsTargeting=true` passes.
2. `dotnet test -p:EnableWindowsTargeting=true -f net8.0` (runs `LocalAppRepository` specs plus `MySqlModelSmokeTests`).
3. Smoke test each upload type with fixture data.
3. Validate master-data forms for CRUD + binding.
4. Review schema diffs; update SQL scripts.
5. Confirm logs capture failures as expected.
6. Whenever errors are found, review [`Fixes.md`](Fixes.md) first; only research externally if no existing remedy applies.

## 13. Documentation Discipline
- Every change updates **both** `agents.md` (guidelines) and `README.md` (functional spec), records any new fixes in [`Fixes.md`](Fixes.md), and appends the detailed summary to [`ChangeLog.md`](ChangeLog.md).
- Each doc update must include a **timestamped “What Changed”** entry.
- Reference relevant entries in [`Fixes.md`](Fixes.md) where applicable.
- Keep `README.md` accurate at all times; the packaged copy is displayed through the in-app Help menu.

## 14. Tooling & Environment
-Install the .net and apt-utils at the container as soon as the container starts
- Verified toolchain (2025-09-18):
  - `.NET SDK 8.0.119` installed.
  - Confirm via `dotnet --info`.
- On Linux: build with `-p:EnableWindowsTargeting=true`.
- Extract the official SDK tarball (e.g., `dotnet-sdk-8.0.120-linux-x64.tar.gz`) into `/usr/share/dotnet8` to obtain the Windows Desktop reference packs when running on Linux.
- Export `DOTNET_ROOT=/usr/share/dotnet8` and prepend `PATH=$DOTNET_ROOT:$PATH` before invoking build/test commands so the tarball-based SDK is used.
- `dotnet workload install windowsdesktop` is unavailable on Linux; rely on the tarball pack to satisfy WinForms targeting.


## 15. Validation Requirements
For critical C# APIs/packages:
- Evaluate Microsoft/.NET official docs or release notes.
- Document availability and expected behavior.

## 16. Schema & ID Standards
- All MySQL primary and foreign keys use `BIGINT`; mirror them with C# `long` properties.
- Apply `DatabaseScripts/20250919_dim_source_systems_bigint.sql` to provision `DimSourceSystems` and upgrade existing ID columns before running uploads.
