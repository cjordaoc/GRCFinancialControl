# Stage 2 — Architectural & Redundancy Review

This assessment catalogs cross-application duplication and structural drift identified during Stage 2. Each item includes recommended consolidation targets to support future shared implementations spanning **GRC Financial Control** and **Invoice Planner**.

## 1. Shared Messaging Patterns
- Both apps declared equivalent `CloseDialogMessage` types that only differed by a `sealed` modifier. (`GRCFinancialControl.Avalonia/Messages/CloseDialogMessage.cs`, `InvoicePlanner.Avalonia/Messages/CloseDialogMessage.cs`)
  - **Action:** Stage 2A moved the message into the shared `GRC.Shared.UI/Messages` namespace and both dialog services now reference the single implementation.
- Refresh messaging previously existed only in GRC Financial Control (`RefreshDataMessage`) while Invoice Planner used a bespoke `RefreshInvoiceLinesGridMessage`. Stage 2A introduced the shared `RefreshViewMessage` so both applications now participate in a single refresh notification pipeline.

## 2. Dialog Infrastructure Duplication
- `DialogService` in both apps builds almost identical acrylic overlays, width/height calculations, and focus loops. (`GRCFinancialControl.Avalonia/Services/DialogService.cs`, `InvoicePlanner.Avalonia/Services/DialogService.cs`)
  - ✅ **Resolved (Stage 2A):** Introduced `GRC.Shared.UI.Dialogs.ModalDialogService` (via `IModalDialogService`) and supporting types to centralize overlay creation, focus trapping, and owner synchronization. Both desktop dialog services now receive the shared service through host builder registration while opting into single-window or stacked behavior via `ModalDialogOptions`.
- `ConfirmationDialogViewModel` in GRC Financial Control mirrors the logic already covered by Invoice Planner’s dialog view models; unifying confirmation dialogs in shared UI avoids redundant commands and messaging wiring.

## 3. ViewModel Base Inconsistencies
- Each application maintains a bespoke `ViewModelBase`. (`GRCFinancialControl.Avalonia/ViewModels/ViewModelBase.cs`, `InvoicePlanner.Avalonia/ViewModels/ViewModelBase.cs`)
  - ✅ **Resolved (Stage 2A):** Added `GRC.Shared.UI.ViewModels.ValidatableViewModelBase` and `ObservableViewModelBase` so both applications reuse the same messenger plumbing, command notification helper, and loading hooks while extending the appropriate base for validation or simple observation.

## 4. Resource Fragmentation
- Both apps keep their own `Strings.resx` sets and the legacy `Resources/Shared` tree persists alongside feature-specific `.resx` files.
  - **Action:** Stage 3 should consolidate all localized assets into `GRC.Shared.Resources/Localization/Strings*.resx`, retiring legacy duplicates in `GRCFinancialControl.Avalonia/Resources`, `InvoicePlanner.Avalonia/Resources`, and `Resources/Features/Import/Resources`.
- Theme dictionaries (spacing, colors, typography) already reside in shared projects; any new tokens must be appended there to prevent drift.

## 5. Helper & Service Overlap
- File and picker services (`App.Presentation/Services/*`) are consumed by both apps but remain app-specific. Introduce shared abstractions in `GRC.Shared.UI` so dialogs, pickers, and toast helpers use the same implementation surface.
- Toast/localization helpers are wired identically in both apps; ensure the Stage 3 localization refactor also routes toast text through shared providers.

## 6. Candidates for Deletion or Merge
- `Resources/Shared/Resources*.resx` duplicates strings found inside app-level resources and should be retired post Stage 3.
- Multiple dialog view models in GRC Financial Control (e.g., `Dialogs/ManagerAssignmentEditorViewModel`, `Dialogs/PapdEngagementAssignmentViewModel`) inherit identical close-message patterns; extracting a shared dialog base class will remove boilerplate once the `CloseDialogMessage` is centralised.

## 7. Next Steps
- Define acceptance tests ensuring both apps still resolve localized strings after consolidation.
- Update `class_interfaces_catalog.md` in future stages when shared abstractions replace the current duplicates.
