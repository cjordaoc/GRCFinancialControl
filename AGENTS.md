# GRC Financial Control – Agent Guide

These instructions keep contributors aligned on tooling, architecture, and workflow expectations.

## 1. Environment & Build Checks

1. Ensure the .NET 8 SDK is installed in the container.
2. Restore dependencies with `dotnet restore`.
3. Verify a clean build with `dotnet build` and address any compiler errors immediately.

> The solution must compile before you continue with feature or bug-fix work.

## 2. Architecture & Coding Practices

- **Pattern:** Use Avalonia with MVVM. Keep views declarative, place interaction logic in ViewModels, and keep models in the `.Core` project.
- **Dependency Injection:** Compose services via the existing host builder so implementations stay testable and replaceable.
- **Data Access:** Central business data lives in MySQL through `ApplicationDbContext`; SQLite is for local/session caches handled by `LocalDbContext`. Never migrate or sync SQLite data back to MySQL automatically.
- **Simplicity:** Prefer clear, direct implementations over additional abstractions or duplication. Each class should own one responsibility.
- **Logging:** Use `Microsoft.Extensions.Logging` for diagnostic output instead of ad-hoc logging helpers.

## 3. Excel & Data Handling

- Use vetted libraries such as EPPlus, ClosedXML, or NPOI for Excel ingestion/export.
- Confirm imported data aligns with expected schemas before persisting changes.
- When modifying allocation or totals logic, double-check sums against the source workbook to prevent regressions.

## 4. Manual Verification

When your change touches user flows (imports, allocations, reporting), review the workflow end-to-end with the available sample files to confirm it behaves as intended. If sample data is unavailable, document what still needs validation.

## 5. Recovery Protocol

If you are blocked:

1. Capture the current state with a descriptive branch and commit if the code builds.
2. Run `dotnet clean`, rebuild, and retest the failing scenario.
3. Revisit the design with a simpler approach or request clarification, then continue once the solution builds successfully.

## 6. Quality Gates

- Maintain MVVM boundaries—no business logic in code-behind files.
- Keep the implementation small and readable; remove unused code and avoid redundancy.
- Every submission must build without warnings or errors on `dotnet build`.
- Ensure feature behavior stays in sync with documented requirements; update the relevant docs only when a change genuinely affects them.
