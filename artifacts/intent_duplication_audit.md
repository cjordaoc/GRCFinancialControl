# Intent & Duplication Audit Summary

The temporary audit reviewed shared services across the Financial Control and Invoice Planner applications to highlight reuse candidates before consolidation.

## Intent Map Highlights

| Symbol | Purpose | Layer | Key Dependencies | Used By (approx.) |
| --- | --- | --- | --- | --- |
| `GRCFinancialControl.Persistence.Services.CustomerService` | SQL-backed CRUD plus cascade cleanup for customers. | Infrastructure / Service | `IDbContextFactory<ApplicationDbContext>` | 4 view models & imports |
| `GRCFinancialControl.Persistence.Services.Dataverse.DataverseCustomerService` | Dataverse implementation of `ICustomerService` with matching cascade rules. | Infrastructure / Service | `IDataverseRepository`, `DataverseEntityMetadataRegistry`, `ILogger` | 4 view models |
| `GRCFinancialControl.Persistence.Services.ManagerService` | SQL-backed manager directory operations. | Infrastructure / Service | `IDbContextFactory<ApplicationDbContext>` | 3 view models |
| `GRCFinancialControl.Persistence.Services.Dataverse.DataverseManagerService` | Dataverse-backed manager directory operations. | Infrastructure / Service | `IDataverseRepository`, `DataverseEntityMetadataRegistry`, `ILogger` | 3 view models |
| `GRCFinancialControl.Persistence.Services.People.DataversePersonDirectory` | Shared Dataverse person lookup for both apps. | Infrastructure / Service | `IDataverseRepository`, `DataverseEntityMetadataRegistry`, `ILogger` | Financial + Invoice apps |
| `GRCFinancialControl.Persistence.Services.Dataverse.DataverseRepository` | Manages `ServiceClient` lifetime for Dataverse calls. | Infrastructure | `IDataverseServiceClientFactory` | 3 services |
| `Invoices.Data.Repositories.InvoicePlanRepository` | Persists invoice plans/items and status transitions. | Infrastructure / Repository | `IDbContextFactory<ApplicationDbContext>`, `ILogger`, `IPersonDirectory` | 5 workflows |
| `InvoicePlanner.Avalonia.Services.GlobalErrorHandler` | Centralized unhandled exception capture routed to dialogs. | UI / Service | `ILogger`, `IErrorDialogService`, Avalonia lifetime | Entire Invoice UI |
| `GRCFinancialControl.Avalonia.ViewModels.MainWindowViewModel` | Navigation shell + dialog coordination for the financial UI. | UI / ViewModel | Multiple feature view models, `IMessenger` | 9 sections |
| `InvoicePlanner.Avalonia.ViewModels.MainWindowViewModel` | Navigation shell for invoice planner flows. | UI / ViewModel | Plan/Request/Emission view models | 3 sections |

## Duplication Candidates

| Candidate | Duplicate Of | Reason | Proposed Canonical |
| --- | --- | --- | --- |
| `DataverseCustomerService` | `CustomerService` | Implements the same domain logic with storage-specific plumbing. | Shared `CustomerService` delegating to an injected data-store strategy. |
| `DataverseManagerService` | `ManagerService` | Identical manager CRUD split only by backend. | Shared `ManagerService` over a backend strategy abstraction. |
| `InvoicePlanner.Avalonia.App.ResolveBackendPreference` | `GRCFinancialControl.Avalonia.App.ResolveBackendPreference` | Near-identical backend preference resolution code in both apps. | Extract `BackendPreferenceResolver` shared helper. |
| `InvoicePlanner.Avalonia.App.ResolveDataverseOptions` | `GRCFinancialControl.Avalonia.App.ResolveDataverseOptions` | Duplicated Dataverse option assembly logic. | Shared `DataverseOptionsProvider`. |
| `InvoicePlanner.Avalonia.Services.ErrorDialogService` | `GRCFinancialControl.Avalonia.Services.DialogService` | Two dialog orchestration stacks with overlapping lifetime/closing flows. | Shared dialog host abstraction with themed implementations. |

The `.tmp` working files were generated during the audit and deleted per instructions.
