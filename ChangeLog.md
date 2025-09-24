# Change Log

## What changed

- 2025-09-24 22:15 UTC — Enabled margin uploads to seed missing `DimEngagements` records with opening margins, taught the charges parser to prioritize Detail sheets over summary tabs, and updated documentation to reflect the .NET SDK 8.0.120 toolchain verification.
- 2025-09-25 18:45 UTC — Hardened ETC uploads by detaching unintended `DimEngagements` inserts/updates, surfaced suppression warnings in summaries, and refreshed documentation/fixes catalogs to reflect the read-only enforcement.
- 2025-09-25 16:10 UTC — Prevented ETC uploads from creating new `DimEngagements` rows by introducing a read-only engagement resolver, updated documentation to stress preloading engagement master data, and recorded the safeguard in the fixes catalog.

- 2025-09-24 20:55 UTC — Connected Excel parsers to the File Field Upload Map, hardened employee resolution with source-system code uniqueness, refreshed the rebuild script, and captured the SDK verification checklist.
- 2025-09-24 14:40 UTC — Centralized EF models under `Data/` with fluent configuration classes, introduced the MySQL schema smoke test, and documented the tarball-based .NET SDK requirement for Linux builds targeting WinForms.
- 2025-09-22 18:30 UTC — Consolidated the MySQL rebuild guidance into a single baseline script and updated documentation to reflect the new authoritative artifact.
- 2025-09-19 19:50 UTC — Logged the BIGINT ID migration plus the `DimSourceSystems` bootstrap script for upload services.
- 2025-09-21 00:45 UTC — Captured the packaged Help dialog update so `README.md` travels with builds, with pointers to consult the in-app copy.
- 2025-09-21 00:15 UTC — Consolidated engineering and functional history from `agents.md` and `README.md` into this dedicated changelog.

## Engineering Guidelines History (agents.md)
- 2025-09-25 18:45 UTC — Logged the ETC upload guard that detaches unintended engagement inserts/updates and requires warning the operator when master-data mutations are suppressed.
- 2025-09-24 22:15 UTC — Captured the margin seeding guidance, Detail-sheet targeting for charges uploads, and the .NET 8.0.120 verification requirement in `agents.md`.
- 2025-09-25 16:10 UTC — Clarified that ETC uploads must resolve existing engagements via `IdResolver.TryResolveEngagement`, skipping rows instead of inserting master data when IDs are missing.
- 2025-09-24 20:55 UTC — Added guidance to honor the File Field Upload Map during parsing, require source-system employee code uniqueness, and log SDK verification in delivery checklists.
- 2025-09-24 14:40 UTC — Documented the `Data/` folder EF organization, the schema smoke test requirement, and the tarball-based SDK installation workflow for Windows desktop builds on Linux.
- 2025-09-22 18:30 UTC — Database: Replaced incremental scripts with the single `DatabaseScripts/20250922_full_rebuild.sql` baseline to keep rebuilds aligned with production.
- 2025-09-21 00:45 UTC — Reinforced documentation discipline by noting the Help dialog ships the packaged `README.md`, making accuracy mandatory for every delivery.
- 2025-09-19 19:50 UTC — Standardized MySQL IDs on BIGINT/`long` and directed engineers to apply the `DimSourceSystems` bootstrap script before uploads.
- 2025-09-20 23:30 UTC — Excel: Added OLE Automation range guards to date parsing. Uploads: Ensured `UploadRunner` maintains EF execution strategy context to prevent disposed `MySqlDbContext` reuse during ETC/Margin uploads.
- 2025-09-20 21:55 UTC — Uploads: Introduced engagement ID extraction helpers, aligned plan/weekly declaration parsers with EY workbook layouts, and wrapped UploadRunner in EF execution strategy retries.
- 2025-09-20 19:55 UTC — Excel Parsing: Rolled out worksheet preferences, resilient header detection, weekly plan aggregation, ETC enrichment, and margin schema coverage.
- 2025-09-20 18:40 UTC — Documentation: Reviewed parser strategy; mandated combined findings + remediation in single responses to cut iteration loops.
- 2025-09-20 17:10 UTC — UI: Added Engagement and Measurement Period WinForms plus schema alignment reminders for `measurement_period_id`.
- 2025-09-19 21:45 UTC — Architecture: Centralized upload logic into UploadRunner and helpers.
- 2025-09-19 14:09 UTC — UI: Restored upload summary grid + week-ending picker in Form1 for cross-platform builds.
- 2025-09-19 13:57 UTC — UI: Improved master-data binding reliability and added insert confirmations.
- 2025-09-19 12:58 UTC — DB: Delivered MySQL alignment scripts for measurement periods, fact tables, and engagement schema tweaks.
- 2025-09-19 15:30 UTC — Docs: Limited handbook scope to C#/.NET practices.

## Functional Specification History (README.md)
- 2025-09-25 18:45 UTC — Documented ETC engagement read-only enforcement and the warning/summary signals produced when unintended master-data writes are suppressed.
- 2025-09-24 22:15 UTC — Recorded that margin uploads now seed missing engagements with opening margins, noted the charges parser Detail-sheet preference, and updated environment guidance for .NET SDK 8.0.120 verification in the functional spec.
- 2025-09-25 16:10 UTC — Recorded the ETC upload safeguard that skips unknown engagements, emphasizing the need to seed `DimEngagements` before running ETC snapshots.
- 2025-09-24 20:55 UTC — Captured the mapping-driven header validation flow, MySQL employee code registry, rebuild script refresh, and toolchain verification checklist.
- 2025-09-24 14:40 UTC — Logged the EF Core configuration refactor, schema smoke test, and Linux tarball SDK setup so cross-platform engineers can reproduce builds/tests.
- 2025-09-22 18:30 UTC — Documented the consolidated MySQL rebuild script so operators know `DatabaseScripts/20250922_full_rebuild.sql` is the authoritative schema baseline.
- 2025-09-21 00:45 UTC — Documented that the WinForms Help dialog now surfaces the packaged `README.md`, outlining the workflow and reminding maintainers to keep it current.
- 2025-09-19 19:50 UTC — Added the `DimSourceSystems` baseline script and BIGINT ID requirement so master data uploads share consistent keys.
- 2025-09-20 23:30 UTC — Hardened Excel date parsing to ignore out-of-range OLE Automation values and kept the UploadRunner execution-strategy context alive to prevent disposed `MySqlDbContext` errors during ETC and margin loads.
- 2025-09-20 21:55 UTC — Enabled EY resourcing, Retain, and ERP workbook ingestion via engagement ID extraction helpers, aligned weekly allocation parsing, and wrapped UploadRunner operations in EF execution strategy retries.
- 2025-09-20 19:55 UTC — Implemented worksheet targeting preferences, hardened header detection, weekly plan aggregation, enriched ETC capture, and header-driven margin parsing aligned with the harmonized dictionary.
- 2025-09-20 18:40 UTC — Captured the Excel parser strategy review plus the remediation roadmap for worksheet selection, header normalization, weekly aggregation, ETC capture, and margin coverage.
- 2025-09-19 14:09 UTC — Reintroduced the upload summary grid and week-ending picker to Form1 so reconciliation uses the selected date and upload batches show per-file results again.
- 2025-09-19 13:57 UTC — Hardened master-data refresh so engagement/measurement period grids repopulate instantly and documented the new insert confirmation prompts across maintenance forms.
- 2025-09-19 12:58 UTC — Clarified database alignment steps for measurement periods, fact foreign keys, and engagement column widths.
- 2025-09-20 17:10 UTC — Delivered engagement CRUD UI, measurement period activation via SQLite parameters, and aligned MySQL scripts for `measurement_periods` + `measurement_period_id` columns.
- 2025-09-19 21:45 UTC — Integrated UploadRunner services, shared Excel parsers, and the upload summary grid to enforce deterministic multi-file batches and clearer results.
- 2025-09-18 19:55 UTC — Created baseline functional specification, documented upload behaviors, data flows, environment setup, and maintenance rules.
