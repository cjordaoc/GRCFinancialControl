# Unused Code Report — 2025-09-24 14:35 UTC

## Solution inventory
- **Applications**: `GRCFinancialControl` (WinForms, .NET 8.0-windows) orchestrates UI, upload flows, and shared domain services.
- **Libraries**: `GRCFinancialControl.Persistence` (SQLite connection management) consumed by the WinForms app.
- **Tests**: `GRCFinancialControl.Persistence.Tests` (xUnit) now multi-targeted for `net8.0`/`net8.0-windows` to host persistence and model smoke tests.
- **Supporting assets**: `DatabaseScripts/` (authoritative MySQL DDL), `GRCFinancialControl/MySQL Specs/Tables.csv` (schema dictionary), and documentation roots (`README.md`, `agents.md`, `Fixes.md`, `ChangeLog.md`).

## Findings
| Path | Symbol(s) | Rationale | Confidence | Safe removal & validation |
| --- | --- | --- | --- | --- |
| `GRCFinancialControl/EF8/EfModels.cs` | Legacy POCOs + inline `MySqlDbContext` | Replaced by explicit POCO files and `IEntityTypeConfiguration` classes under `GRCFinancialControl/Data`. Old file duplicated mappings and blocked centralized configuration. | High | Remove after migrating entities to `Data/Entities` and configs to `Data/Configurations`; rebuild solution & run `dotnet test -p:EnableWindowsTargeting=true -f net8.0`. |
| `GRCFinancialControl/EF8/LocalSqliteContext.cs` | Inline SQLite context + entity | Superseded by `Data/LocalSqliteContext.cs` plus fluent config (`SqliteConfigurations/ParameterEntryConfiguration`). | High | Delete after moving code; ensure `DbContextFactory.CreateLocalContext` still materializes schema via new model, then rerun build/tests. |
| `GRCFinancialControl/EF8/DimMasterDataExtensions.cs` | Partial/extension definitions | Relocated to `Data/DimMasterDataExtensions.cs` alongside new POCO files. | High | Delete original after move; rely on compiler to confirm the partial class resolves. |
| Root `node_modules/`, `package.json`, `package-lock.json` | Obsolete tooling stub | Leftover package scaffold unrelated to .NET toolchain; unused throughout solution. | High | Remove directory/files; no code references. Confirm `.NET` build/test succeed. |

## Additional notes
- No other dead classes were detected: shared helpers in `Common/`, upload services, and parser infrastructure remain referenced across UI and services.
- New smoke test `MySqlModelSmokeTests` (wrapped in `#if NET8_0_WINDOWS`) ensures EF mappings stay aligned with `Tables.csv`.

## Post-removal validation plan
1. `dotnet build -p:EnableWindowsTargeting=true`
2. `dotnet test -p:EnableWindowsTargeting=true -f net8.0`
3. Manual spot-check: open WinForms solution on Windows host (out of scope here) to verify designer loads with refactored namespaces.
