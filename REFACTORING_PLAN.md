# GRC Financial Control - Refactoring Plan

## Issue Analysis

### Issue A: PAPD Import Bug
**Problem**: PAPDs are being imported from cells U13 and V13 (header cells) instead of from each engagement row's "Engagement Partner GUI" column (AO).

**Root Cause**: The fallback column resolution logic in `FullManagementDataImporter.ResolveColumnIndexes()` searches for partial header matches when the expected column header is not found. This causes it to match against any column header containing keywords like "partner" or "delegate".

**Solution**: 
1. Make column resolution stricter - only use fallback for optional columns if data exists
2. Add validation to warn when falling back to non-expected columns
3. Ensure partner/manager data is read from engagement row-specific columns (AO/AZ), not header cells

---

## Phase 1: Fix PAPD Import Bug

### Files to Modify:
- `GRCFinancialControl.Persistence/Services/Importers/FullManagementDataImporter.cs`

### Changes:
1. Modify `TryResolveAliasColumn()` to be stricter about column matching for assignment fields
2. Add logging to identify when fallback columns are being used
3. Consider requiring partner/manager columns to be in expected locations or provide explicit mapping

---

## Phase 2: Major Refactoring

### Objective: 
Reduce compilation time, improve maintainability, and separate concerns by splitting the monolithic solution.

### Current Structure Problems:
- One large solution with multiple loosely-related projects (GRC Financial Control, Invoice/Invoice Planner, GRC Shared)
- GRC.Shared is a Git submodule - hard to iterate and maintain
- Full rebuild takes significant time due to project interdependencies
- Difficult to work on individual features without rebuilding everything

### Proposed Architecture:

```
GitHub
├── GRC.Shared (Library)
│   ├── GRC.Shared.Core (Business models, interfaces)
│   ├── GRC.Shared.UI (Avalonia controls, converters, behaviors)
│   ├── GRC.Shared.Resources (Localization, assets)
│   └── Outputs: NuGet package or DLL
│
├── GRCFinancialControl (Solution)
│   ├── GRCFinancialControl.Core
│   ├── GRCFinancialControl.Persistence
│   ├── GRCFinancialControl.Avalonia (UI)
│   ├── GRCFinancialControl.Tests
│   └── References: GRC.Shared.dll
│
└── InvoicePlanner (Solution)
    ├── Invoices.Core
    ├── Invoices.Data
    ├── InvoicePlanner.Avalonia (UI)
    └── References: GRC.Shared.dll
```

### Implementation Steps:

#### Step 1: Identify Shared Code
- [ ] Audit `GRC.Shared` for code used by both GRC Financial Control and Invoice Planner
- [ ] Move any missing shared components to `GRC.Shared`
- [ ] Common items: Localization, UI components, base interfaces, shared enums

#### Step 2: Prepare GRC.Shared for Distribution
- [ ] Update GRC.Shared project files to generate proper DLLs
- [ ] Create build configuration for Release builds
- [ ] Test building as standalone library
- [ ] Commit to GitHub (ensure clean history)

#### Step 3: Create Separate Solutions
- [ ] Create `GRCFinancialControl.sln` (without Invoice projects)
- [ ] Create `InvoicePlanner.sln` (only Invoice projects)
- [ ] Both solutions reference GRC.Shared as external DLL

#### Step 4: Update Project References
- [ ] Replace GRC.Shared project references with DLL references
- [ ] Update AssemblyBinding paths
- [ ] Verify all namespaces remain intact
- [ ] Test full build in each solution

#### Step 5: Cleanup & Publish
- [ ] Delete old monolithic solution structure
- [ ] Push three separate repositories/solutions to GitHub
- [ ] Update documentation with new build instructions
- [ ] Archive old solution version

### Benefits:
- **Faster Compilation**: Each solution only builds what it needs
- **Independent Development**: Changes to one solution don't affect the other
- **Easier Maintenance**: Clearer separation of concerns
- **Faster CI/CD**: Only relevant tests run for each solution
- **Better Dependency Management**: GRC.Shared is properly versioned

### Timeline Estimate:
- Phase 1 (Bug Fix): 1-2 hours
- Phase 2 (Refactoring): 3-4 hours
  - Step 1: 30 mins
  - Step 2: 30 mins
  - Step 3: 1 hour
  - Step 4: 1 hour
  - Step 5: 30 mins

---

## Next Actions

1. **Immediate**: Implement Phase 1 fix for PAPD import
2. **Short-term**: Complete Phase 2 refactoring
3. **Post-refactoring**: Update CI/CD pipelines, documentation, and team processes
