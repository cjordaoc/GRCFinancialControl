\# Receita × Projetos × PAPD — Functional Specification



\## 1. Purpose

Provide a single place to \*\*plan, reconcile, and monitor\*\* revenue against approved \*\*PAPD\*\* across projects and periods, highlighting risks and enabling quick, accurate reporting.



\## 2. Users \& Value

\- \*\*Delivery Managers / PMO\*\*: see health, fix mapping issues, adjust forecasts.

\- \*\*Finance Controllers\*\*: ensure recognized revenue is \*\*covered by PAPD\*\*, validate variances, and prepare governance packs.



\## 3. Scope (What it Must Do)

\- \*\*Ingest \& Normalize\*\* data from the files listed in §6.

\- Maintain a \*\*canonical schema\*\* so all sources align (§4).

\- Run \*\*reconciliation rules\*\* to verify PAPD coverage and detect variances (§5).

\- Let users update \*\*forecasts\*\* and see impacts on margin/PAPD instantly (§5.3).

\- Enforce \*\*period close/locks\*\* and a simple \*\*approval flow\*\* for plan changes (§7).

\- Provide \*\*dashboards, drilldowns, and exports\*\* (CSV/Excel; PDF optional) (§8).



\## 4. Canonical Data (Business Dictionary)

\*\*Keys\*\*: `ProjectKey`, `CustomerKey`, `PeriodKey`, plus `SourceSystem`, `LastUpdated`.



\*\*Main fields\*\*:

\- ProjectName, CustomerName, Manager, Practice  

\- PostingDate  

\- PlannedRevenue, ForecastRevenue, RecognizedRevenue  

\- PAPDApproved, PAPDConsumed, PAPDRemaining, PAPDStatus  

\- HoursPlanned, HoursActual, LaborCost, RateSet, Level  

\- DataQualityFlags (array of issues per row)



\*\*Naming alignment\*\*: any synonyms (e.g., `EngagementID`, `CodProjeto`) are mapped to `ProjectKey`.



\## 5. Core Behaviors

\### 5.1 Normalization \& Conversion Rules

\- \*\*Dates → PeriodKey\*\*: YYYY-MM using the organization’s calendar.

\- \*\*Currency\*\*: BRL, two decimals; FX policy if multi-currency arises.

\- \*\*Text hygiene\*\*: trim, collapse spaces, title-case for display names; keys are case-invariant.

\- \*\*Alias resolution\*\*: customer/project aliases mapped; unresolved items flagged.

\- \*\*Nulls\*\*: numeric blanks are zero \*\*only\*\* where business rules allow; else flagged.



\### 5.2 Reconciliation \& Health

\- \*\*Coverage\*\*: `RecognizedRevenue ≤ PAPDApproved − PAPDConsumed`; else “Revenue without PAPD”.

\- \*\*Activity gap\*\*: PAPDApproved exists with zero RecognizedRevenue \& HoursActual.

\- \*\*Period mismatch\*\*: PostingDate outside PeriodKey window.

\- \*\*Variance thresholds\*\*: Plan vs Actual, Forecast vs Plan — configurable % flags.

\- \*\*Freshness\*\*: sources older than N days flagged.



\### 5.3 Forecast \& What-Ifs

\- Adjust ForecastRevenue, hours, and level mix per project/period; immediately recalc \*\*margin\*\* and \*\*PAPDRemaining\*\*.

\- Keep \*\*baseline\*\* and \*\*version\*\* history (read-only timeline).



\## 6. Data Sources \& Responsibilities

\- \*\*Revenue actuals (recognized)\*\*: `2500829220250916220434.xlsx` (SAP export).

\- \*\*PAPD approvals/consumption \& linkage view\*\*: `Receita vs Projetos vs PAPD.xlsx` (both sheets).

\- \*\*Weekly pricing/budget targets \& rates\*\*: `EY\_WEEKLY\_PRICING\_BUDGETING...xlsx`.

\- \*\*ETC, hours, person allocations\*\*: `EY\_PERSON\_ETC\_LAST\_TRANSFERRED\_v1.xlsx`.

\- \*\*Retain allocations\*\*: `Programação Retain Platforms GRC (1).xlsx`.

\- \*\*Project roster/status (if provided)\*\*: `20140812 - Programação ERP\_v14.xlsx`.



\*\*Minimum required columns per source\*\* (business names):

\- \*\*All\*\*: ProjectKey, CustomerKey, PeriodKey(or PostingDate), SourceSystem, LastUpdated

\- \*\*Revenue\*\*: RecognizedRevenue

\- \*\*PAPD\*\*: PAPDApproved, PAPDConsumed (derive Remaining), PAPDStatus

\- \*\*Planning\*\*: PlannedRevenue or Rates × HoursPlanned

\- \*\*Forecast/ETC\*\*: ForecastRevenue or HoursETC × Rates

\- \*\*Effort\*\*: HoursActual, Level, LaborCost (or derive via Rates)



\## 7. Governance

\- \*\*Period Close\*\*: locked periods are read-only; adjustments require an auditable note.

\- \*\*Plan Changes\*\*: submit/approve workflow with a visible change log (who/when/what).



\## 8. Reporting \& Exports

\- \*\*Portfolio Dashboard\*\*: revenue, PAPD, margin, utilization, variances by Customer/Manager.

\- \*\*Project Drilldown\*\*: period trend, reconciliation flags, exceptions.

\- \*\*Exception Reports\*\*: Revenue without PAPD, Unmapped entities, Period mismatches.

\- \*\*Exports\*\*: CSV/Excel for all grids; optional PDF summary packs.



\## 9. Acceptance Criteria

\- 100% rows have ProjectKey, CustomerKey, PeriodKey (else appear in Quarantine with reasons).

\- All reconciliation rules run with zero critical blockers.

\- Export totals exactly match on-screen totals.



\## 10. Non-Goals

No time entry, billing execution, or GL posting; no external workflow engine.





