# GRC Financial Control — Agent Guide (vNext)

These instructions keep contributors consistent across tooling, architecture, and workflow.  
All work must follow the **“as simple as it can be”** rule — every change should reduce complexity, not add it.

---

## 1 · Environment & Build Readiness

1. Confirm **.NET 8 SDK** is installed in the container or host.  
2. Restore dependencies with  
   ```bash
   dotnet restore
   ```
3. Verify a clean Release build:  
   ```bash
   dotnet build -c Release
   ```
   The solution **must compile cleanly before and after** any edit. Never continue with warnings or errors.

---

## 2 · Architecture & Coding Principles

- **Design Pattern → MVVM (Avalonia)**  
  - *Views* = UI layout only (no business logic).  
  - *ViewModels* = interaction / state handling.  
  - *Models* = domain entities, kept inside `*.Core`.

- **Dependency Injection**  
  Register every service through the existing **Host Builder**.  
  Do **not** create parallel factories or custom service locators.

- **Data Access**  
  - **MySQL** → authoritative data (`ApplicationDbContext`) - Being Migrated to **Dataverse**.  
  - **SQLite** → local/session cache (`LocalDbContext`).  
  - No automatic sync from SQLite → MySQL.  
  - Schema changes belong **only** in `artifacts/mysql/rebuild_schema.sql`.

- **Simplicity First**  
  - Implement directly; avoid “helper-for-a-helper” layers.  
  - Remove unused or duplicated code before adding anything new.  
  - Reuse existing components; prefer composition to inheritance.  
  - One class = one responsibility.  
  - If a change adds new abstractions, justify why simpler reuse isn’t possible.

- **Logging**  
  Use the built-in `Microsoft.Extensions.Logging`.  
  No custom or ad-hoc log utilities.

---

## 3 · Excel & Data Handling

- Allowed libraries: **EPPlus**, **ClosedXML**, or **NPOI**.  
- Always validate imported structures and column headers against the expected schema before writing to the database.  
- When changing allocation or totals logic, cross-check calculated sums with the source workbook to prevent silent regressions.

---

## 4 · Change Verification (Manual Check)

Whenever your change affects imports, calculations, or reporting:

1. Run an **end-to-end** test using the provided sample files.  
2. If sample data is missing, record what still requires verification and mark the item pending.  
3. Confirm that visual behavior and totals match the documented expectation — not just that no errors occur.

---

## 5 · Recovery Protocol (If Blocked)

1. **Commit** the current working state (only if Release build passes).  
2. Run `dotnet clean && dotnet build -c Release`.  
3. Reproduce the failing scenario in isolation.  
4. If still unclear, step back to a simpler implementation or request clarification.  
   Complexity is never an acceptable workaround.

---

## 6 · Quality Gates (Every Submission Must Pass)

- Strict MVVM boundaries — zero business logic in code-behind.  
- Code must be **small, readable, and self-explanatory**.  
- Remove redundancy and dead code before submitting.  
- `dotnet build -c Release` = no warnings, no errors.  
- Behavior must stay aligned with documented requirements.  
  Update documentation **only when** functional behavior actually changes.  
- Never introduce abstractions or layers that do not add measurable value.

---

## 7 · Source of Truth & Consistency

- **Database schema:** single source in `artifacts/mysql/rebuild_schema.sql`.  
- **No other SQL scripts** may redefine tables or indexes.  
- Keep field names and types consistent across C#, EF, and SQL.  
- Use consistent terminology in code, UI, and documentation.  
- Each commit should represent one coherent, build-passing change.

---

## 8 · Execution Rules (vNext Addendum)

- **Simplicity first.** Keep the app as simple as possible. **Avoid duplicated code** and **unnecessary abstractions**. Prefer reuse and direct implementations.  
- **Build gate:** **Every stage must end with a successful** `dotnet build -c Release` **(zero warnings/errors).**  
- **Schema rules:**  
  - **MySQL:** all changes live **only** in `artifacts/mysql/rebuild_schema.sql`.  
  - **Dataverse:** keep schema handling **as is** (use the existing metadata artifact if applicable). **Do not introduce migrations.**  
- **SOLID, pragmatically:** enforce interfaces + DI; keep **single responsibility** per class; **no god classes**.  
- **Docs & artifacts:** keep documentation **minimal**; **remove temp files** and generated junk before committing.  
- **Commits:** after **each stage**, commit with a clear, focused message (one coherent unit of work).  
- **Stage summaries:** when a request is split into stages, each summary must state **current stage / total stages** and **what’s next**.

---

✅ **Core Mantra:**  
> Make it work → Make it simple → Keep it consistent.  
> Complexity, duplication, and abstraction are defects — not features.
