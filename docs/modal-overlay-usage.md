# Modal Overlay Usage Guide

The modal dialog services in both Avalonia shells (`GRCFinancialControl.Avalonia` and `InvoicePlanner.Avalonia`) now create a themed, full-window overlay at runtime. Each modal displays its content inside a centered card that never exceeds 55 % of the owner window dimensions, traps keyboard focus, and restores the previously focused element when it closes.

## How dialogs are constructed

1. **View resolution** – `ViewLocator` maps the requested view-model to its paired `UserControl`. The service assigns the view-model as the control's `DataContext`.
2. **Overlay window** – A transparent `Window` is instantiated with:
   - Backdrop brush `ModalOverlayBrush` (~55 % black) to dim and block the owner.
   - Card styling driven by `ModalDialogBackgroundBrush`, `ModalDialogPadding`, `ModalDialogCornerRadius`, and `ModalDialogShadow` from `App.Presentation/Styles/ModalOverlay.xaml`.
   - Owner sizing: the dialog card caps itself at 55 % of the owner width/height and respects a 360 × 320 minimum.
3. **Interaction affordances** – The services disable the owner window, capture the element that had focus, and register keyboard handlers so `Tab`/`Shift+Tab` loop inside the modal. Cancel buttons marked with `IsCancel="True"` respond to `Esc`, and default buttons (`IsDefault="True"`) respond to `Enter` only when enabled.
4. **Focus restoration** – After the modal closes the services dispose their size subscriptions, detach keyboard handlers, re-enable the owner, and restore the captured focus target.

## Adding a new modal

- Implement the UI as a `UserControl` in the target shell. Avoid placing modal layout directly inside the main view.
- Expose a matching view-model registered with DI. Use existing `IDialogService` APIs (`ShowDialogAsync`, `ShowConfirmationAsync`) to launch the modal.
- Inside the view, ensure:
  - Edge padding ≥ 24 px and consistent spacing tokens (`Space12`, `Space16`, `Space24`).
  - Footer buttons are right-aligned with `IsDefault`/`IsCancel` flags.
  - Read-only flows disable or hide the Save button per the modal UX rules.
- Close the modal via messenger (`CloseDialogMessage`) or by invoking the associated close command.

## Theming considerations

- Use theme resources from `App.Presentation` instead of hard-coded colors. When a new surface token is required, define it locally in the view's scope and keep the backdrop alpha near 0.55.
- Both services read brushes, padding, and shadow resources at runtime. Updating `App.Presentation/Styles/ModalOverlay.xaml` automatically applies to every modal in both applications.

Following this pattern keeps modal experiences consistent between light and dark themes while honoring the accessibility checklist from the overlay refresh work.
