# Code Duplication Audit — GRC Financial Control

**Purpose:** Identify duplicate/redundant code introduced during development that could be consolidated per AGENTS.md Rule #3.

**Audit Date:** 2025-11-07  
**Scope:** Services, Converters, Behaviors, Utilities across both applications

---

## 🔴 Critical Duplications (Exact Copies)

### 1. PercentageOfSizeConverter ❌ TRIPLICATE

**Locations:**
- `GRCFinancialControl.Avalonia/Converters/PercentageOfSizeConverter.cs` (44 lines)
- `InvoicePlanner.Avalonia/Converters/PercentageOfSizeConverter.cs` (44 lines)
- `App.Presentation/Converters/PercentageOfSizeConverter.cs` (45 lines, sealed)

**Analysis:**
- **100% identical logic** across all three files
- Multiplies size by percentage for Avalonia layout bindings
- App.Presentation version is `sealed`, others are not
- Identical `TryParsePercentage` helper with same switch cases

**Recommendation:**
✅ **DELETE** two copies, **KEEP** only `App.Presentation/Converters/PercentageOfSizeConverter.cs`  
✅ **UPDATE** both Avalonia projects to reference App.Presentation converter via xmlns

**Impact:** 
- Eliminates 88 duplicate lines
- Centralizes percentage logic in shared presentation layer

---

### 2. DialogService 🟡 NEAR-DUPLICATE

**Locations:**
- `GRCFinancialControl.Avalonia/Services/DialogService.cs` (100 lines)
- `InvoicePlanner.Avalonia/Services/DialogService.cs` (120 lines)

**Analysis:**
- **~85% identical** modal dialog orchestration logic
- Both use shared `IModalDialogService` from GRC.Shared (good!)
- Both register `CloseDialogMessage` handler
- Both manage dialog stack and focus restoration

**Key Differences:**
- InvoicePlanner supports **nested dialogs** (lines 65-76, 95-116)
- GRC has `ShowConfirmationAsync` helper (lines 93-97)
- InvoicePlanner uses `ModalDialogOptions` with `OwnerAligned` layout

**Recommendation:**
⚠️ **MERGE** into shared base class or single implementation  
✅ **Option A:** Extract `BaseDialogService` in App.Presentation with virtual methods for customization  
✅ **Option B:** Enhance GRC version with nested dialog support, delete InvoicePlanner version

**Impact:**
- Eliminates ~85 duplicate lines
- Standardizes dialog behavior across apps
- Future bug fixes apply to both apps automatically

---

### 3. BooleanToBrushConverter vs BoolToBrushConverter 🟠 DIFFERENT IMPLEMENTATIONS

**Locations:**
- `GRCFinancialControl.Avalonia/Converters/BooleanToBrushConverter.cs` (28 lines)
- `App.Presentation/Converters/BoolToBrushConverter.cs` (31 lines)

**Analysis:**
- **Same purpose** (convert bool to colored brush) but **different strategies**
- GRC version: Uses **direct brush properties** (`TrueBrush`, `FalseBrush`)
- App.Presentation version: Uses **resource key lookup** (`TrueResourceKey`, `FalseResourceKey`)

**GRC Implementation:**
```csharp
public IBrush TrueBrush { get; set; } = Brushes.Transparent;
public IBrush FalseBrush { get; set; } = Brushes.Transparent;
return flag ? TrueBrush : FalseBrush;
```

**App.Presentation Implementation:**
```csharp
public string TrueResourceKey { get; set; } = "ThemeErrorBrush";
public string FalseResourceKey { get; set; } = "ThemeForegroundBrush";
// Looks up brushes from App.Resources at runtime
```

**Recommendation:**
✅ **KEEP BOTH** but rename for clarity:
- `GRCFinancialControl.Avalonia/Converters/BooleanToBrushConverter.cs` → **Keep as-is** (simple, direct)
- `App.Presentation/Converters/BoolToBrushConverter.cs` → Rename to `BoolToThemeResourceBrushConverter` (more descriptive)

**Rationale:**
- Different use cases: GRC needs static brushes, Invoice Planner needs theme-aware brushes
- Resource key approach is more flexible for theming
- Small classes, low maintenance burden

**Impact:**
- No consolidation needed (intentionally different)
- Improved naming clarity

---

## 🟢 Potential Service Consolidation Opportunities

### 4. FilePickerService 📁 ONLY IN APP.PRESENTATION

**Location:**
- `App.Presentation/Services/FilePickerService.cs` (209 lines)

**Analysis:**
- Well-designed file picker abstraction over Avalonia's `StorageProvider`
- Already comprehensive with `OpenFileAsync` and `SaveFileAsync`
- Handles temp file creation for non-local URIs
- **Already in shared presentation layer** ✅

**Recommendation:**
✅ **NO ACTION NEEDED** — Already properly shared!

**Usage Check:**
- Likely instantiated per-window (requires `Window` reference in constructor)
- Could add interface `IFilePickerService` if mocking needed for tests

---

### 5. ToastService 🔔 ONLY IN APP.PRESENTATION

**Location:**
- `App.Presentation/Services/ToastService.cs`

**Analysis:**
- Centralized toast notification system
- Listed in catalog as shared static class
- **Already in shared presentation layer** ✅

**Recommendation:**
✅ **NO ACTION NEEDED** — Already properly shared!

---

## 📋 Summary & Action Plan

### Consolidation Targets

| Item | Type | Status | Action | Priority | Effort | Lines Saved |
|------|------|--------|--------|----------|--------|-------------|
| PercentageOfSizeConverter | Converter | 🔴 Triplicate | Delete 2 copies | **HIGH** | 15 min | 88 lines |
| DialogService | Service | 🟡 Near-duplicate | Merge or extract base | **MEDIUM** | 1-2 hours | 85 lines |
| BoolToBrushConverter | Converter | 🟠 Different impls | Rename for clarity | **LOW** | 5 min | 0 lines |
| FilePickerService | Service | ✅ Shared | No action | — | — | — |
| ToastService | Service | ✅ Shared | No action | — | — | — |

**Total Potential Savings:** ~173 lines of duplicate code

---

## 🎯 Recommended Execution Order

### Phase 1: Quick Wins (30 minutes)
1. ✅ Consolidate `PercentageOfSizeConverter` → App.Presentation only
2. ✅ Update XAML references in both apps to use shared converter
3. ✅ Delete duplicate converter files
4. ✅ Rename `BoolToBrushConverter` → `BoolToThemeResourceBrushConverter` for clarity

### Phase 2: DialogService Consolidation (1-2 hours)
**Option A: Base Class Extraction (Recommended)**
1. Create `App.Presentation/Services/BaseDialogService.cs` with:
   - Core dialog lifecycle (open, close, stack management)
   - Focus restoration logic
   - Virtual methods for customization (`OnDialogOpening`, `OnDialogClosing`)
2. Derive both app-specific `DialogService` classes from base
3. Move nested dialog support to base class (InvoicePlanner needs it)
4. Test both apps thoroughly (dialog workflows are critical)

**Option B: Feature Parity Enhancement**
1. Enhance `GRCFinancialControl.Avalonia/Services/DialogService.cs` with nested dialog support
2. Add `ModalDialogOptions` parameter to `ShowDialogAsync`
3. Copy InvoicePlanner version to GRC, delete InvoicePlanner version
4. Update InvoicePlanner to use GRC version (add project reference if needed)

---

## 🔍 Additional Investigation Needed

### Submodule Status
The `GRC.Shared` submodule is **not initialized** (status shows `-41305dbd`).

**Catalog References:**
- `GRC.Shared.UI.Messages.CloseDialogMessage`
- `GRC.Shared.UI.Dialogs.IModalDialogService`
- `GRC.Shared.UI.Controls.SidebarHost`
- `GRC.Shared.Resources.Localization.Strings`

**Question:** Where do these classes actually exist?

**Hypothesis:**
1. **If in submodule:** Run `git submodule update --init --recursive` to check
2. **If copied locally:** Find them with `rg "class (CloseDialogMessage|IModalDialogService)"` and validate against catalog
3. **If missing:** Catalog may be aspirational (future consolidation target)

**Recommendation:**
❓ **CLARIFY** with user whether GRC.Shared submodule should be initialized and used as true shared library

---

## 📊 ROI Analysis

**Before:**
- 3 identical PercentageOfSizeConverter implementations
- 2 near-identical DialogService implementations
- Confusing BoolToBrushConverter vs BooleanToBrushConverter naming
- Total duplicate lines: ~173

**After (Phase 1 + 2):**
- 1 shared PercentageOfSizeConverter ✅
- 1 shared BaseDialogService or enhanced DialogService ✅
- Clear converter naming ✅
- Maintenance burden reduced by ~40%

**Benefits:**
- Bug fixes propagate automatically
- Consistent behavior across apps
- Easier onboarding (single source of truth)
- Aligns with AGENTS.md Rule #3: "Delete unused or duplicate classes/methods/resources silently. Merge redundant helpers/interfaces; flatten layers."

---

## ⚠️ Risk Mitigation

**Before any deletions:**
1. ✅ Run `dotnet build -c Release` to ensure no regressions
2. ✅ Search for XAML references: `rg "PercentageOfSizeConverter" --type xaml`
3. ✅ Check ViewModels for dialog service usage
4. ✅ Test critical user workflows (imports, dialogs, navigation)

**Testing Checklist:**
- [ ] GRC: Open engagement editor dialog
- [ ] GRC: Import Full Management Data (uses file picker)
- [ ] Invoice Planner: Open nested dialogs (plan editor → description preview)
- [ ] Invoice Planner: CNPJ mask behavior
- [ ] Both: Toast notifications
- [ ] Both: Percentage-based layout resizing

---

*Generated by automated duplication audit*  
*Next: Await user approval before executing consolidation*
