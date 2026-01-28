# Full Management Data Import - Field Mapping & Headers

**File**: FullManagementDataImporter.cs  
**Header Row**: Row 11 (0-based index = 10)  
**Total Fields**: 28 fields imported (exact header names from Excel file)  
**Last Updated**: Property renaming, exact header matching, and Next ETC Date calculation completed

---

## Field Mapping Table - EXACT HEADER NAMES ONLY

| # | FieldName | Exact Header Name (Row 11) | Column | Required |
|---|-----------|----------------------------|--------|----------|
| 1 | EngagementId | **Engagement ID** | A | ✅ YES |
| 2 | EngagementDescription | **Engagement** | B | ❌ NO |
| 3 | CustomerName | **Client** | BK | ❌ NO |
| 4 | CustomerCode | **Client ID** | BI | ❌ NO |
| 5 | OpportunityCurrency | **Opportunity Currency** | FX | ❌ NO |
| 6 | OriginalBudgetHours | **Original Budget Hours** | HL | ❌ NO |
| 7 | OriginalBudgetTer | **Original Budget TER** | JN | ❌ NO |
| 8 | OriginalBudgetMarginPercent | **Original Budget Margin %** | HW | ❌ NO |
| 9 | OriginalBudgetExpenses | **Original Budget Expenses** | HQ | ❌ NO |
| 10 | ChargedHours | **Charged Hours ETD** | CI | ❌ NO |
| 11 | ChargedHoursFytd | **Charged Hours FYTD** | CJ | ❌ NO |
| 12 | TermMercuryProjectedOppCurrency | **TER Mercury Projected Opp Currency** | FP | ❌ NO |
| 13 | ValueData | **TER ETD** | CU | ❌ NO |
| 14 | TerFiscalYearToDate | **TER FYTD** | CV | ❌ NO |
| 15 | MarginPercentEtd | **Margin % ETD** | CG | ❌ NO |
| 16 | MarginPercentFytd | **Margin % FYTD** | CH | ❌ NO |
| 17 | ExpensesEtd | **Expenses ETD** | DH | ❌ NO |
| 18 | ExpensesFytd | **Expenses FYTD** | DI | ❌ NO |
| 19 | StatusText | **Engagement Status** | AH | ❌ NO |
| 20 | EngagementPartnerGUI | **Engagement Partner GUI** | AM | ❌ NO |
| 21 | EngagementPartner | **Engagement Partner** | AN | ❌ NO |
| 22 | EngagementManagerGUI | **Engagement Manager GUI** | AY | ❌ NO |
| 23 | EngagementManager | **Engagement Manager** | BA | ❌ NO |
| 24 | EtcAgeDays | **ETC-P Age** | FB | ❌ NO |
| 25 | UnbilledRevenueDays | **Unbilled Revenue Days** | GA | ❌ NO |
| 26 | LastActiveEtcPDate | **Last Active ETC-P Date** | EZ | ❌ NO |
| 27 | CurrentFiscalYearBacklog | **FYTG Backlog** | GR | ❌ NO |
| 28 | FutureFiscalYearBacklog | **Future FY Backlog** | GS | ❌ NO |

**Note**: **Next ETC Date** is NOT imported - it's calculated automatically as `LastActiveEtcPDate + 1 month`

---

## Property Renaming Summary

**Assignment Fields - Corrected Naming:**

| Old Property Names | New Property Names | Source Header | Meaning |
|---|---|---|---|
| `PartnerGuiIds` (IReadOnlyList) | `EngagementPartner` (string) | "Engagement Partner" | Partner name/identifier from column AN |
| `PartnerGuiCode` (string) | `EngagementPartnerGUI` (string) | "Engagement Partner GUI" | Partner numeric GUI code from column AM |
| `ManagerGuiIds` (IReadOnlyList) | `EngagementManager` (string) | "Engagement Manager" | Manager name/identifier from column BA |
| `ManagerGuiCode` (string) | `EngagementManagerGUI` (string) | "Engagement Manager GUI" | Manager numeric GUI code from column AY |

**Rationale:**
- Property names now match the actual Excel column headers
- Numeric identifiers (GUI codes) have "GUI" suffix
- Text identifiers (names) do not have GUI suffix
- Type changed from IReadOnlyList to string for direct mapping
- Search now uses EXACT header match only, no aliases

---

## Header Search - EXACT MATCH ONLY

**Search Strategy:**
1. Search row 11 for exact header name match (case-insensitive)
2. NO partial matches allowed
3. NO alias searching
4. Example: Searching for "Engagement Partner" will NOT match "Engagement Partner GUI"

**Header Arrays Updated:**
```csharp
// Old: Multiple aliases (fuzzy matching - DEPRECATED)
PartnerGuiCodeHeaders = { "engagement partner delegate", "partner gui code", "partner code", ... }

// New: EXACT header only
EngagementPartnerGUIHeaders = { "engagement partner gui" }
EngagementPartnerHeaders = { "engagement partner" }
EngagementManagerGUIHeaders = { "engagement manager gui" }
EngagementManagerHeaders = { "engagement manager" }
```

---

## Impact on Data Import

**Before (Fuzzy Matching - Data Corruption):**
- Search for "partner code" could match wrong columns
- Hardcoded column positions checked first, caused fallbacks
- Multiple rows read header values instead of per-row values
- Result: All engagements getting same partner (Natalia Yasuda)

**After (Exact Matching - Correct Data):**
- Only exact header match accepted
- Each row reads from correct column
- Numeric GUI and name values stored separately
- Result: Each engagement gets correct partner and manager assignments

---

## Build Status

✅ Solution compiled successfully  
✅ 0 Warnings  
✅ 0 Errors  
✅ All tests passed

---

## Calculated Fields

**Next ETC Date** is automatically calculated, not imported:
- **Formula**: `LastActiveEtcPDate + 1 month`
- **Implementation**: `CalculateProposedNextEtcDate()` method
- **Logic**: ETC-P (Estimate to Complete - Projected) should be updated monthly
- **Stored As**: `engagement.ProposedNextEtcDate`
- **Example**: If Last ETC Date = January 15, 2026 → Next ETC Date = February 15, 2026
