# Change Log

## What changed
- 2025-09-21 00:45 UTC — Captured the packaged Help dialog update so `README.md` travels with builds, with pointers to consult the in-app copy.
- 2025-09-21 00:15 UTC — Consolidated engineering and functional history from `agents.md` and `README.md` into this dedicated changelog.

## Engineering Guidelines History (agents.md)
- 2025-09-21 00:45 UTC — Reinforced documentation discipline by noting the Help dialog ships the packaged `README.md`, making accuracy mandatory for every delivery.
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
- 2025-09-21 00:45 UTC — Documented that the WinForms Help dialog now surfaces the packaged `README.md`, outlining the workflow and reminding maintainers to keep it current.
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
