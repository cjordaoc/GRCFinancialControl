# GRC Financial Control — Agent Guide (vNext Refactored)

These instructions keep contributors consistent across tooling, architecture, and workflow.
All work must follow the **“as simple as it can be”** rule — every change should reduce complexity, not add it.

---

## 1 · Environment & Build Readiness
Confirm .NET 8, restore dependencies, and ensure `dotnet build -c Release` passes with zero warnings before shipping significant code changes.

---

## 2 · Architecture & Coding Principles
- MVVM (Avalonia).
- Views = XAML layout only; no business logic.
- ViewModels = interaction/state handling.
- Models = domain entities inside *.Core.
- Register all services through Host Builder; no custom factories.
- Simplicity First → reuse > abstraction.

---

## 3 · Performance & Refactor Policy (Precision Mode)
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

## 4 · Quality Gates
- Strict MVVM boundaries.
- Code must be small, readable, deterministic.
- Behavior unchanged unless a functional requirement explicitly demands it.
- Documentation updates are mandatory whenever functionality changes.

---

## 5 · Documentation Discipline
- For **every new functionality** (beyond bug fixes), update both `README.md` and `readme_specs.md` to reflect the behavior and technical specification.
- Keep documentation changes synchronized with the implemented scope in the same branch.

---

## 6 · Class & Interface Governance
- Consult `class_interfaces_catalog.md` before creating any class or interface to determine if reuse, improvement, or inheritance is possible.
- Whenever you add, rename, or delete a class/interface, update `class_interfaces_catalog.md` in the same change set with its purpose and dependencies.

---

✅ **Core Mantra**
> Make it work → Make it simple → Keep it consistent → **Make it perform.**
