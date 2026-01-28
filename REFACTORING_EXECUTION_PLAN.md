# Clean Architecture Refactoring - Execution Plan

## Discovery Summary

### Critical Finding: **Entity Duplication**
Both `GRCFinancialControl.Core/Models` and `Invoices.Core/Models` contain **IDENTICAL entity files**:
- Engagement, Customer, Employee, Manager, Papd
- ClosingPeriod, FiscalYear, FinancialEvolution
- EngagementRankBudget, Allocations, etc.

**Current State**: Two copies of same entities maintained separately
**Target State**: Single source of truth in `GRC.Shared.Core`

---

## Refactoring Strategy

### Phase 1: Entity Consolidation
**Move shared domain entities to GRC.Shared.Core:**

#### Shared Entities (Both Apps):
```
GRC.Shared.Core/Models/
├── Core/
│   ├── Engagement.cs
│   ├── Customer.cs
│   ├── Employee.cs
│   ├── Manager.cs
│   ├── Papd.cs
│   ├── Setting.cs
│   └── ConnectionTestResult.cs
├── Financial/
│   ├── ClosingPeriod.cs
│   ├── FiscalYear.cs
│   ├── FinancialEvolution.cs
│   ├── EngagementFiscalYearRevenueAllocation.cs
│   └── ActualsEntry.cs
├── Allocations/
│   ├── HoursAllocationSnapshot.cs
│   ├── HoursAllocationRowSnapshot.cs
│   ├── HoursAllocationCellSnapshot.cs
│   ├── HoursAllocationRowAdjustment.cs
│   ├── HoursAllocationCellUpdate.cs
│   ├── PlannedAllocation.cs
│   └── FiscalYearAllocationInfo.cs
├── Assignments/
│   ├── EngagementManagerAssignment.cs
│   ├── EngagementPapd.cs
│   ├── EngagementRankBudget.cs
│   └── EngagementRankBudgetHistory.cs
└── Lookups/
    ├── RankMapping.cs
    ├── RankOption.cs
    └── EngagementLookup.cs
```

#### GRC-Specific Entities (Keep in GRCFinancialControl.Core):
```
GRCFinancialControl.Core/Models/
├── Reporting/  (Dashboard/Analytics specific to GRC)
│   ├── BacklogData.cs
│   ├── EngagementPerformanceData.cs
│   ├── FinancialEvolutionPoint.cs
│   ├── FiscalPerformanceData.cs
│   ├── PapdContributionData.cs
│   ├── PlannedVsActualData.cs
│   ├── StrategicKpiData.cs
│   └── TimeAllocationData.cs
├── Enums/  (Keep all enums here or move to Shared)
├── Exceptions/
└── Extensions/
```

#### Invoice-Specific Entities (Keep in Invoices.Core):
```
Invoices.Core/Models/
├── InvoiceEmission.cs
├── InvoiceEmissionCancellation.cs
├── InvoiceEmissionUpdate.cs
├── InvoiceItem.cs
├── InvoiceNotificationPreview.cs
├── InvoicePlan.cs
├── InvoicePlanEmail.cs
├── InvoicePlanSummary.cs
├── InvoiceRequestUpdate.cs
├── InvoiceSummaryFilter.cs
├── InvoiceSummaryGroup.cs
├── InvoiceSummaryItem.cs
├── InvoiceSummaryResult.cs
├── MailOutbox.cs
├── MailOutboxLog.cs
├── EngagementAdditionalSale.cs
├── EngagementForecastUpdateEntry.cs
├── ExceptionEntry.cs
└── FiscalYearCloseResult.cs
```

---

## Phase 2: Persistence Layer Split

### Create Two Separate DbContexts

#### GRCFinancialControl.Persistence → Keep `ApplicationDbContext`
- Focuses on GRC-specific operations
- References: GRC.Shared.Core (shared entities) + GRCFinancialControl.Core (reporting models)

#### Invoices.Data → Create `InvoiceDbContext`
- Focuses on Invoice-specific operations
- References: GRC.Shared.Core (shared entities) + Invoices.Core (invoice models)

**Both contexts point to SAME database** - just different focus areas.

---

## Phase 3: Merge App.Presentation into GRC.Shared.UI

**Move from App.Presentation to GRC.Shared.UI:**
```
GRC.Shared.UI/
├── Services/
│   ├── BaseDialogService.cs
│   ├── FilePickerService.cs
│   ├── ToastService.cs
│   ├── CurrencyDisplayHelper.cs
│   ├── CurrencyFormatInfo.cs
│   ├── InvoiceDescriptionFormatter.cs
│   └── ReadmeContentProvider.cs
├── Converters/
│   └── CurrencyDisplayConverter.cs
├── Localization/
│   ├── LocalizationCultureManager.cs
│   ├── LocalizationLanguageOptions.cs
│   └── LocalizationRegistry.cs
└── Messages/
    └── ApplicationRestartRequestedMessage.cs
```

**Delete App.Presentation project entirely after migration.**

---

## Phase 4: Build GRC.Shared as Standalone Library

### Update GRC.Shared.sln
- Ensure GRC.Shared.Core, GRC.Shared.Resources, GRC.Shared.UI build independently
- Configure Release builds to output to `/lib` folder
- Test compilation without parent solution

---

## Phase 5: Create Separate Solutions

### GRCFinancialControl.sln (New)
```
GRCFinancialControl.sln
├── GRCFinancialControl.Core (Project)
├── GRCFinancialControl.Persistence (Project)
├── GRCFinancialControl.Avalonia (Project)
├── GRCFinancialControl.Avalonia.Tests (Project)
└── References:
    ├── GRC.Shared.Core.dll
    ├── GRC.Shared.Resources.dll
    └── GRC.Shared.UI.dll
```

### InvoicePlanner.sln (New)
```
InvoicePlanner.sln
├── Invoices.Core (Project)
├── Invoices.Data (Project)
├── InvoicePlanner.Avalonia (Project)
└── References:
    ├── GRC.Shared.Core.dll
    ├── GRC.Shared.Resources.dll
    └── GRC.Shared.UI.dll
```

---

## Execution Steps

### Step 1: Backup & Branch ✅
```bash
git checkout -b refactor/clean-architecture
git add .
git commit -m "Checkpoint before refactoring"
```

### Step 2: Move Entities to GRC.Shared.Core
1. Create folder structure in GRC.Shared.Core
2. Copy shared entities from GRCFinancialControl.Core
3. Update namespaces to `GRC.Shared.Core.Models.*`
4. Build GRC.Shared.sln to verify

### Step 3: Update References - GRCFinancialControl
1. Add reference to GRC.Shared.Core
2. Delete duplicate entities from GRCFinancialControl.Core
3. Update using statements throughout solution
4. Fix compilation errors

### Step 4: Update References - Invoices
1. Add reference to GRC.Shared.Core  
2. Delete duplicate entities from Invoices.Core
3. Update using statements
4. Fix compilation errors

### Step 5: Merge App.Presentation → GRC.Shared.UI
1. Copy files to GRC.Shared.UI
2. Update namespaces
3. Update project references
4. Delete App.Presentation project
5. Build GRC.Shared.sln

### Step 6: Create New Solution Files
1. Create GRCFinancialControl.sln (without Invoice projects)
2. Create InvoicePlanner.sln (without GRC projects)
3. Test both build independently

### Step 7: Final Validation
1. Build GRC.Shared.sln → Generate DLLs
2. Build GRCFinancialControl.sln
3. Build InvoicePlanner.sln
4. Run tests for both
5. Commit changes

---

## Timeline Estimate

| Step | Task | Time |
|------|------|------|
| 1 | Backup & Branch | 5 min |
| 2 | Move entities to GRC.Shared.Core | 30 min |
| 3 | Update GRCFinancialControl references | 45 min |
| 4 | Update Invoices references | 45 min |
| 5 | Merge App.Presentation | 30 min |
| 6 | Create solution files | 20 min |
| 7 | Final validation & testing | 30 min |
| **Total** | | **~3.5 hours** |

---

## Success Criteria

- ✅ Zero entity duplication
- ✅ GRC.Shared builds independently
- ✅ GRCFinancialControl.sln builds without Invoice projects
- ✅ InvoicePlanner.sln builds without GRC projects
- ✅ All tests pass
- ✅ Both apps can run independently
- ✅ Shared entities in single location (GRC.Shared.Core)

---

## Risk Mitigation

**Risk**: Breaking changes during entity migration
**Mitigation**: Work in feature branch, test incrementally

**Risk**: Namespace conflicts
**Mitigation**: Use global search/replace carefully

**Risk**: Build order dependencies
**Mitigation**: Build GRC.Shared first, then applications

---

## Next Action

**Ready to proceed?** I'll start with Step 1 (Backup & Branch) and Step 2 (Move entities).
