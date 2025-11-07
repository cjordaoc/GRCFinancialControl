# Correct Import Architecture Plan

## User Requirement
**FullManagementDataImporter MUST INHERIT from ImportService**
Each file type extractor = separate child class inheriting from ImportService

## Correct Hierarchy
```
ImportService (ABSTRACT BASE CLASS)
    ↓ (inherits)
    ├── FullManagementDataImporter : ImportService
    ├── BudgetImporter : ImportService
    └── AllocationPlanningImporter : ImportService
```

## Implementation Steps

### Step 1: Create Abstract ImportService Base Class
**File**: `Services/ImportServiceBase.cs`
- Contains: Common Excel loading, normalization, transaction handling
- Protected methods for child classes
- Abstract methods that children implement

### Step 2: Rename Current ImportService
**Current**: ImportService.cs (2,774 lines with all logic)
**New**: Keep as temporary reference, extract to children

### Step 3: Create Child Classes

#### FullManagementDataImporter : ImportService
- **Already exists** as separate class
- **Action**: Make it inherit from ImportService base
- **Remove**: IFullManagementDataImporter interface (use base class)

#### BudgetImporter : ImportService  
- **Extract from**: Current ImportService.ImportBudgetAsync + helpers
- **Implements**: Budget-specific import logic
- **~700 lines**

#### AllocationPlanningImporter : ImportService
- **Extract from**: Current ImportService.ImportAllocationPlanningAsync + helpers  
- **Implements**: Allocation-specific import logic
- **~500 lines**

### Step 4: Create Import Orchestrator/Facade (Optional)
If needed, create a thin facade that uses the specific importers

### Step 5: Update DI Registration
Register each importer separately:
```csharp
services.AddScoped<FullManagementDataImporter>();
services.AddScoped<BudgetImporter>();
services.AddScoped<AllocationPlanningImporter>();
```

### Step 6: Update ViewModels
Use specific importers directly:
```csharp
private readonly FullManagementDataImporter _fullMgmtImporter;
private readonly BudgetImporter _budgetImporter;
private readonly AllocationPlanningImporter _allocationImporter;
```

## Benefits of This Architecture
1. ✅ Each file type = separate, focused class
2. ✅ Easy to add new import types (create new child class)
3. ✅ Easy to remove import types (delete child class)
4. ✅ Shared functionality in base class (no duplication)
5. ✅ True inheritance as user requested

## Current Status
- ❌ Wrong architecture implemented
- ⏳ Need to refactor to correct inheritance

## Next Actions
1. Create ImportServiceBase abstract class
2. Make FullManagementDataImporter inherit from it
3. Extract BudgetImporter from current ImportService
4. Extract AllocationPlanningImporter from current ImportService
5. Update DI and ViewModels
6. Delete old ImportService implementation
