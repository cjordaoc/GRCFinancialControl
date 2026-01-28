# Solution Refactoring - Dependency Analysis

## Current Structure

### Monolithic Solution: GRCFinancialControl.sln
Contains 9 projects mixing two applications:

**GRC Financial Control Projects:**
1. GRCFinancialControl.Core
2. GRCFinancialControl.Persistence  
3. GRCFinancialControl.Avalonia (UI)
4. GRCFinancialControl.Avalonia.Tests
5. App.Presentation

**Invoice Planner Projects:**
6. Invoices.Core
7. Invoices.Data
8. InvoicePlanner.Avalonia

**Shared (Submodule):**
9. GRC.Shared.Resources (from submodule)
10. GRC.Shared.UI (from submodule)

---

## Dependency Map

### GRC Financial Control Dependencies

**GRCFinancialControl.Core:**
- Independent (no project refs)
- Packages: Avalonia, Avalonia.Themes.Fluent, Microsoft.Extensions.Logging.Console

**GRCFinancialControl.Persistence:**
- → GRCFinancialControl.Core
- → Invoices.Core ⚠️ **CROSS-APPLICATION DEPENDENCY**
- Packages: Entity Framework, ClosedXML, ExcelDataReader

**GRCFinancialControl.Avalonia:**
- → GRCFinancialControl.Core
- → GRCFinancialControl.Persistence
- → App.Presentation
- → GRC.Shared.Resources
- → GRC.Shared.UI
- Packages: Avalonia full stack, WebView, Markdown

**GRCFinancialControl.Avalonia.Tests:**
- → GRCFinancialControl.Avalonia
- → GRCFinancialControl.Persistence

**App.Presentation:**
- → GRC.Shared.Resources
- → GRC.Shared.UI
- → Invoices.Core ⚠️ **CROSS-APPLICATION DEPENDENCY**

### Invoice Planner Dependencies

**Invoices.Core:**
- → GRC.Shared.Resources
- Packages: Avalonia basics

**Invoices.Data:**
- → Invoices.Core
- → GRCFinancialControl.Persistence ⚠️ **CROSS-APPLICATION DEPENDENCY**

**InvoicePlanner.Avalonia:**
- → Invoices.Core
- → Invoices.Data
- → App.Presentation
- → GRC.Shared.Resources
- → GRC.Shared.UI
- Packages: Avalonia full stack, CommunityToolkit.Mvvm

---

## Critical Issues Found

### Issue 1: Cross-Application Dependencies ⚠️
**Problem**: GRCFinancialControl and InvoicePlanner projects reference each other directly.

**Instances**:
1. `GRCFinancialControl.Persistence` → `Invoices.Core`
2. `Invoices.Data` → `GRCFinancialControl.Persistence`
3. `App.Presentation` → `Invoices.Core`

**Impact**: Cannot split solutions without breaking builds.

**Solution**: 
- Move shared business logic to `GRC.Shared.Core`
- Or: Keep database layer shared (both apps use same DB?)
- Or: Create separate persistence layers

### Issue 2: App.Presentation Ambiguity
**Problem**: `App.Presentation` is referenced by both applications but it's unclear what it contains.

**Investigation Needed**: 
- What does App.Presentation do?
- Is it truly shared UI layer?
- Can it be merged into GRC.Shared.UI?

### Issue 3: Database Sharing
**Problem**: Both applications seem to share database access through `GRCFinancialControl.Persistence`.

**Questions**:
- Do both apps use the same database?
- Are invoices part of the same domain as financial control?
- Should persistence be split or kept unified?

---

## Recommended Refactoring Approach

### Option A: Complete Separation (Clean Architecture)
**Move shared code to GRC.Shared:**
- Move common database entities to `GRC.Shared.Core`
- Create `GRC.Shared.Persistence` for shared data access
- Each app has its own specific persistence layer
- GRC.Shared becomes proper shared library

**Pros**: True independence, clear boundaries
**Cons**: More refactoring work, potential code duplication

### Option B: Shared Database Layer (Pragmatic)
**Keep persistence unified:**
- Both apps use `GRCFinancialControl.Persistence` as DLL
- Build GRC.Shared as DLL
- Create two solutions that reference same persistence DLL
- Invoice and GRC share database/entities

**Pros**: Less refactoring, realistic for tightly coupled domains
**Cons**: Not fully independent, shared DB dependency

### Option C: Hybrid Approach (Recommended)
**Phase 1 - Quick Win:**
1. Build GRC.Shared as standalone DLL
2. Move `App.Presentation` into appropriate place (merge to GRC.Shared.UI or split)
3. Create two solutions with SHARED persistence DLL
4. Document the shared dependency

**Phase 2 - Future Clean Separation:**
1. Analyze persistence coupling
2. Extract shared entities to GRC.Shared.Core
3. Split persistence layers if needed
4. Remove cross-references

---

## Next Steps

### Immediate Actions:
1. ✅ Audit complete (this document)
2. ⏳ Investigate App.Presentation purpose
3. ⏳ Analyze persistence coupling depth
4. ⏳ Decide on refactoring approach
5. ⏳ Execute chosen approach

### Questions to Answer:
- [ ] What does App.Presentation contain?
- [ ] Do both apps share the same database schema?
- [ ] Are Invoices and GRC Financial data related domains?
- [ ] Can we tolerate shared persistence DLL in both solutions?
