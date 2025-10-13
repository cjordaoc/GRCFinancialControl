# GRC Financial Control - Revenue x Projects x PAPD Reconciliation

This document outlines the functional specification for the GRC Financial Control application. Its purpose is to reconcile financial data from various sources, providing clear insights into performance against goals.

## 1. Goal & Scope

The primary goal of this application is to provide a single source of truth for tracking planned and actual hours for client engagements. It automates the reconciliation of budget and actuals data, attributing every hour to the responsible Partner, Associate Partner, or Director (PAPD). The system is designed for a single user responsible for financial oversight.

**Key Objectives:**
- Maintain master data for Engagements and Fiscal Years.
- Ingest budget data (planned hours) and actuals data (recognized hours) from standardized Excel files.
- Allow for the manual allocation of total planned hours across defined Fiscal Years.
- Track performance against goals by comparing planned vs. actual hours.
- Calculate future-looking backlog based on allocations to future Fiscal Years.
- Ensure 100% of data is attributed to a PAPD or flagged for review.

## Recent Updates

- The **Delete All** maintenance action now issues MySQL `TRUNCATE` statements so every business table is cleared quickly and without leaving auto-increment gaps.
- All report exports generate Excel workbooks following the `Export_<Entity>_<yyyyMMdd_HHmm>.xlsx` naming pattern. Saved files include headers, auto-fit columns, and filters.
- File pickers only accept and produce `.xlsx` files, aligning imports (e.g., `DataTemplate/data.xlsx` for margin actuals) with the supported Excel format. Revenue actuals remain a future enhancement.
- Custom UI styles have been consolidated in `Styles/Controls.axaml`, making DataGrid look-and-feel changes easier to maintain from a single resource dictionary.
- The MySQL rebuild script seeds closing periods from July 2024 through June 2031 and pre-populates fiscal years FY25–FY31 following the July-to-June calendar while leaving PAPD ownership to be defined in-app.

## 2. Personas & User Journeys

**Persona:** Financial Controller (single user)

**User Journey:**
1.  **Setup:** The user defines the Fiscal Years relevant for reporting (e.g., FY2025, FY2026).
2.  **Master Data:** The user creates or edits Engagement records, assigning a description, customer key, and an effective-dated PAPD leader.
3.  **Budget Upload:** The user uploads the `EY_WEEKLY_PRICING_BUDGETING...xlsx` file. The application extracts the total planned hours for each engagement.
4.  **Allocation:** The user navigates to the "Fiscal Year Allocation" screen. For each engagement, they distribute the total planned hours across the defined Fiscal Years. Hours allocated to future years constitute the backlog.
5.  **Actuals Upload:** On a periodic basis (e.g., monthly), the user uploads one or more `2500...xlsx` files containing actual hours worked.
6.  **Reconciliation & Reporting:** The user reviews the auto-generated reports, which compare planned vs. actual hours for the current fiscal year and display the backlog.
7.  **Maintenance:** The user manages exceptions (e.g., missing mappings) and updates the PAPD leadership on engagements as changes occur.

## 3. Data Sources & Responsibilities

| Data Source                                        | Responsibility                                                                                                     |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| **Budget File** (`EY_WEEKLY_PRICING_BUDGETING...`)   | Source of **Total Planned Hours** per Engagement. Used for initial creation or updating of engagement master data.   |
| **Actuals File** (`2500...`)                         | Source of **Recognized Actual Hours** per Engagement. The date in this file is used for Fiscal Year attribution.    |
| **In-App Engagement Master**                       | Canonical source for Engagement details (Description, CustomerKey) and the effective-dated PAPD leader mapping.      |
| **In-App Fiscal Year Configuration**               | Defines the start and end dates for each Fiscal Year, used to bucket all time-based data.                          |
| **In-App Fiscal Year Allocation**                  | User-defined allocation of total planned hours into specific Fiscal Years.                                         |

## 4. Column Mappings & Conversion Rules

| File                                               | Sheet         | Source Column(s)                  | Target Field                | Notes                                                              |
| -------------------------------------------------- | ------------- | --------------------------------- | --------------------------- | ------------------------------------------------------------------ |
| `EY_WEEKLY_PRICING_BUDGETING...xlsx`                 | `(variable)`  | `EngagementID`, `(other fields)`  | `Engagement.ID`             | Used to identify the engagement.                                   |
|                                                    |               | `Total Planned Hours` (Calculated) | `Engagement.TotalPlannedHours` | Sum of all weekly planned hours for the engagement.                |
| `2500...xlsx`                                      | `(variable)`  | `EngagementID`                    | `Actual.EngagementID`       | Used to link the actuals to an engagement.                         |
|                                                    |               | `Recognized Hours`                | `Actual.Hours`              | The actual hours to be recorded.                                   |
|                                                    |               | `Posting Date`                    | `Actual.Date`               | The date used to assign the actuals to the correct Fiscal Year.    |

## 5. Reconciliation & Validation Rules

- **100% Attribution:** Every row of data (planned and actual) must be attributed to a PAPD based on the effective-dated mapping for the relevant period. Rows that cannot be mapped are placed in an "Exceptions" queue with a clear reason (e.g., "Missing PAPD mapping for Engagement X on Date Y").
- **Allocation Validation:** The sum of hours allocated across all Fiscal Years on the "Fiscal Year Allocation" screen must exactly equal the `Total Planned Hours` read from the budget file. The UI will prevent saving an invalid allocation.
- **Data Integrity:** The sum of hours in the database for a given import batch must match the total from the source file.

## 6. Forecast & What-If Behavior

- **Backlog:** The backlog is defined as the sum of all planned hours allocated to future Fiscal Years. It is automatically calculated and updated after every change in the Fiscal Year Allocation screen.
- **No Direct What-If Scenarios:** The initial version does not support "what-if" scenarios. The forecast is a direct reflection of the user's manual allocations.

## 7. Governance & Period Close

The concept of a "Closing Period" acts as a data snapshot. When a user uploads actuals for a period (e.g., 2025-01), the application records the state of both the actuals and the current plan at that moment. This ensures that historical reports remain consistent even if the forward-looking plan is changed later.

## 8. Reporting & Exports

All reports are generated automatically and are available in the UI.

1.  **Planned vs. Actual by Fiscal Year (Hours):**
    *   **Rows:** Engagements
    *   **Columns:** Fiscal Year, Planned Hours (from allocation), Actual Hours (from actuals files), Variance.
2.  **Backlog Report (Hours):**
    *   **Rows:** Engagements
    *   **Columns:** Future Fiscal Year, Allocated Hours.
3.  **Exceptions Report:**
    *   Lists all data rows that failed validation, with a reason for each failure.

## 9. Acceptance & Quality Gates

The application is considered "Done" when:
1.  All features described in the User Journey are implemented and functional.
2.  100% of rows in any uploaded file are either processed into the system and attributed to a PAPD or land in the Exceptions report.
3.  Report totals for "Planned" and "Actual" hours can be manually traced back to the source files and allocation screen.
4.  The Backlog report accurately reflects the sum of hours allocated to future Fiscal Years.
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

\- Provide \*\*dashboards, drilldowns, and exports\*\* (Excel only; PDF optional) (§8).



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

\- \*\*Exports\*\*: Excel (.xlsx) for all grids; optional PDF summary packs.



\## 9. Acceptance Criteria

\- 100% rows have ProjectKey, CustomerKey, PeriodKey (else appear in Quarantine with reasons).

\- All reconciliation rules run with zero critical blockers.

\- Export totals exactly match on-screen totals.



\## 10. Non-Goals

No time entry, billing execution, or GL posting; no external workflow engine.





