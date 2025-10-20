# Modal Overlay Migration Plan — Stage 1

## Inventory of Secondary Views
The tables below track every workflow that currently opens content outside of the main navigation surface (through `IDialogService`, `IErrorDialogService`, or direct dialog calls). Each row lists the existing view model/view pair and how we intend to host it once the new modal system is in place so both desktop experiences follow the same pattern.

### GRC Financial Control (Avalonia)
| Feature area & trigger | View model & view | Current presentation | Planned presentation |
| --- | --- | --- | --- |
| Managers – Add/Edit/View | `ManagerEditorViewModel` → `ManagerEditorView` | Inline dialog via `CurrentDialog` overlay | Reuse as modal overlay content hosted by the shared `ModalOverlayHost` |
| Closing periods – Add/Edit/View | `ClosingPeriodEditorViewModel` → `ClosingPeriodEditorView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| Closing periods – Delete data confirmation | `ConfirmationDialogViewModel` → `ConfirmationDialogView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` (bool result preserved) |
| Engagements – Add/Edit/View | `EngagementEditorViewModel` → `EngagementEditorView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| Engagements – Delete data confirmation | `ConfirmationDialogViewModel` → `ConfirmationDialogView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| Manager assignments – Add/Edit/View | `ManagerAssignmentEditorViewModel` → `ManagerAssignmentEditorView` | Inline dialog via `CurrentDialog` overlay | Rehost inside `ModalOverlayHost` (needs result for save) |
| Manager assignments – Delete confirmation | `ConfirmationDialogViewModel` → `ConfirmationDialogView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| Fiscal years – Add/Edit/View | `FiscalYearEditorViewModel` → `FiscalYearEditorView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| Fiscal years – Delete/Delete data/Lock/Close confirmations | `ConfirmationDialogViewModel` → `ConfirmationDialogView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| Customers – Add/Edit/View | `CustomerEditorViewModel` → `CustomerEditorView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| Customers – Delete data confirmation | `ConfirmationDialogViewModel` → `ConfirmationDialogView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| Allocations (hours & revenue) – Edit/View | `AllocationEditorViewModel` → `AllocationEditorView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| PAPD – Add/Edit/View | `PapdEditorViewModel` → `PapdEditorView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| PAPD – Manage engagement assignments | `PapdEngagementAssignmentViewModel` → `PapdEngagementAssignmentView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| PAPD – Delete data confirmation | `ConfirmationDialogViewModel` → `ConfirmationDialogView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |
| Settings – Clear all data confirmation | `ConfirmationDialogViewModel` → `ConfirmationDialogView` | Inline dialog via `CurrentDialog` overlay | Reuse within `ModalOverlayHost` |

### Invoice Planner (Avalonia)
| Feature area & trigger | View model & view | Current presentation | Planned presentation |
| --- | --- | --- | --- |
| Global error handling (unhandled exceptions, explicit error surface) | `ErrorDialogViewModel` → `ErrorDialog` (`Window`) | Separate window shown through `ErrorDialogService.ShowErrorAsync` | Extract content into a `UserControl` hosted by the shared `ModalOverlayHost`; service will forward to the same overlay API |
| Manual error reporting (e.g., validations surfacing technical stack traces) | `ErrorDialogViewModel` → `ErrorDialog` (`Window`) | Same window reused in non-global flows | Same modal overlay usage as above so error messaging behaves consistently |
| Invoice plans – Create new plan (`CreatePlanCommand`) | `PlanEditorViewModel` → `PlanEditorView` | In-view overlay built from a `Border` with `BrushOverlayStrong`, manually sized to `MaxWidth="1200"`/`MaxHeight="760"` and toggled by `IsPlanFormVisible` | Present the existing `PlanEditorView` content inside `ModalOverlayHost` (90 % sizing, focus trap, async close) and remove the bespoke overlay container |
| Invoice plans – Load/Edit existing plan (`LoadPlan(planId)`) | `PlanEditorViewModel` → `PlanEditorView` | Same ad-hoc overlay (shown after selecting a plan from the grid) | Use the shared overlay host so edit flows share the standardized modal behavior |
| Request confirmation – View/edit selected plan lines (`LoadPlanCommand`) | `RequestConfirmationViewModel` → `RequestConfirmationView` | Inline overlay (`Border` with `BrushOverlayStrong`) toggled by `IsPlanDetailsVisible`; contains editable DataGrid | Move the detail panel into the `ModalOverlayHost` to get consistent dimming, focus containment, and window resize behavior |
| Emission confirmation – Review/update plan lines (`LoadPlanCommand`) | `EmissionConfirmationViewModel` → `EmissionConfirmationView` | Same inline overlay pattern with manual `MaxWidth="1100"`/`MaxHeight="720"` | Rehost the detail surface within the shared overlay so emission updates follow the same modal model |

## General Approach
- **Single overlay infrastructure**: Introduce one reusable `ModalOverlayHost` component (implemented in `App.Presentation` so both shells can consume it) and embed it into `GRCFinancialControl.Avalonia.Views.MainWindow` and `InvoicePlanner.Avalonia.Views.MainWindow`. The host will satisfy the Stage 2 requirements (dim background, focus trap, 90 % sizing, header support, async API, stacked dialogs).
- **Dialog service continuity**: Keep using existing services (`IDialogService` in GRC and `IErrorDialogService` in Invoice Planner); internally they will forward to the shared host so that feature logic stays untouched.
- **User control reuse**: Every editor/confirmation/error view already derives from `UserControl` or can be trivially extracted from a `Window`; we will reuse the same visuals without adding new dependencies. Only fallback to a standalone window if a view cannot render in the overlay (no such cases identified yet).
- **Read-only enforcement plan**: Editors already accept an `isReadOnlyMode` flag. Stage 4 will enforce that any "View" or "Display" action sets this flag while still using the same overlay host.
- **Result handling**: Workflows that currently rely on a `bool` dialog result (e.g., confirmations, manager assignment editor) will keep that behavior through the host's async completion source.

## Next Steps
1. **Stage 2** – Build the shared `ModalOverlayHost`, embed it into both main windows, and update the dialog services (`IDialogService`, `IErrorDialogService`) to drive it.
2. **Stage 3** – Point all dialog flows listed above to the new host (structural cleanup, remove window shells if any remain).
3. **Stage 4** – Audit “View/Display” commands to guarantee read-only overlay usage.
4. **Stage 5** – Apply UX polish (header, close/save buttons, focus cycle validation, scaling checks).
5. **Stage 6** – Remove dead window code and document overlay usage for developers (see `docs/modal-overlay-usage.md`).

## Developer note · Opening content in the modal overlay
Follow these steps whenever a feature needs to present secondary content across either shell:

1. **Prepare the view** – Ensure the UI is implemented as a `UserControl`. If you are migrating from a legacy `Window`, move its layout into a user control and delete the window shell (the former `InvoicePlanner.Avalonia.Views.ErrorDialog` window has been retired following this rule).
2. **Expose a view-model** – Register the view-model with DI (typically `AddTransient` when the overlay owns the lifetime). Implement `IModalOverlayActionProvider` when the header requires Save/Confirm buttons; expose read-only state via existing flags.
3. **Request the overlay** –
   - In **GRC Financial Control**, obtain `IDialogService` from the view-model constructor and call `ShowDialogAsync(viewModel)` or `ShowConfirmationAsync(...)`.
   - In **Invoice Planner**, obtain `IModalOverlayService` (or `IErrorDialogService` for error flows) and call `ShowAsync(viewModel, title)`; hook to `CloseRequested` to finish the dialog.
4. **Handle close/results** – Use the overlay service `Close(result)` (or await the `Show*` call) to propagate dialog results back to the caller. Do not call `Close()` on controls directly.
5. **Avoid window fallbacks** – If an overlay cannot host the content, document the exception in the code and set `WindowStartupLocation="CenterOwner"`, `SizeToContent="Manual"`, and 90 % sizing, but always prefer the modal overlay.

All new secondary experiences must follow this pattern so the dimmed backdrop, focus trapping, and header controls remain consistent throughout the desktop apps.
