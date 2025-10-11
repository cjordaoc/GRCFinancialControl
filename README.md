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