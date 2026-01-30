# GRC Financial Control â€" Agent Guide (vNext Refactored)

These instructions keep contributors consistent across tooling, architecture, and workflow.
All work must follow the **â€œas simple as it can beâ€** rule â€" every change should reduce complexity, not add it.

---

## 1 Â· Environment & Build Readiness
Confirm .NET 8, restore dependencies, and ensure `dotnet build -c Release` passes with zero warnings before shipping significant code changes.
**GRC.Shared usage:** build the GRC.Shared DLLs (Release) and reference them from `/lib`. **Do not add project references** to GRC.Shared projects.

---

## 2 Â· Architecture & Coding Principles
- MVVM (Avalonia).
- Views = XAML layout only; no business logic.
- ViewModels = interaction/state handling.
- Models = domain entities inside *.Core.
- Register all services through Host Builder; no custom factories.
- Simplicity First â†’ reuse > abstraction.
- Prefer shared GRC.Shared.UI components (dialogs, status/loading/empty/toast/search controls, data templates) before creating app-specific XAML.
- Fail Fast. No Fallbacks

---

## 3 Â· Performance & Refactor Policy (Precision Mode)
- Evaluate loops and replace nested iterations with dictionary/set lookups.
- Pull allocations and heavy operations out of hot paths.
- Use `StringBuilder` for concatenations.
- Apply `await using` and `ConfigureAwait(false)` in libraries.
- Delete unused or duplicate classes/methods/resources silently.
- Merge redundant helpers/interfaces; flatten layers.
- Avalonia Views remain declarative.
- Preserve `.md`, `/artifacts/`, `/DataTemplates/`, `/Resources/`, `/Assets/`.
- Codex may pause/resume per stage; every stage prints progress.

---

## 3.1 Â· Fail-Fast Principle (No Fallbacks)
- **NO automatic column fallbacks** in data import. If expected column is not found at the hardcoded location, fail with explicit error.
- Column header search/detection is **acceptable** (source files may shift columns), but the search must find the column unambiguously.
- Once column is identified by header search, it must be used correctly (read from each row, not just header row).
- Implicit fallbacks hide data corruption and make bugs hard to trace.
- Always prefer explicit validation + clear error messages over silent fallback behavior.

---

## 4 Â· Quality Gates
- Strict MVVM boundaries.
- Code must be small, readable, deterministic.
- Behavior unchanged unless a functional requirement explicitly demands it.
- Documentation updates are mandatory whenever functionality changes.

---

## 5 Â· Documentation Discipline
- For **every new functionality** (beyond bug fixes), update both `README.md` and `readme_specs.md` to reflect the behavior and technical specification.
- Keep documentation changes synchronized with the implemented scope in the same branch.

---

## 6 Â· Class & Interface Governance
- Consult `class_interfaces_catalog.md` before creating any class or interface to determine if reuse, improvement, or inheritance is possible.
- Whenever you add, rename, or delete a class/interface, update `class_interfaces_catalog.md` in the same change set with its purpose and dependencies.

---

## 7 Â· Tasks Export Conventions
- Weekly tasks exports must target the XML schema `WeeklyTasks.xsd` (version 1.3) â€" legacy JSON payloads are no longer supported.
- Planner payment types surface through `PaymentTypeCatalog` (`TRANSFERENCIA_BANCARIA`, `BOLETOS`); do not introduce free-form values.
- Invoice descriptions are generated at runtime following the Excel â€œTexto de Faturamentoâ€ pattern and must not be persisted in the database.

---

## 8 Â· Allocation Snapshot Architecture
- **Snapshot-Based Historical Tracking**: All revenue and hours allocations are now tied to specific closing periods, creating historical snapshots instead of mutable "current" values.
- **Unique Constraints**:
  - Revenue: `(EngagementId, FiscalYearId, ClosingPeriodId)`
  - Hours: `(EngagementId, FiscalYearId, RankName, ClosingPeriodId)`
- **Auto-Sync to Financial Evolution**: Whenever allocations are saved via `IAllocationSnapshotService`, the corresponding Financial Evolution snapshot is automatically updated.
- **Copy-From-Previous Feature**: Users can replicate allocations from the latest previous closing period for productivity.
- **Discrepancy Detection**: After each save, the system compares allocation totals against imported values and surfaces variances in the UI.
- **Fiscal Year Locking**: Allocations for locked fiscal years cannot be modified; service methods throw `InvalidOperationException` with descriptive messages.
- **Service Contract**: Use `IAllocationSnapshotService` for all allocation CRUD operations; avoid direct repository access to ensure sync consistency.

---

✅ **Core Mantra**
> Make it work → Make it simple → Keep it consistent → **Make it perform.**

---

## 9 · GRC.Shared Library Management

**Build & Reference Process:**
- GRC.Shared sources live in `/GRC.Shared` as part of the main repository (no longer a submodule).
- Build GRC.Shared in Release configuration: `dotnet build GRC.Shared/GRC.Shared.sln -c Release`.
- Copy resulting DLLs (`GRC.Shared.Core.dll`, `GRC.Shared.Resources.dll`, `GRC.Shared.UI.dll`) to `/lib`.
- All application projects reference these DLLs via `<Reference Include="..."><HintPath>..\lib\...</HintPath></Reference>`.
- **Do not add project references** to GRC.Shared projects from application layers.

**Modal Dialog Configuration:**
- `ModalDialogOptions` controls dialog behavior: tokenized sizing via `DialogContentRatio*` resource keys, title overrides, system window decorations, dimmed background, and owner-freeze behavior.
- Dialog services override `GetModalDialogOptions()` to configure layout (CenteredOverlay vs OwnerAligned), size tokens, and interaction behavior.
- All resource lookups (size ratios, brushes) enforce fail-fast validation—missing keys throw exceptions.

