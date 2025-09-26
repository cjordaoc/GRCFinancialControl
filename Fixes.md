# Fixes Catalog

## What changed
- 2025-09-26 13:41 UTC — Documented the ETC parsing regression fix: treat the Resourcing `Activity` column as the engagement header, skip subtotal rows such as "Result," fall back to `ETC INFO` for canonical IDs, and captured the .NET SDK 8.0.120 `dotnet --info` verification before execution.
- 2025-09-24 22:15 UTC — Added remedies for margin uploads that encountered missing engagements and for charges files whose Detail sheets were skipped, plus noted the .NET 8.0.120 verification requirement.
- 2025-09-25 18:45 UTC — Logged the follow-up ETC regression where tracked `DimEngagements` entities still persisted; documented the change-tracker sweep that detaches unintended inserts/updates and the associated operator warnings.
- 2025-09-25 16:10 UTC — Captured the ETC upload regression that auto-created DimEngagements, recorded the resolution (read-only engagement lookup + skip), and reiterated the requirement to preload engagement master data.
- 2025-09-24 20:55 UTC — Documented the duplicate employee code race fix (MapEmployeeCodes + retry), refreshed rebuild guidance, and reminded teams to capture SDK/toolchain verification.
- 2025-09-24 14:40 UTC — Added guidance for installing the tarball-based .NET SDK to obtain Windows Desktop packs on Linux and recorded the EF schema smoke test as a regression guard.
- 2025-09-22 18:30 UTC — Updated rebuild guidance so all missing-table issues reference the consolidated `DatabaseScripts/20250922_full_rebuild.sql` baseline.
- 2025-09-21 00:45 UTC — Logged the Help dialog packaging fix so `README.md` ships with builds and remains viewable in-app.
- 2025-09-21 00:15 UTC — Created a centralized fixes catalog with historical mistakes and operational error guidance moved from `agents.md` and `README.md`.

## How to use this file
- Review the entries below whenever you encounter an error before searching the web for external solutions.
- Append new mistakes and their fixes **before** each delivery to prevent regressions.
- Reference relevant entries in commit messages or documentation updates when you apply an existing fix.

## Mistake Catalog
- 2025-09-26 13:41 UTC — Parsing — ETC uploads parsed zero rows when EY templates only listed `Activity` captions and subtotal lines before the data — Extend `EtcExcelParser` to map the `Activity` header, ignore subtotal rows such as "Result," pull canonical IDs from the `ETC INFO` sheet when no `E-#######` token is present, and lock the behavior with a regression test prior to rerunning uploads.
- 2025-09-24 22:15 UTC — Uploads — Margin loads failed with `DbUpdateConcurrencyException` when encountering engagements missing from MySQL — Allow the margin loader to create missing `DimEngagements` rows and seed `OpeningMargin` from `Margin % Bud` so the subsequent fact update succeeds.
- 2025-09-24 22:15 UTC — Parsing — Charges files selected the summary tab first and reported missing ENGAGEMENT/EMPLOYEE/DATE/HOURS headers — Update the parser to prefer Detail/Export sheets before falling back to the first visible worksheet.
- 2025-09-25 18:45 UTC — Uploads — ETC snapshots still inserted `DimEngagements` rows when other workflows seeded new engagements in the same context — Run a change-tracker sweep that detaches added/modified `DimEngagements` entries prior to saving, surface warnings listing suppressed IDs, and retry after seeding master data separately.
- 2025-09-25 16:10 UTC — Uploads — ETC snapshot loads inserted unexpected `DimEngagements` rows when encountering unknown IDs — Use `IdResolver.TryResolveEngagement` to require pre-existing engagements, skip affected rows with warnings, and seed `DimEngagements` via the master-data UI/import before running ETC uploads.
- 2025-09-24 14:40 UTC — Build — `MSB4019` complaining `Microsoft.NET.Sdk.WindowsDesktop.targets` missing on Linux → Install the official SDK tarball (e.g., `dotnet-sdk-8.0.120-linux-x64.tar.gz`) into `/usr/share/dotnet8`, export `DOTNET_ROOT=/usr/share/dotnet8`, update `PATH`, and rerun `dotnet build -p:EnableWindowsTargeting=true` before executing tests.
- 2025-09-24 20:55 UTC — Uploads — Concurrent employee creation triggered `DbUpdateConcurrencyException` or duplicate codes → Add `MapEmployeeCodes` with unique `(SourceSystemId, EmployeeCode)` index, wrap inserts in duplicate-key retry (MySQL 1062), and always requery existing rows before returning.
- 2025-09-19 19:50 UTC — Uploads — Plan/ETC loads crashed with `DimSourceSystems` missing — Apply `DatabaseScripts/20250922_full_rebuild.sql` to recreate the schema (includes `DimSourceSystems`) and keep EF ID properties on `long` so MySQL tables expose BIGINT keys before uploads.
- 2025-09-21 00:45 UTC — Help — Help dialog showed “README.md not found” in packaged builds → Copy the root `README.md` into the WinForms output via `<None Include="..\README.md" CopyToOutputDirectory="PreserveNewest" />` and confirm the tab renders text locally.
- 2025-09-20 23:30 UTC — Parsing — `DateTime.FromOADate` crashed on invalid Excel doubles → Range validation added, invalid values emit warnings.
- 2025-09-20 21:55 UTC — Uploads — Transaction scopes clashed with Pomelo retry → Wrapped operations in `CreateExecutionStrategy().Execute(...)`.
- 2025-09-19 14:09 UTC — UI — Missing controls broke builds → Restored DataGridView + DateTimePicker in Form1 designer.

## Error Recovery & Logging Guidance
- Each file logs validation and load outcomes to MySQL (central log table) and a local text log.
- Failed files remain isolated; correct source data and rerun only the affected files.
- Use this Fixes Catalog to identify prior mitigations before developing a new remedy or consulting external resources.
