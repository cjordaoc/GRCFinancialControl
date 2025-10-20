# Modal Overlay Refresh — Stage 0 Report

The following views were identified from `docs/modal-overlay-plan.md` as requiring the redesigned modal presentation. Each entry lists the backing XAML view(s) and confirms the modal entry point in the current implementation.

## GRC Financial Control (Avalonia)
- **ManagerEditorView**  
  - View: `GRCFinancialControl.Avalonia/Views/ManagerEditorView.axaml` (+ `.axaml.cs`)  
  - Modal trigger: `ManagersViewModel` uses `IDialogService.ShowDialogAsync` for add/edit/view flows.
- **ClosingPeriodEditorView**  
  - View: `GRCFinancialControl.Avalonia/Views/ClosingPeriodEditorView.axaml` (+ `.axaml.cs`)  
  - Modal trigger: `ClosingPeriodsViewModel` opens it through `IDialogService.ShowDialogAsync` for add/edit/view.
- **ConfirmationDialogView**
  - View: `GRCFinancialControl.Avalonia/Views/Dialogs/ConfirmationDialogView.axaml` (+ `.axaml.cs`)
  - Modal trigger: `IDialogService.ShowConfirmationAsync` wraps this view inside the shared modal window overlay.
- **EngagementEditorView**  
  - View: `GRCFinancialControl.Avalonia/Views/EngagementEditorView.axaml` (+ `.axaml.cs`)  
  - Modal trigger: `EngagementsViewModel` calls `IDialogService.ShowDialogAsync` for add/edit/view scenarios.
- **ManagerAssignmentEditorView**  
  - View: `GRCFinancialControl.Avalonia/Views/Dialogs/ManagerAssignmentEditorView.axaml` (+ `.axaml.cs`)  
  - Modal trigger: `ManagerAssignmentsViewModel` launches it via `IDialogService.ShowDialogAsync` for assignment management.
- **FiscalYearEditorView**  
  - View: `GRCFinancialControl.Avalonia/Views/FiscalYearEditorView.axaml` (+ `.axaml.cs`)  
  - Modal trigger: `FiscalYearsViewModel` calls `IDialogService.ShowDialogAsync` for add/edit/view workflows.
- **CustomerEditorView**  
  - View: `GRCFinancialControl.Avalonia/Views/CustomerEditorView.axaml` (+ `.axaml.cs`)  
  - Modal trigger: `CustomersViewModel` opens it with `IDialogService.ShowDialogAsync`.
- **AllocationEditorView**  
  - View: `GRCFinancialControl.Avalonia/Views/AllocationEditorView.axaml` (+ `.axaml.cs`)  
  - Modal trigger: `AllocationsViewModelBase` derived screens invoke `IDialogService.ShowDialogAsync`.
- **PapdEditorView**  
  - View: `GRCFinancialControl.Avalonia/Views/PapdEditorView.axaml` (+ `.axaml.cs`)  
  - Modal trigger: `PapdViewModel` opens it through `IDialogService.ShowDialogAsync` for PAPD add/edit/view.
- **PapdEngagementAssignmentView**  
  - View: `GRCFinancialControl.Avalonia/Views/Dialogs/PapdEngagementAssignmentView.axaml` (+ `.axaml.cs`)  
  - Modal trigger: `PapdViewModel` launches it with `IDialogService.ShowDialogAsync` when managing assignments.

## Invoice Planner (Avalonia)
- **ErrorDialogView**  
  - View: `InvoicePlanner.Avalonia/Views/ErrorDialogView.axaml` (+ `.axaml.cs`)  
  - Modal trigger: `DialogService.ShowDialogAsync` hosts `ErrorDialogViewModel` for global/manual error flows.
- **PlanEditorView**  
  - Modal content is implemented by `InvoicePlanner.Avalonia/Views/PlanEditorDialogView.axaml` (+ `.axaml.cs`), which wraps the editor surface.  
  - Modal trigger: `PlanEditorViewModel.ShowPlanDialog()` calls `_dialogService.ShowDialogAsync` with `PlanEditorDialogViewModel`.
- **RequestConfirmationView**  
  - Modal content provided via `InvoicePlanner.Avalonia/Views/RequestConfirmationDialogView.axaml` (+ `.axaml.cs`).  
  - Modal trigger: `RequestConfirmationViewModel` opens `_dialogService.ShowDialogAsync` with `RequestConfirmationDialogViewModel`.
- **EmissionConfirmationView**  
  - Modal surface defined in `InvoicePlanner.Avalonia/Views/EmissionConfirmationDialogView.axaml` (+ `.axaml.cs`).  
  - Modal trigger: `EmissionConfirmationViewModel` calls `_dialogService.ShowDialogAsync` via `_dialogViewModel`.

All of the above rely on the shared modal plumbing introduced via `DialogService.ShowDialogAsync`, which now builds a themed modal window overlay at runtime. These are the targets for the redesign in subsequent stages.
