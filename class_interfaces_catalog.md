# Class & Interface Catalog — GRC Financial Control (vNext)

Authoritative reference for reusable classes and interfaces. Consult this catalog **before** introducing new types and update it immediately after creating, renaming, or removing classes/interfaces.

---

## Usage Rules
1. Always review this catalog prior to adding a class/interface to confirm whether an existing type can be reused, extended, or versioned.
2. Prefer incremental improvements to the existing types listed here instead of creating near-duplicates.
3. When new functionality is introduced, update this catalog in the same change set describing the new classes/interfaces.
4. Document only metadata (purpose, key members, dependencies); do not include implementation code snippets.

---

## Core Services
| Type | Namespace | Name | Purpose | Key Members | Dependencies | Status | Notes |
|------|-----------|------|---------|-------------|--------------|--------|-------|
| Class | `Invoices.Core.Models` | `InvoiceItem` | Billing plan line containing scheduling, request, and emission state. | `SeqNo`, `Status`, `Emissions`, `Amount` | `InvoicePlan`, `InvoiceEmission` | Stable | Status flow: Planned → Requested → Emitted/Planned (after cancellation). |
| Class | `Invoices.Core.Models` | `InvoiceEmission` | Persists a single emission attempt for an invoice item. | `BzCode`, `EmittedAt`, `CanceledAt`, `CancelReason` | `InvoiceItem` | New | Maintains history of BZ codes and cancellation reasons. |
| Class | `Invoices.Core.Models` | `InvoiceEmissionCancellation` | DTO instructing the repository to cancel an active emission. | `ItemId`, `CancelReason`, `CanceledAt` | `InvoicePlanRepository` | New | Generates cancellation entries and reopens the invoice for planning. |
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `IImportService` | Entry point for Excel imports (budget, staffing, revenue). | `ImportBudgetAsync`, `ImportFullManagementAsync`, `ImportFiscalControlAsync` | `ApplicationDbContext`, ExcelDataReader, `IFiscalCalendarConsistencyService` | Stable | Use for orchestrating file ingestion. |
| Class | `GRCFinancialControl.Persistence.Services` | `ImportService` | Implements import orchestration, header validation, and persistence. | Methods above plus helpers like `ParseResourcing`, `AggregateRankBudgets` | `IDbContextFactory<ApplicationDbContext>`, `IFiscalCalendarConsistencyService`, `ILoggerFactory` | Stable | Handles customer/engagement upsert with EF execution strategy. |
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `IFullManagementDataImporter` | Specialized importer for Full Management Data workbook. | `ImportAsync` | `ApplicationDbContext`, ExcelDataReader | Stable | Enforces header/metadata validation rules. |
| Class | `GRCFinancialControl.Persistence.Services.Importers` | `FullManagementDataImporter` | Parses management workbook and persists engagement metrics & allocations. | `ImportAsync`, `ParseRows`, `ExtractReportMetadata` | `IDbContextFactory<ApplicationDbContext>`, `ILogger` | Stable | Provides culture-aware numeric/date parsing helpers. |
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `IHoursAllocationService` | Allocation workspace contract for fetching/saving rank budgets. | `GetAllocationAsync`, `SaveAsync`, `AddRankAsync`, `DeleteRankAsync` | `ApplicationDbContext` | Stable | Throws when fiscal years are locked or budgets missing. |
| Class | `GRCFinancialControl.Persistence.Services` | `HoursAllocationService` | Applies business rules for allocation edits and traffic-light recalculation. | Same as interface + `BuildRows` | `IDbContextFactory<ApplicationDbContext>` | Stable | Aggregates adjustments by normalized rank. |
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `IPlannedAllocationService` | Contract to manage planned allocation records per engagement. | `GetAllocationsForEngagementAsync`, `SaveAllocationsForEngagementAsync` | `ApplicationDbContext` | Stable | Validates fiscal-year lock state before persistence. |
| Class | `GRCFinancialControl.Persistence.Services` | `PlannedAllocationService` | Implements guarded save/load for planned allocations. | Methods above | `EngagementMutationGuard`, `IDbContextFactory<ApplicationDbContext>` | Stable | Blocks insert/update/delete on locked fiscal years. |
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `IRetainTemplateGenerator` | Generates Retain upload templates based on allocation data. | `GenerateRetainTemplateAsync` | ClosedXML, `ILogger` | Stable | Produces Excel copies at the caller-selected destination path. |
| Class | `GRCFinancialControl.Persistence.Services.Exporters` | `RetainTemplateGenerator` | Clones template asset, builds headers, and populates weekly allocations. | Methods above + `PopulateTemplate`, `LoadTemplateBytes` | ClosedXML, `ILogger` | Stable | Validates template sheet existence. |
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `IReportService` | Exposes reporting endpoints (dashboards). | `GetEngagementSummaryAsync`, `GetBudgetConsumptionAsync` | `ApplicationDbContext` | Stable | Returns projection DTOs for Avalonia dashboards. |
| Class | `GRCFinancialControl.Persistence.Services` | `ReportService` | Implements reporting queries over MySQL views. | Methods above | `IDbContextFactory<ApplicationDbContext>` | Stable | Uses AsNoTracking projections for performance. |

---

## Supporting Infrastructure
| Type | Namespace | Name | Purpose | Key Members | Dependencies | Status | Notes |
|------|-----------|------|---------|-------------|--------------|--------|-------|
| Static Class | `GRCFinancialControl.Persistence.Services.Infrastructure` | `EngagementMutationGuard` | Ensures engagements can be mutated before save operations. | `EnsureCanMutateAsync` | `ApplicationDbContext` | Stable | Throws descriptive exceptions when engagements locked. |
| Class | `GRCFinancialControl.Persistence.Services.Importers` | `WorksheetValueHelper` | Utility for reading normalized values from Excel worksheets. | `NormalizeWhitespace`, `TryGetCellString`, `SanitizeNumericString` | ExcelDataReader | Stable | Shared by importers to align parsing rules. |
| Class | `GRCFinancialControl.Persistence.Services.Importers` | `FullManagementDataImportResult` | Encapsulates outcomes of the management import. | Properties for counts, warnings, errors | — | Stable | Surfaced to UI via summaries. |
| Class | `GRCFinancialControl.Persistence.Services.Exporters` | `RetainTemplatePlanningWorkbook` | Loads allocation planning workbook into structured snapshot. | `Load`, `BuildSaturdayHeaders` | ClosedXML | Stable | Provides reference dates & entries for template generator. |
| Class | `GRCFinancialControl.Persistence.Services.Exporters` | `RetainTemplatePlanningSnapshot` | Holds flattened allocation entries per resource/week. | `Entries`, `ReferenceWeekStart`, `LastWeekStart` | — | Stable | Used only by template generation flow. |
| Class | `GRCFinancialControl.Persistence.Services.Importers.StaffAllocations` | `SimplifiedStaffAllocationParser` | Parses simplified staff allocation sheets. | `ParseAsync` | ExcelDataReader | Stable | Produces normalized allocation rows consumed by planner. |

---

## Domain Models (Core)
| Type | Namespace | Name | Purpose | Key Members | Dependencies | Status | Notes |
|------|-----------|------|---------|-------------|--------------|--------|-------|
| Class | `GRCFinancialControl.Core.Models` | `Engagement` | Root aggregate representing a client engagement. | `Id`, `EngagementId`, `Description`, navigation collections | EF Core | Stable | Linked to budgets, allocations, managers. |
| Class | `GRCFinancialControl.Core.Models` | `EngagementRankBudget` | Stores budget/forecast data per rank & fiscal year. | `UpdateConsumedHours`, `UpdateAdditionalHours`, `ApplyIncurredHours`, `CalculateIncurredHours` | `FiscalYear`, `TrafficLightStatus` | Stable | Central to allocation calculations. |
| Class | `GRCFinancialControl.Core.Models` | `HoursAllocationSnapshot` | DTO returned to UI for allocation grid. | `Rows`, `FiscalYearInfos`, `RankOptions` | — | Stable | Created by `HoursAllocationService`. |
| Class | `GRCFinancialControl.Core.Models` | `HoursAllocationCellUpdate` | Describes a single cell edit request. | `BudgetId`, `ConsumedHours` | — | Stable | Consumed by `SaveAsync`. |
| Class | `GRCFinancialControl.Core.Models` | `HoursAllocationRowAdjustment` | Captures rank-level adjustments. | `RankName`, `AdditionalHours` | — | Stable | Combined with cell updates for consolidation. |
| Class | `GRCFinancialControl.Core.Models` | `PlannedAllocation` | Represents planned hours for a closing period. | `EngagementId`, `ClosingPeriodId`, `AllocatedHours` | `ClosingPeriod` | Stable | Managed by `PlannedAllocationService`. |
| Class | `GRCFinancialControl.Core.Models` | `EngagementFiscalYearRevenueAllocation` | Persists fiscal year revenue/backlog metrics. | `ToGo`, `ToDate`, `FiscalYearId`, `EngagementId` | `FiscalYear` | Stable | Upserted during revenue imports. |
| Class | `GRCFinancialControl.Core.Models.Reporting` | `BudgetConsumptionKpi` | DTO for budget consumption dashboards. | `EngagementId`, `RankName`, `ConsumedHours`, `RemainingHours` | — | Stable | Populated via views in `ReportService`. |

---

## Interfaces (Auxiliary)
| Type | Namespace | Name | Purpose | Key Members | Dependencies | Status | Notes |
|------|-----------|------|---------|-------------|--------------|--------|-------|
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `IFiscalCalendarConsistencyService` | Validates calendar data before imports. | `EnsureConsistencyAsync` | `ApplicationDbContext` | Stable | Invoked prior to budget imports. |
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `IReportService` | (See Core Services table) | — | — | Stable | Listed twice for quick lookup with services. |
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `IExceptionService` | Captures domain exceptions for diagnostics. | `LogAsync`, `GetRecentAsync` | `ApplicationDbContext` | Stable | Used by UI to surface errors. |
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `ISettingsService` | Retrieves persisted configuration. | `GetSettingsAsync`, `SaveSettingsAsync` | `ApplicationDbContext` | Stable | Supports UI configuration panels. |
| Interface | `GRCFinancialControl.Persistence.Services.Interfaces` | `IManagerAssignmentService` | Manages engagement manager assignments. | `GetAssignmentsAsync`, `SaveAssignmentsAsync` | `ApplicationDbContext` | Stable | Works with manager directory integration. |

---

## UI Utilities
| Type | Namespace | Name | Purpose | Key Members | Dependencies | Status | Notes |
|------|-----------|------|---------|-------------|--------------|--------|-------|
| Class | `InvoicePlanner.Avalonia.Behaviors` | `CnpjMaskBehavior` | Applies Brazilian CNPJ mask formatting to text inputs. | Attached property `IsEnabled` | Avalonia `TextBox` | New | Formats digits as `00.000.000/0000-00` during edits. |
| Class | `InvoicePlanner.Avalonia.Behaviors` | `DataGridRowResizeBehavior` | Enables resizing DataGrid rows via pointer drag. | Attached property `IsEnabled` | Avalonia `DataGrid`, pointer events | New | Captures pointer near row edge to adjust row height. |
| Class | `InvoicePlanner.Avalonia.Messages` | `RefreshInvoiceLinesGridMessage` | Requests that the invoice lines grid reflow its layout. | Inherits `ValueChangedMessage<bool>` | CommunityToolkit.Mvvm | New | Triggered by the invoice lines editor refresh command. |
| Class | `App.Presentation.Converters` | `BoolToBrushConverter` | Maps boolean flags to Avalonia brushes for highlighting validation states. | `Convert`, `ConvertBack` | Avalonia `IBrush`, `BindingNotification` | New | Used by invoice planner totals to switch between default and warning brushes. |
| Static Class | `Invoices.Core.Utilities` | `BusinessDayCalculator` | Normalizes invoice dates to the next business day (Mon–Fri). | `AdjustToNextBusinessDay`, `IsBusinessDay` | — | New | Weekend dates advance to Monday for emission and due date rules. |

---

> ⚠️ **Keep synchronized:** Whenever you introduce a new class/interface for a feature, append it to the appropriate section above, noting purpose and dependencies.
