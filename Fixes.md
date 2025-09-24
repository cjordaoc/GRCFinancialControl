# Fixes Catalog

## What changed
- 2025-09-24 14:40 UTC — Added guidance for installing the tarball-based .NET SDK to obtain Windows Desktop packs on Linux and recorded the EF schema smoke test as a regression guard.
- 2025-09-22 18:30 UTC — Updated rebuild guidance so all missing-table issues reference the consolidated `DatabaseScripts/20250922_full_rebuild.sql` baseline.
- 2025-09-21 00:45 UTC — Logged the Help dialog packaging fix so `README.md` ships with builds and remains viewable in-app.
- 2025-09-21 00:15 UTC — Created a centralized fixes catalog with historical mistakes and operational error guidance moved from `agents.md` and `README.md`.

## How to use this file
- Review the entries below whenever you encounter an error before searching the web for external solutions.
- Append new mistakes and their fixes **before** each delivery to prevent regressions.
- Reference relevant entries in commit messages or documentation updates when you apply an existing fix.

## Mistake Catalog
- 2025-09-24 14:40 UTC — Build — `MSB4019` complaining `Microsoft.NET.Sdk.WindowsDesktop.targets` missing on Linux → Install the official SDK tarball (e.g., `dotnet-sdk-8.0.120-linux-x64.tar.gz`) into `/usr/share/dotnet8`, export `DOTNET_ROOT=/usr/share/dotnet8`, update `PATH`, and rerun `dotnet build -p:EnableWindowsTargeting=true` before executing tests.
- 2025-09-19 19:50 UTC — Uploads — Plan/ETC loads crashed with `DimSourceSystems` missing — Apply `DatabaseScripts/20250922_full_rebuild.sql` to recreate the schema (includes `DimSourceSystems`) and keep EF ID properties on `long` so MySQL tables expose BIGINT keys before uploads.
- 2025-09-21 00:45 UTC — Help — Help dialog showed “README.md not found” in packaged builds → Copy the root `README.md` into the WinForms output via `<None Include="..\README.md" CopyToOutputDirectory="PreserveNewest" />` and confirm the tab renders text locally.
- 2025-09-20 23:30 UTC — Parsing — `DateTime.FromOADate` crashed on invalid Excel doubles → Range validation added, invalid values emit warnings.
- 2025-09-20 21:55 UTC — Uploads — Transaction scopes clashed with Pomelo retry → Wrapped operations in `CreateExecutionStrategy().Execute(...)`.
- 2025-09-19 14:09 UTC — UI — Missing controls broke builds → Restored DataGridView + DateTimePicker in Form1 designer.

## Error Recovery & Logging Guidance
- Each file logs validation and load outcomes to MySQL (central log table) and a local text log.
- Failed files remain isolated; correct source data and rerun only the affected files.
- Use this Fixes Catalog to identify prior mitigations before developing a new remedy or consulting external resources.
