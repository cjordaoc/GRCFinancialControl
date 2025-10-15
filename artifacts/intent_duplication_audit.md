# Intent & Duplication Audit Summary

The temporary analysis catalogued core service and view-model responsibilities and highlighted reuse opportunities before refactoring.

## Key Symbols Reviewed

- EngagementService orchestrates engagement CRUD operations and is consumed broadly across allocation and reporting view models.
- ManagerService, PapdService, CustomerService, ClosingPeriodService, and FiscalYearService all share the same EF Core CRUD template with minimal specialization.
- ImportService owns spreadsheet ingestion, touching multiple downstream aggregates and logging infrastructure.
- DialogService and the various editor view models coordinate modal workflows in the Avalonia layer.

## Duplication Highlights

- Service classes for simple entities (`PapdService`, `ManagerService`, `CustomerService`, `ClosingPeriodService`, `FiscalYearService`) repeat identical asynchronous context factory patterns.
- Editor-oriented view models (`PapdEditorViewModel`, `ManagerEditorViewModel`, `CustomerEditorViewModel`) expose matching property sets and save/cancel command flows.
- Assignment management view models for PAPDs and managers contain parallel filtering and dialog orchestration logic.
- Numeric sanitation helpers exist in multiple services; consolidating them would reduce subtle inconsistencies.

These findings will guide the consolidation work planned for the next stage.
