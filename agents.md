# GRC Financial Control – Engineering Guidelines

## 1. Change Log
Keep this section always updated. Use the format:  
`YYYY-MM-DD HH:MM UTC — [Area] — Summary`

- **2025-09-20 23:30 UTC** — Excel: Added OLE Automation range guards to date parsing. Uploads: Ensured `UploadRunner` maintains EF execution strategy context to prevent disposed `MySqlDbContext` reuse during ETC/Margin uploads.  
- **2025-09-20 21:55 UTC** — Uploads: Introduced engagement ID extraction helpers, aligned plan/weekly declaration parsers with EY workbook layouts, and wrapped UploadRunner in EF execution strategy retries.  
- **2025-09-20 19:55 UTC** — Excel Parsing: Rolled out worksheet preferences, resilient header detection, weekly plan aggregation, ETC enrichment, and margin schema coverage.  
- **2025-09-20 18:40 UTC** — Documentation: Reviewed parser strategy; mandated combined findings + remediation in single responses to cut iteration loops.  
- **2025-09-20 17:10 UTC** — UI: Added Engagement and Measurement Period WinForms plus schema alignment reminders for `measurement_period_id`.  
- **2025-09-19 21:45 UTC** — Architecture: Centralized upload logic into UploadRunner and helpers.  
- **2025-09-19 14:09 UTC** — UI: Restored upload summary grid + week-ending picker in Form1 for cross-platform builds.  
- **2025-09-19 13:57 UTC** — UI: Improved master-data binding reliability and added insert confirmations.  
- **2025-09-19 12:58 UTC** — DB: Delivered MySQL alignment scripts for measurement periods, fact tables, and engagement schema tweaks.  
- **2025-09-19 15:30 UTC** — Docs: Limited handbook scope to C#/.NET practices.  

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

## 5. Mistake Catalog (Fixes.md)
Document issues + fixes **before delivery** to avoid regressions.  
Format:  
`YYYY-MM-DD HH:MM UTC — [Area] — Issue — Fix/Prevention`

Examples:
- **2025-09-20 23:30 UTC — Parsing** — `DateTime.FromOADate` crashed on invalid Excel doubles → Range validation added, invalid values emit warnings.  
- **2025-09-20 21:55 UTC — Uploads** — Transaction scopes clashed with Pomelo retry → Wrapped operations in `CreateExecutionStrategy().Execute(...)`.  
- **2025-09-19 14:09 UTC — UI** — Missing controls broke builds → Restored DataGridView + DateTimePicker in Form1 designer.  

## 6. Naming & Structure
- Favor descriptive, human-readable names (e.g., `MarginUploadController`, `MeasurementPeriodForm`).  
- Organize by **feature** (`Uploads/Margin`, `MasterData/Periods`); shared utils under `Common/`.  
- One class = one responsibility. Extract helpers for parsing, validation, logging.  
- Use **partial classes** only for WinForms designer separation.  

## 7. Data Access & Transactions
- Fresh `DbContext` per logical operation.  
- One **transaction per file** — rollback on error, continue with next file.  
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

## 12. Testing & Pre-Commit Checklist
1. `dotnet build -p:EnableWindowsTargeting=true` passes.  
2. Smoke test each upload type with fixture data.  
3. Validate master-data forms for CRUD + binding.  
4. Review schema diffs; update SQL scripts.  
5. Confirm logs capture failures as expected.
6. Whenever errors are found, look at the web to gather knowledge on how to fix them  

## 13. Documentation Discipline
- Every change updates **both** `agent.md` (guidelines) and `README.md` (functional spec) and fixes.md (create it if it does not exists - It should recieve all fixes applied so far).  
- Each doc update must include a **timestamped “What Changed”** entry.  
- Reference Mistake Catalog entries where relevant.  

## 14. Tooling & Environment
-Install the .net and apt-utils at the container as soon as the container starts
- Verified toolchain (2025-09-18):  
  - `.NET SDK 8.0.119` installed.  
  - Confirm via `dotnet --info`.  
- On Linux: build with `-p:EnableWindowsTargeting=true`.  
-Look at the web on how properly install the desktop workload and install it


## 15. Validation Requirements
For critical C# APIs/packages:  
- Evaluate Microsoft/.NET official docs or release notes.  
- Document availability and expected behavior.  
