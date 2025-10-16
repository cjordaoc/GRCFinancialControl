# Dataverse Alignment Report

Generated: 2025-10-16 04:23:25 UTC
Source schema: `16` tables parsed from artifacts/mysql/rebuild_schema.sql
Dataverse org URL: (not configured)
Dataverse connection: not available

## Connection Warnings
- Dataverse connection is not configured. Set DV_ORG_URL, DV_CLIENT_ID, DV_CLIENT_SECRET, and DV_TENANT_ID.

## Native Field Replacement Map
- `CreatedAt` → `createdon`
- `UpdatedAt` → `modifiedon`
- `CreatedBy` → `createdby`
- `UpdatedBy` → `modifiedby`
- `OwnerId` → `ownerid`
- `Owner` → `ownerid`
- `Status` → `statuscode`
- `StatusText` → `statecode`
- `IsActive` → `statecode`
- `IsDeleted` → `statecode`
- `DeletedAt` → `overriddencreatedon`
- `AssignedTo` → `ownerid`

## Table `ActualsEntries`

- MySQL table: `ActualsEntries`
- Dataverse entity: `ActualsEntries`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `EngagementId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `PapdId` | `INT` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `ClosingPeriodId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `Date` | `DATETIME(6)` | No | Missing | `` | `` | Dataverse entity not available. |
| `Hours` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `ImportBatchId` | `VARCHAR(100)` | No | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_ActualsEntries` | `Id` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
| `FK_ActualsEntries_Engagements` | `EngagementId` | `Engagements(Id)` | Missing | `` |
| `FK_ActualsEntries_Papds` | `PapdId` | `Papds(Id)` | Missing | `` |
| `FK_ActualsEntries_ClosingPeriods` | `ClosingPeriodId` | `ClosingPeriods(Id)` | Missing | `` |

## Table `ClosingPeriods`

- MySQL table: `ClosingPeriods`
- Dataverse entity: `ClosingPeriods`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `Name` | `VARCHAR(100)` | No | Missing | `` | `` | Dataverse entity not available. |
| `FiscalYearId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `PeriodStart` | `DATETIME(6)` | No | Missing | `` | `` | Dataverse entity not available. |
| `PeriodEnd` | `DATETIME(6)` | No | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_ClosingPeriods` | `Id` | `` | Missing |
| `UX_ClosingPeriods_Name` | `Name` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
| `FK_ClosingPeriods_FiscalYears` | `FiscalYearId` | `FiscalYears(Id)` | Missing | `` |

## Table `Customers`

- MySQL table: `Customers`
- Dataverse entity: `Customers`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `Name` | `VARCHAR(200)` | No | Missing | `` | `` | Dataverse entity not available. |
| `CustomerCode` | `VARCHAR(20)` | No | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_Customers` | `Id` | `` | Missing |
| `UX_Customers_CustomerCode` | `CustomerCode` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |

## Table `EngagementFiscalYearAllocations`

- MySQL table: `EngagementFiscalYearAllocations`
- Dataverse entity: `EngagementFiscalYearAllocations`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `EngagementId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `FiscalYearId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `PlannedHours` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_EngagementFiscalYearAllocations` | `Id` | `` | Missing |
| `UX_EngagementFiscalYearAllocations_Allocation` | `EngagementId, FiscalYearId` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
| `FK_EngagementFiscalYearAllocations_Engagements` | `EngagementId` | `Engagements(Id)` | Missing | `` |
| `FK_EngagementFiscalYearAllocations_FiscalYears` | `FiscalYearId` | `FiscalYears(Id)` | Missing | `` |

## Table `EngagementFiscalYearRevenueAllocations`

- MySQL table: `EngagementFiscalYearRevenueAllocations`
- Dataverse entity: `EngagementFiscalYearRevenueAllocations`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `EngagementId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `FiscalYearId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `ToGoValue` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `ToDateValue` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_EngagementFiscalYearRevenueAllocations` | `Id` | `` | Missing |
| `UQ_EFYRA_Allocation` | `EngagementId, FiscalYearId` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
| `FK_EFYRA_Engagements` | `EngagementId` | `Engagements(Id)` | Missing | `` |
| `FK_EFYRA_FiscalYears` | `FiscalYearId` | `FiscalYears(Id)` | Missing | `` |

## Table `EngagementManagerAssignments`

- MySQL table: `EngagementManagerAssignments`
- Dataverse entity: `EngagementManagerAssignments`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `EngagementId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `ManagerId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `BeginDate` | `DATETIME(6)` | No | Missing | `` | `` | Dataverse entity not available. |
| `EndDate` | `DATETIME(6)` | Yes | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_EngagementManagerAssignments` | `Id` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
| `FK_EngagementManagerAssignments_Engagements` | `EngagementId` | `Engagements(Id)` | Missing | `` |
| `FK_EngagementManagerAssignments_Managers` | `ManagerId` | `Managers(Id)` | Missing | `` |

## Table `EngagementPapds`

- MySQL table: `EngagementPapds`
- Dataverse entity: `EngagementPapds`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `EngagementId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `PapdId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `BeginDate` | `DATETIME(6)` | No | Missing | `` | `` | Dataverse entity not available. |
| `EndDate` | `DATETIME(6)` | Yes | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_EngagementPapds` | `Id` | `` | Missing |
| `UX_EngagementPapds_Assignment` | `EngagementId, PapdId, BeginDate` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
| `FK_EngagementPapds_Engagements` | `EngagementId` | `Engagements(Id)` | Missing | `` |
| `FK_EngagementPapds_Papds` | `PapdId` | `Papds(Id)` | Missing | `` |

## Table `EngagementRankBudgets`

- MySQL table: `EngagementRankBudgets`
- Dataverse entity: `EngagementRankBudgets`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `BIGINT` | No | Missing | `` | `` | Dataverse entity not available. |
| `EngagementId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `RankName` | `VARCHAR(100)` | No | Missing | `` | `` | Dataverse entity not available. |
| `Hours` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `CreatedAtUtc` | `DATETIME(6)` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `UpdatedAtUtc` | `DATETIME(6)` | Yes | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_EngagementRankBudgets` | `Id` | `` | Missing |
| `UX_EngagementRankBudgets_EngagementRank` | `EngagementId, RankName` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
| `FK_EngagementRankBudgets_Engagements` | `EngagementId` | `Engagements(Id)` | Missing | `` |

## Table `Engagements`

- MySQL table: `Engagements`
- Dataverse entity: `Engagements`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `EngagementId` | `VARCHAR(64)` | No | Missing | `` | `` | Dataverse entity not available. |
| `Description` | `VARCHAR(255)` | No | Missing | `` | `` | Dataverse entity not available. |
| `Currency` | `VARCHAR(16)` | No | Missing | `` | `` | Dataverse entity not available. |
| `MarginPctBudget` | `DECIMAL(9, 4)` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `MarginPctEtcp` | `DECIMAL(9, 4)` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `LastEtcDate` | `DATETIME(6)` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `ProposedNextEtcDate` | `DATETIME(6)` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `StatusText` | `VARCHAR(100)` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `CustomerId` | `INT` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `OpeningValue` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `OpeningExpenses` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `Status` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `Source` | `VARCHAR(20)` | No | Missing | `` | `` | Dataverse entity not available. |
| `InitialHoursBudget` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `EtcpHours` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `ValueEtcp` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `ExpensesEtcp` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `LastClosingPeriodId` | `INT` | Yes | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_Engagements` | `Id` | `` | Missing |
| `UX_Engagements_EngagementId` | `EngagementId` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
| `FK_Engagements_Customers` | `CustomerId` | `Customers(Id)` | Missing | `` |
| `FK_Engagements_LastCP` | `LastClosingPeriodId` | `ClosingPeriods(Id)` | Missing | `` |

## Table `Exceptions`

- MySQL table: `Exceptions`
- Dataverse entity: `Exceptions`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `Timestamp` | `DATETIME(6)` | No | Missing | `` | `` | Dataverse entity not available. |
| `SourceFile` | `VARCHAR(260)` | No | Missing | `` | `` | Dataverse entity not available. |
| `RowData` | `TEXT` | No | Missing | `` | `` | Dataverse entity not available. |
| `Reason` | `VARCHAR(500)` | No | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_Exceptions` | `Id` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |

## Table `FinancialEvolution`

- MySQL table: `FinancialEvolution`
- Dataverse entity: `FinancialEvolution`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `ClosingPeriodId` | `VARCHAR(100)` | No | Missing | `` | `` | Dataverse entity not available. |
| `EngagementId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `HoursData` | `DECIMAL(18, 2)` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `ValueData` | `DECIMAL(18, 2)` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `MarginData` | `DECIMAL(9, 4)` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `ExpenseData` | `DECIMAL(18, 2)` | Yes | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_FinancialEvolution` | `Id` | `` | Missing |
| `UX_FinancialEvolution_Key` | `EngagementId, ClosingPeriodId` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
| `FK_FinancialEvolution_Engagements` | `EngagementId` | `Engagements(Id)` | Missing | `` |

## Table `FiscalYears`

- MySQL table: `FiscalYears`
- Dataverse entity: `FiscalYears`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `Name` | `VARCHAR(100)` | No | Missing | `` | `` | Dataverse entity not available. |
| `StartDate` | `DATETIME(6)` | No | Missing | `` | `` | Dataverse entity not available. |
| `EndDate` | `DATETIME(6)` | No | Missing | `` | `` | Dataverse entity not available. |
| `AreaSalesTarget` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `AreaRevenueTarget` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |
| `IsLocked` | `TINYINT(1)` | No | Missing | `` | `` | Dataverse entity not available. |
| `LockedAt` | `DATETIME(6)` | Yes | Missing | `` | `` | Dataverse entity not available. |
| `LockedBy` | `VARCHAR(100)` | Yes | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_FiscalYears` | `Id` | `` | Missing |
| `UX_FiscalYears_Name` | `Name` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |

## Table `Managers`

- MySQL table: `Managers`
- Dataverse entity: `Managers`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `Name` | `VARCHAR(200)` | No | Missing | `` | `` | Dataverse entity not available. |
| `Email` | `VARCHAR(254)` | No | Missing | `` | `` | Dataverse entity not available. |
| `Position` | `VARCHAR(50)` | No | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_Managers` | `Id` | `` | Missing |
| `UQ_Managers_Email` | `Email` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |

## Table `Papds`

- MySQL table: `Papds`
- Dataverse entity: `Papds`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `Name` | `VARCHAR(200)` | No | Missing | `` | `` | Dataverse entity not available. |
| `Email` | `VARCHAR(254)` | No | Missing | `` | `` | Dataverse entity not available. |
| `Level` | `VARCHAR(100)` | No | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_Papds` | `Id` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |

## Table `PlannedAllocations`

- MySQL table: `PlannedAllocations`
- Dataverse entity: `PlannedAllocations`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `EngagementId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `ClosingPeriodId` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `AllocatedHours` | `DECIMAL(18, 2)` | No | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_PlannedAllocations` | `Id` | `` | Missing |
| `UX_PlannedAllocations_EngagementPeriod` | `EngagementId, ClosingPeriodId` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
| `FK_PlannedAllocations_Engagements` | `EngagementId` | `Engagements(Id)` | Missing | `` |
| `FK_PlannedAllocations_ClosingPeriods` | `ClosingPeriodId` | `ClosingPeriods(Id)` | Missing | `` |

## Table `Settings`

- MySQL table: `Settings`
- Dataverse entity: `Settings`
- Dataverse entity status: missing

### Column Alignment

| SQL Column | SQL Type | Nullable | Alignment | Dataverse Attribute | Dataverse Type | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `Id` | `INT` | No | Missing | `` | `` | Dataverse entity not available. |
| `Key` | `VARCHAR(128)` | No | Missing | `` | `` | Dataverse entity not available. |
| `Value` | `TEXT` | No | Missing | `` | `` | Dataverse entity not available. |

### Key Alignment

| Key | SQL Columns | Dataverse Columns | Status |
| --- | --- | --- | --- |
| `PK_Settings` | `Id` | `` | Missing |
| `UX_Settings_Key` | `Key` | `` | Missing |

### Foreign Key Alignment

| Foreign Key | Columns | References | Status | Matched Relationship |
| --- | --- | --- | --- | --- |
