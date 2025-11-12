/* ================================================================
   GRC FINANCIAL CONTROL DASHBOARD - COMPLETE OPTIMIZED SCHEMA
   
   This script contains everything needed to recreate the database
   with all performance optimizations included.
   
   Includes:
   - Original schema with all tables
   - All performance indexes
   - Optimized views for Power BI
   - Materialized summary tables
   - Refresh procedures
   
   NO AUDIT TRAIL (Simplified version)
   
   Run this script to recreate the entire database from scratch.
   Estimated execution time: 2-5 minutes
   ================================================================ */

-- ================================================================
-- SECTION 1: DROP AND RECREATE DATABASE (OPTIONAL)
-- ================================================================

-- DROP DATABASE IF EXISTS blac3289_GRCFinancialControl;
-- CREATE DATABASE blac3289_GRCFinancialControl CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE blac3289_GRCFinancialControl;

-- If not recreating, just use the database
-- USE blac3289_GRCFinancialControl;

-- ================================================================
-- SECTION 2: DROP ALL EXISTING TABLES
-- ================================================================

SET FOREIGN_KEY_CHECKS = 0;

-- Drop objects recreated by this script
DROP VIEW IF EXISTS vw_PapdRevenueSummary;

DROP PROCEDURE IF EXISTS sp_RefreshEngagementLatestFinancials;
DROP PROCEDURE IF EXISTS sp_RefreshPapdRevenueSummary;
DROP PROCEDURE IF EXISTS sp_RefreshManagerRevenueSummary;
DROP PROCEDURE IF EXISTS sp_RefreshCustomerSummaryCache;
DROP PROCEDURE IF EXISTS sp_RefreshAllMaterializedTables;

-- 1) Allow a big enough GROUP_CONCAT so the list of tables isn't cut off
SET SESSION group_concat_max_len = 1024 * 1024;  -- 1 MB

-- 2) Build the DROP statement safely (schema-qualified + backticks)
SELECT COALESCE(
         CONCAT(
           'DROP TABLE IF EXISTS ',
           GROUP_CONCAT(CONCAT('`', TABLE_SCHEMA, '`.`', TABLE_NAME, '`')
                        ORDER BY TABLE_NAME SEPARATOR ', ')
         ),
         'SELECT 1'
       )
INTO @drop_sql
FROM information_schema.tables
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_TYPE = 'BASE TABLE';

-- 4) Run it
PREPARE stmt FROM @drop_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;



/* ============================
   Core reference tables
   ============================ */

-- Table: Customers - master records for client organizations served by the firm.
CREATE TABLE `Customers`
(
    `Id`           INT           NOT NULL AUTO_INCREMENT,
    `Name`         VARCHAR(200)  NOT NULL,
    `CustomerCode` VARCHAR(20)   NOT NULL,
    CONSTRAINT `PK_Customers` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Customers_CustomerCode` UNIQUE (`CustomerCode`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: FiscalYears - defines yearly planning windows with lock state and targets.
CREATE TABLE `FiscalYears`
(
    `Id`                INT             NOT NULL AUTO_INCREMENT,
    `Name`              VARCHAR(100)    NOT NULL,
    `StartDate`         DATETIME(6)     NOT NULL,
    `EndDate`           DATETIME(6)     NOT NULL,
    `AreaSalesTarget`   DECIMAL(18, 2)  NOT NULL DEFAULT 0,
    `AreaRevenueTarget` DECIMAL(18, 2)  NOT NULL DEFAULT 0,
    `IsLocked`          TINYINT(1)      NOT NULL DEFAULT 0,
    `LockedAt`          DATETIME(6)     NULL,
    `LockedBy`          VARCHAR(100)    NULL,
    CONSTRAINT `PK_FiscalYears` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_FiscalYears_Name` UNIQUE (`Name`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: ClosingPeriods - monthly/periodic snapshots mapped to a fiscal year.
CREATE TABLE `ClosingPeriods`
(
    `Id`           INT           NOT NULL AUTO_INCREMENT,
    `Name`         VARCHAR(100)  NOT NULL,
    `FiscalYearId` INT           NOT NULL,
    `PeriodStart`  DATETIME(6)   NOT NULL,
    `PeriodEnd`    DATETIME(6)   NOT NULL,
    `IsLocked`     TINYINT(1)    NOT NULL DEFAULT 0,
    CONSTRAINT `PK_ClosingPeriods` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_ClosingPeriods_Name` UNIQUE (`Name`),
    CONSTRAINT `FK_ClosingPeriods_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE RESTRICT
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: Papds - partner/associate partner directory used for portfolio slicing.
CREATE TABLE `Papds`
(
    `Id`    INT          NOT NULL AUTO_INCREMENT,
    `Name`  VARCHAR(200) NOT NULL,
    `Level` VARCHAR(100) NOT NULL,
    `EngagementPapdGui` VARCHAR(10) NOT NULL,
    `WindowsLogin` VARCHAR(200) NULL,
    CONSTRAINT `PK_Papds` PRIMARY KEY (`Id`),
    CONSTRAINT `UQ_Papds_WindowsLogin` UNIQUE (`WindowsLogin`),
    CONSTRAINT `UQ_Papds_EngagementPapdGUI` UNIQUE (`EngagementPapdGUI`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: Managers - engagement managers with contact metadata for allocations.
CREATE TABLE `Managers`
(
    `Id`       INT           NOT NULL AUTO_INCREMENT,
    `Name`     VARCHAR(200)  NOT NULL,
    `Email`    VARCHAR(254)  NOT NULL,
    `Position` VARCHAR(50)   NOT NULL,
    `EngagementManagerGui` VARCHAR(10) NOT NULL, 
    `WindowsLogin` VARCHAR(200) NULL,
    CONSTRAINT `PK_Managers` PRIMARY KEY (`Id`),
    CONSTRAINT `UQ_Managers_Email` UNIQUE (`Email`),
    CONSTRAINT `UQ_Managers_WindowsLogin` UNIQUE (`WindowsLogin`),
    CONSTRAINT `UQ_Managers_EngagementManagerGUI` UNIQUE (`EngagementManagerGUI`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: RankMappings - normalizes spreadsheet rank labels to internal catalog.
CREATE TABLE IF NOT EXISTS `RankMappings`
(
    `Id`              INT             NOT NULL AUTO_INCREMENT,
    `RankCode`        VARCHAR(50)     NOT NULL,
    `RankName`        VARCHAR(100)    NULL,
    `SpreadsheetRank` VARCHAR(100)    NULL,
    `CreatedAt`       TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `IsActive`        TINYINT(1)      NOT NULL DEFAULT 1,
    `LastSeenAt`      DATETIME(6)     NULL,
    CONSTRAINT `PK_RankMappings` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_RankMappings_RankCode` UNIQUE (`RankCode`),
    INDEX `IX_RankMappings_RankName` (`RankName`),
    INDEX `IX_RankMappings_SpreadsheetRank` (`SpreadsheetRank`),
    INDEX `IX_RankMappings_IsActive` (`IsActive`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;


-- Table: Employees - resource roster keyed by GPN for hours attribution.
CREATE TABLE `Employees`
(
    `Gpn`           VARCHAR(20)    NOT NULL,
    `EmployeeName`  VARCHAR(200)   NOT NULL,
    `IsEyEmployee`  TINYINT(1)     NOT NULL DEFAULT 1,
    `IsContractor`  TINYINT(1)     NOT NULL DEFAULT 0,
    `Office`        VARCHAR(100)   NULL,
    `CostCenter`    VARCHAR(50)    NULL,
    `StartDate`     DATE           NULL,
    `EndDate`       DATE           NULL,
    CONSTRAINT `PK_Employees` PRIMARY KEY (`Gpn`),
    INDEX `IX_Employees_Office` (`Office`),
    INDEX `IX_Employees_CostCenter` (`CostCenter`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;


/* ============================
   Engagements & assignments
   ============================ */

-- Table: Engagements - core project records including financial metadata.
CREATE TABLE `Engagements`
(
    `Id`                   INT            NOT NULL AUTO_INCREMENT,
    `EngagementId`         VARCHAR(64)    NOT NULL,
    `Description`          VARCHAR(255)   NOT NULL,
    `Currency`             VARCHAR(16)    NOT NULL DEFAULT '',
    `MarginPctBudget`      DECIMAL(9, 4)  NULL,
    `MarginPctEtcp`        DECIMAL(9, 4)  NULL,
    `LastEtcDate`          DATETIME(6)    NULL,
    `ProposedNextEtcDate`  DATETIME(6)    NULL,
    `StatusText`           VARCHAR(100)   NULL,
    `CustomerId`           INT            NULL,
    `OpeningValue`         DECIMAL(18, 2) NOT NULL,
    `OpeningExpenses`      DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `Status`               INT            NOT NULL,
    `Source`               VARCHAR(20)    NOT NULL DEFAULT 'GrcProject',
    `InitialHoursBudget`   DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `EtcpHours`            DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `ValueEtcp`            DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `ExpensesEtcp`         DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `UnbilledRevenueDays`  INT            NULL,
    `LastClosingPeriodId`  INT            NULL,
    CONSTRAINT `PK_Engagements` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Engagements_EngagementId` UNIQUE (`EngagementId`),
    CONSTRAINT `FK_Engagements_Customers` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_Engagements_LastCP` FOREIGN KEY (`LastClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: EngagementPapds - relationship bridge between engagements and PAPDs.
CREATE TABLE `EngagementPapds`
(
    `Id`           INT         NOT NULL AUTO_INCREMENT,
    `EngagementId` INT         NOT NULL,
    `PapdId`       INT         NOT NULL,
    CONSTRAINT `PK_EngagementPapds` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementPapds_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementPapds_Papds` FOREIGN KEY (`PapdId`) REFERENCES `Papds` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementPapds_Assignment` UNIQUE (`EngagementId`, `PapdId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: EngagementManagerAssignments - links engagements to responsible managers.
CREATE TABLE `EngagementManagerAssignments`
(
    `Id`           INT         NOT NULL AUTO_INCREMENT,
    `EngagementId` INT         NOT NULL,
    `ManagerId`    INT         NOT NULL,
    CONSTRAINT `PK_EngagementManagerAssignments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementManagerAssignments_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementManagerAssignments_Managers` FOREIGN KEY (`ManagerId`) REFERENCES `Managers` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementManagerAssignments_Assignment` UNIQUE (`EngagementId`, `ManagerId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: EngagementAdditionalSales - captures upsell opportunities tied to an engagement.
CREATE TABLE `EngagementAdditionalSales`
(
    `Id`            INT             NOT NULL AUTO_INCREMENT,
    `EngagementId`  INT             NOT NULL,
    `Description`   VARCHAR(500)    NOT NULL,
    `OpportunityId` VARCHAR(100)    NULL,
    `Value`         DECIMAL(18, 2)  NOT NULL,
    CONSTRAINT `PK_EngagementAdditionalSales` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementAdditionalSales_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    INDEX `IX_EngagementAdditionalSales_EngagementId` (`EngagementId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;


/* ============================
   Budgets & financial evolution
   ============================ */

-- Table: EngagementRankBudgets - latest hours budget snapshot per engagement/rank/period.
CREATE TABLE `EngagementRankBudgets`
(
    `Id`             BIGINT         NOT NULL AUTO_INCREMENT,
    `EngagementId`   INT            NOT NULL,
    `FiscalYearId`   INT            NOT NULL,
    `ClosingPeriodId` INT           NOT NULL,
    `RankName`       VARCHAR(100)   NOT NULL,
    `BudgetHours`    DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `ConsumedHours`  DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `AdditionalHours` DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `RemainingHours` DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `Status`         VARCHAR(20)    NOT NULL DEFAULT 'Green',
    `CreatedAtUtc`   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAtUtc`   DATETIME(6)    NULL,
    CONSTRAINT `PK_EngagementRankBudgets` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementRankBudgets_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementRankBudgets_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementRankBudgets_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementRankBudgets_Snapshot` UNIQUE (`EngagementId`, `FiscalYearId`, `RankName`, `ClosingPeriodId`),
    INDEX `IX_EngagementRankBudgets_EngagementId` (`EngagementId`),
    INDEX `IX_EngagementRankBudgets_ClosingPeriodId` (`ClosingPeriodId`),
    INDEX `IX_EngagementRankBudgets_Latest` (`EngagementId`, `FiscalYearId`, `RankName`, `ClosingPeriodId` DESC)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: EngagementRankBudgetHistory - raw history imports for audit and replay.
CREATE TABLE `EngagementRankBudgetHistory`
(
    `Id`               INT             NOT NULL AUTO_INCREMENT,
    `EngagementCode`   VARCHAR(50)     NOT NULL,
    `RankCode`         VARCHAR(50)     NOT NULL,
    `FiscalYearId`     INT             NOT NULL,
    `ClosingPeriodId`  INT             NOT NULL,
    `Hours`            DECIMAL(12, 2)  NOT NULL,
    `UploadedAt`       TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_EngagementRankBudgetHistory` PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE UNIQUE INDEX `IX_History_Key`
    ON `EngagementRankBudgetHistory` (`EngagementCode`, `RankCode`, `FiscalYearId`, `ClosingPeriodId`);

-- Financial Evolution: Granular metrics per closing period (Budget/ETD/FYTD breakdowns)
-- Table: FinancialEvolution - monthly financial rollups feeding dashboards.
CREATE TABLE `FinancialEvolution`
(
    `Id`                   INT            NOT NULL AUTO_INCREMENT,
    -- Stored as text because the importer persists the closing period identifier exactly as supplied by EF (numeric id string).
    `ClosingPeriodId`      VARCHAR(100)   NOT NULL,
    `EngagementId`         INT            NOT NULL,
    -- Hours Metrics
    `BudgetHours`          DECIMAL(18, 2) NULL,
    `ChargedHours`         DECIMAL(18, 2) NULL,
    `FYTDHours`            DECIMAL(18, 2) NULL,
    `AdditionalHours`      DECIMAL(18, 2) NULL,
    -- Revenue Metrics
    `ValueData`            DECIMAL(18, 2) NULL,
    `FiscalYearId`         INT            NULL,
    `RevenueToGoValue`     DECIMAL(18, 2) NULL,
    `RevenueToDateValue`   DECIMAL(18, 2) NULL,
    -- Margin Metrics
    `BudgetMargin`         DECIMAL(18, 2) NULL,
    `ToDateMargin`         DECIMAL(18, 2) NULL,
    `FYTDMargin`           DECIMAL(18, 2) NULL,
    -- Expense Metrics
    `ExpenseBudget`        DECIMAL(18, 2) NULL,
    `ExpensesToDate`       DECIMAL(18, 2) NULL,
    `FYTDExpenses`         DECIMAL(18, 2) NULL,
    CONSTRAINT `PK_FinancialEvolution` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_FinancialEvolution_Key` UNIQUE (`EngagementId`, `ClosingPeriodId`),
    CONSTRAINT `FK_FinancialEvolution_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_FinancialEvolution_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE SET NULL,
    INDEX `IX_FinancialEvolution_FiscalYear` (`FiscalYearId`),
    INDEX `IX_FinancialEvolution_ClosingPeriod` (`ClosingPeriodId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;


/* ============================
   Allocations (hours & revenue)
   ============================ */

-- Table: PlannedAllocations - planned hours per engagement and closing period.
CREATE TABLE `PlannedAllocations`
(
    `Id`              INT            NOT NULL AUTO_INCREMENT,
    `EngagementId`    INT            NOT NULL,
    `ClosingPeriodId` INT            NOT NULL,
    `AllocatedHours`  DECIMAL(18, 2) NOT NULL DEFAULT 0,
    CONSTRAINT `PK_PlannedAllocations` PRIMARY KEY (`Id`),
    CONSTRAINT `UQ_PlannedAllocations_EngagementPeriod` UNIQUE (`EngagementId`, `ClosingPeriodId`),
    CONSTRAINT `FK_PlannedAllocations_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_PlannedAllocations_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE CASCADE,
    INDEX `IX_PlannedAllocations_ClosingPeriodId` (`ClosingPeriodId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: ActualsEntries - time tracking entries imported from source systems.
CREATE TABLE `ActualsEntries`
(
    `Id`              INT            NOT NULL AUTO_INCREMENT,
    `EngagementId`    INT            NOT NULL,
    `PapdId`          INT            NULL,
    `ClosingPeriodId` INT            NOT NULL,
    `Date`            DATETIME(6)    NOT NULL,
    `Hours`           DECIMAL(18, 2) NOT NULL,
    `ImportBatchId`   VARCHAR(100)   NOT NULL,
    CONSTRAINT `PK_ActualsEntries` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ActualsEntries_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ActualsEntries_Papds` FOREIGN KEY (`PapdId`) REFERENCES `Papds` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_ActualsEntries_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE RESTRICT,
    INDEX `IX_ActualsEntries_Engagement_Date` (`EngagementId`, `Date`),
    INDEX `IX_ActualsEntries_ClosingPeriodId` (`ClosingPeriodId`),
    INDEX `IX_ActualsEntries_PapdId` (`PapdId`),
    INDEX `IX_ActualsEntries_ImportBatch` (`ImportBatchId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Table: Exceptions - captures rejected import rows with diagnostic info.
CREATE TABLE `Exceptions`
(
    `Id`         INT           NOT NULL AUTO_INCREMENT,
    `Timestamp`  DATETIME(6)   NOT NULL, -- populated via trigger
    `SourceFile` VARCHAR(260)  NOT NULL,
    `RowData`    TEXT          NOT NULL,
    `Reason`     VARCHAR(500)  NOT NULL,
    CONSTRAINT `PK_Exceptions` PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Snapshot-based Revenue Allocations (aligned with Financial Evolution closing periods)
-- Table: EngagementFiscalYearRevenueAllocations - FY revenue snapshots aligned to closing periods.
CREATE TABLE `EngagementFiscalYearRevenueAllocations`
(
    `Id`             INT            NOT NULL AUTO_INCREMENT,
    `EngagementId`   INT            NOT NULL,
    `FiscalYearId`   INT            NOT NULL,
    `ClosingPeriodId` INT           NOT NULL,
    `ToGoValue`      DECIMAL(18, 2) NOT NULL,
    `ToDateValue`    DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `LastUpdateDate` DATE           NULL,
    `CreatedAt`      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT `PK_EngagementFiscalYearRevenueAllocations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EFYRA_Engagements` FOREIGN KEY (`EngagementId`)
        REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EFYRA_FiscalYears` FOREIGN KEY (`FiscalYearId`)
        REFERENCES `FiscalYears` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EFYRA_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`)
        REFERENCES `ClosingPeriods` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementFiscalYearRevenueAllocations_Snapshot` UNIQUE (`EngagementId`, `FiscalYearId`, `ClosingPeriodId`),
    INDEX `IX_EFYRA_FiscalYearId` (`FiscalYearId`),
    INDEX `IX_EFYRA_ClosingPeriodId` (`ClosingPeriodId`),
    INDEX `IX_EngagementFiscalYearRevenueAllocations_Latest` (`EngagementId`, `FiscalYearId`, `ClosingPeriodId` DESC)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_unicode_ci;



/* ===========================================================
   INVOICE PLANNER + NOTIFICATION INFRA
   (unchanged behavior; TIMESTAMP where auto-defaults are desired)
   =========================================================== */

-- Table: InvoicePlan - header for invoice scheduling scenarios imported from Excel.
CREATE TABLE `InvoicePlan` (
  `Id`                      INT NOT NULL AUTO_INCREMENT,
  `EngagementId`            VARCHAR(64) NOT NULL,
  `Type`                    VARCHAR(16) NOT NULL,
  `NumInvoices`             INT NOT NULL,
  `PaymentTermDays`         INT NOT NULL,
  `CustomerFocalPointName`  VARCHAR(120) NOT NULL,
  `CustomerFocalPointEmail` VARCHAR(200) NOT NULL,
  `CustomInstructions`      TEXT NULL,
  `AdditionalDetails`      TEXT NULL,
  `FirstEmissionDate`       DATE NULL,
  `CreatedAt`               TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt`               TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  KEY `IX_InvoicePlan_Engagement` (`EngagementId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Table: InvoicePlanEmail - distribution list for invoice plan notifications.
CREATE TABLE `InvoicePlanEmail` (
  `Id`        INT NOT NULL AUTO_INCREMENT,
  `PlanId`    INT NOT NULL,
  `Email`     VARCHAR(200) NOT NULL,
  `CreatedAt` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  KEY `IX_InvoicePlanEmail_Plan` (`PlanId`),
  CONSTRAINT `FK_InvoicePlanEmail_Plan` FOREIGN KEY (`PlanId`) REFERENCES `InvoicePlan`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Table: InvoiceItem - planned invoice installments with financial metadata.
CREATE TABLE `InvoiceItem` (
  `Id`                  INT NOT NULL AUTO_INCREMENT,
  `PlanId`              INT NOT NULL,
  `SeqNo`               INT NOT NULL,
  `Percentage`          DECIMAL(9,4) NOT NULL DEFAULT 0,
  `Amount`              DECIMAL(18,2) NOT NULL DEFAULT 0,
  `EmissionDate`        DATE NULL,
  `DueDate`             DATE NULL,
  `PayerCnpj`           VARCHAR(20) NOT NULL,
  `PoNumber`            VARCHAR(64) NULL,
  `FrsNumber`           VARCHAR(64) NULL,
  `CustomerTicket`      VARCHAR(64) NULL,
  `PaymentTypeCode`     VARCHAR(64) NOT NULL DEFAULT 'TRANSFERENCIA_BANCARIA',
  `AdditionalInfo`      TEXT NULL,
  `DeliveryDescription` VARCHAR(255) NULL,
  `Status`              VARCHAR(16) NOT NULL DEFAULT 'Planned',
  `RitmNumber`          VARCHAR(64) NULL,
  `CoeResponsible`      VARCHAR(120) NULL,
  `RequestDate`         DATE NULL,
  `CreatedAt`           TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt`           TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  CONSTRAINT `FK_InvoiceItem_Plan` FOREIGN KEY (`PlanId`) REFERENCES `InvoicePlan`(`Id`) ON DELETE CASCADE,
  UNIQUE KEY `UQ_InvoiceItem_PlanSeq` (`PlanId`,`SeqNo`),
  KEY `IX_InvoiceItem_EmissionDate` (`EmissionDate`),
  KEY `IX_InvoiceItem_Status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Table: InvoiceEmission - tracks actual invoice emissions synced back from S4.
CREATE TABLE `InvoiceEmission` (
  `Id`            INT NOT NULL AUTO_INCREMENT,
  `InvoiceItemId` INT NOT NULL,
  `BzCode`        VARCHAR(64) NOT NULL,
  `EmittedAt`     DATE NOT NULL,
  `CreatedAt`     TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt`     TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `CanceledAt`    DATE NULL,
  `CancelReason`  VARCHAR(255) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_InvoiceEmission_Item` (`InvoiceItemId`),
  CONSTRAINT `FK_InvoiceEmission_Item` FOREIGN KEY (`InvoiceItemId`) REFERENCES `InvoiceItem`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- Table: ManagerRevenueSummaryMaterialized - cached KPI rollups per manager/year for dashboards.
CREATE TABLE `ManagerRevenueSummaryMaterialized` (
    `FiscalYearId` INT NOT NULL,
    `FiscalYearName` VARCHAR(100) NOT NULL,
    `ManagerId` INT NOT NULL,
    `ManagerName` VARCHAR(200) NOT NULL,
    `ManagerPosition` VARCHAR(50) NOT NULL,
    `TotalToGoValue` DECIMAL(18, 2) DEFAULT 0,
    `TotalToDateValue` DECIMAL(18, 2) DEFAULT 0,
    `TotalValue` DECIMAL(18, 2) DEFAULT 0,
    `ChargedHours` DECIMAL(18, 2) DEFAULT 0,
    `FYTDHours` DECIMAL(18, 2) DEFAULT 0,
    `FYTDExpenses` DECIMAL(18, 2) DEFAULT 0,
    `FYTDMargin` DECIMAL(18, 2) DEFAULT 0,
    `LatestClosingPeriodEnd` DATETIME(6) NULL,
    `EngagementCount` INT DEFAULT 0,
    `ActiveEngagementCount` INT DEFAULT 0,
    CONSTRAINT `PK_ManagerRevenueSummaryMaterialized` PRIMARY KEY (`FiscalYearId`, `ManagerId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

DROP PROCEDURE IF EXISTS sp_RefreshManagerRevenueSummary;
DELIMITER $$
CREATE PROCEDURE sp_RefreshManagerRevenueSummary()
BEGIN
    DELETE FROM ManagerRevenueSummaryMaterialized
    WHERE FiscalYearId IS NOT NULL;
    INSERT INTO ManagerRevenueSummaryMaterialized (
        FiscalYearId, FiscalYearName, ManagerId, ManagerName, ManagerPosition,
        TotalToGoValue, TotalToDateValue, TotalValue,
        ChargedHours, FYTDHours, FYTDExpenses, FYTDMargin,
        LatestClosingPeriodEnd, EngagementCount, ActiveEngagementCount
    )
    SELECT
        elf.FiscalYearId,
        elf.FiscalYearName,
        m.Id,
        m.Name,
        m.Position,
        COALESCE(SUM(elf.RevenueToGoValue), 0) AS TotalToGoValue,
        COALESCE(SUM(elf.RevenueToDateValue), 0) AS TotalToDateValue,
        COALESCE(SUM(elf.TotalRevenue), 0) AS TotalValue,
        COALESCE(SUM(elf.ChargedHours), 0) AS ChargedHours,
        COALESCE(SUM(elf.FYTDHours), 0) AS FYTDHours,
        COALESCE(SUM(elf.FYTDExpenses), 0) AS FYTDExpenses,
        COALESCE(SUM(elf.FYTDMargin), 0) AS FYTDMargin,
        MAX(elf.ClosingPeriodEnd) AS LatestClosingPeriodEnd,
        COUNT(DISTINCT elf.EngagementId) AS EngagementCount,
        COUNT(DISTINCT CASE WHEN e.Status = 1 THEN elf.EngagementId END) AS ActiveEngagementCount
    FROM EngagementLatestFinancials elf
    INNER JOIN EngagementManagerAssignments ema ON ema.EngagementId = elf.EngagementId
    INNER JOIN Managers m ON m.Id = ema.ManagerId
    INNER JOIN Engagements e ON e.Id = elf.EngagementId
    WHERE elf.FiscalYearId IS NOT NULL
    GROUP BY elf.FiscalYearId, elf.FiscalYearName, m.Id, m.Name, m.Position;
END$$
DELIMITER ;

-- ===========================================================
-- Procedure: sp_RefreshPapdRevenueSummary
-- Purpose  : Repopulate the physical summary table PapdRevenueSummary used by dashboards
-- ===========================================================

-- Table: PapdRevenueSummary - materialized PAPD-level KPI snapshot for dashboards.
CREATE TABLE `PapdRevenueSummary` (
    `FiscalYearId` INT NOT NULL,
    `FiscalYearName` VARCHAR(100) NOT NULL,
    `PapdId` INT NOT NULL,
    `PapdName` VARCHAR(200) NOT NULL,
    `TotalToGoValue` DECIMAL(18, 2) DEFAULT 0,
    `TotalToDateValue` DECIMAL(18, 2) DEFAULT 0,
    `TotalValue` DECIMAL(18, 2) DEFAULT 0,
    `ChargedHours` DECIMAL(18, 2) DEFAULT 0,
    `FYTDHours` DECIMAL(18, 2) DEFAULT 0,
    `FYTDExpenses` DECIMAL(18, 2) DEFAULT 0,
    `FYTDMargin` DECIMAL(18, 2) DEFAULT 0,
    `LatestClosingPeriodEnd` DATETIME(6) NULL,
    CONSTRAINT `PK_PapdRevenueSummary` PRIMARY KEY (`FiscalYearId`, `PapdId`),
    INDEX `IX_PapdRevenueSummary_FY` (`FiscalYearId`),
    INDEX `IX_PapdRevenueSummary_Papd` (`PapdId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

DROP PROCEDURE IF EXISTS sp_RefreshPapdRevenueSummary;
DELIMITER $$

CREATE DEFINER=`blac3289_GRCFinControl`@`%` PROCEDURE `sp_RefreshPapdRevenueSummary`()
BEGIN
    DECLARE exit HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    -- Step 1: clear existing snapshot so the refresh is idempotent
    DELETE FROM PapdRevenueSummary
    WHERE FiscalYearId IS NOT NULL;

    -- Step 2: aggregate EngagementLatestFinancials by PAPD and FiscalYear
    INSERT INTO PapdRevenueSummary (
        FiscalYearId, FiscalYearName, PapdId, PapdName,
        TotalToGoValue, TotalToDateValue, TotalValue,
        ChargedHours, FYTDHours, FYTDExpenses, FYTDMargin,
        LatestClosingPeriodEnd
    )
    WITH aggregated AS (
        SELECT
            elf.FiscalYearId,
            fy.Name AS FiscalYearName,
            ep.PapdId,
            SUM(elf.RevenueToGoValue)   AS TotalToGoValue,
            SUM(elf.RevenueToDateValue) AS TotalToDateValue,
            SUM(elf.TotalRevenue)       AS TotalValue,
            SUM(elf.ChargedHours)       AS ChargedHours,
            SUM(elf.FYTDHours)          AS FYTDHours,
            SUM(elf.FYTDExpenses)       AS FYTDExpenses,
            SUM(elf.FYTDMargin)         AS FYTDMargin,
            MAX(elf.ClosingPeriodEnd)   AS LatestClosingPeriodEnd
        FROM EngagementLatestFinancials elf
        INNER JOIN FiscalYears fy ON fy.Id = elf.FiscalYearId
        INNER JOIN EngagementPapds ep ON ep.EngagementId = elf.EngagementId
        WHERE elf.FiscalYearId IS NOT NULL
        GROUP BY elf.FiscalYearId, fy.Name, ep.PapdId
    )
    SELECT
        a.FiscalYearId,
        a.FiscalYearName,
        p.Id AS PapdId,
        p.Name AS PapdName,
        COALESCE(a.TotalToGoValue, 0),
        COALESCE(a.TotalToDateValue, 0),
        COALESCE(a.TotalValue, 0),
        COALESCE(a.ChargedHours, 0),
        COALESCE(a.FYTDHours, 0),
        COALESCE(a.FYTDExpenses, 0),
        COALESCE(a.FYTDMargin, 0),
        a.LatestClosingPeriodEnd
    FROM aggregated a
    INNER JOIN Papds p ON p.Id = a.PapdId;

    -- Step 3: ensure every FiscalYear × PAPD exists with zero placeholders
    INSERT INTO PapdRevenueSummary (
        FiscalYearId, FiscalYearName, PapdId, PapdName,
        TotalToGoValue, TotalToDateValue, TotalValue,
        ChargedHours, FYTDHours, FYTDExpenses, FYTDMargin,
        LatestClosingPeriodEnd
    )
    SELECT
        fy.Id,
        fy.Name,
        p.Id,
        p.Name,
        0, 0, 0,
        0, 0, 0, 0,
        NULL
    FROM FiscalYears fy
    CROSS JOIN Papds p
    WHERE NOT EXISTS (
        SELECT 1
        FROM PapdRevenueSummary prs
        WHERE prs.FiscalYearId = fy.Id
          AND prs.PapdId = p.Id
    );

    COMMIT;
END$$
DELIMITER ;

-- ===========================================================
-- Procedure: sp_RefreshEngagementLatestFinancials
-- ===========================================================

-- Table: EngagementLatestFinancials - materialized engagement KPI snapshot keyed by closing period.
CREATE TABLE `EngagementLatestFinancials` (
    `EngagementId` INT NOT NULL,
    `EngagementCode` VARCHAR(64) NOT NULL,
    `EngagementDescription` VARCHAR(255) NOT NULL,
    `Source` VARCHAR(20) NOT NULL,
    `CustomerId` INT NULL,
    `CustomerName` VARCHAR(200) NULL,
    `FiscalYearId` INT NOT NULL,
    `FiscalYearName` VARCHAR(100) NOT NULL,
    `ClosingPeriodId` VARCHAR(100) NOT NULL,
    `ClosingPeriodNumericId` INT NULL,
    `ClosingPeriodName` VARCHAR(100) NOT NULL,
    `ClosingPeriodEnd` DATETIME(6) NOT NULL,
    `RevenueToGoValue` DECIMAL(18, 2) DEFAULT 0,
    `RevenueToDateValue` DECIMAL(18, 2) DEFAULT 0,
    `TotalRevenue` DECIMAL(18, 2) DEFAULT 0,
    `ValueData` DECIMAL(18, 2) DEFAULT 0,
    `BudgetHours` DECIMAL(18, 2) DEFAULT 0,
    `ChargedHours` DECIMAL(18, 2) DEFAULT 0,
    `FYTDHours` DECIMAL(18, 2) DEFAULT 0,
    `AdditionalHours` DECIMAL(18, 2) DEFAULT 0,
    `ExpenseBudget` DECIMAL(18, 2) DEFAULT 0,
    `ExpensesToDate` DECIMAL(18, 2) DEFAULT 0,
    `FYTDExpenses` DECIMAL(18, 2) DEFAULT 0,
    `ToDateMargin` DECIMAL(18, 2) DEFAULT 0,
    `FYTDMargin` DECIMAL(18, 2) DEFAULT 0,
    `MarginPercentage` DECIMAL(9, 4) DEFAULT 0,
    `PrimaryManagerId` INT NULL,
    `PrimaryManagerName` VARCHAR(200) NULL,
    `PrimaryPapdId` INT NULL,
    `PrimaryPapdName` VARCHAR(200) NULL,
    `LastRefresh` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_EngagementLatestFinancials` PRIMARY KEY (`EngagementId`, `ClosingPeriodId`),
    INDEX `IX_ELF_FY` (`FiscalYearId`),
    INDEX `IX_ELF_CP` (`ClosingPeriodId`),
    INDEX `IX_ELF_CP_NUM` (`ClosingPeriodNumericId`),
    INDEX `IX_ELF_Customer` (`CustomerId`),
    INDEX `IX_ELF_PrimaryManager` (`PrimaryManagerId`),
    INDEX `IX_ELF_PrimaryPapd` (`PrimaryPapdId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

DROP PROCEDURE IF EXISTS sp_RefreshEngagementLatestFinancials;
DELIMITER $$

CREATE DEFINER=`blac3289_GRCFinControl`@`%` PROCEDURE `sp_RefreshEngagementLatestFinancials`()
BEGIN
    DECLARE exit HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    -- Step 1: clear existing snapshot rows so refresh is deterministic
    DELETE FROM EngagementLatestFinancials
    WHERE EngagementId IS NOT NULL;

    -- Step 2: load latest FinancialEvolution snapshot per engagement and fiscal year
    INSERT INTO EngagementLatestFinancials (
        EngagementId, EngagementCode, EngagementDescription, Source,
        CustomerId, CustomerName,
        FiscalYearId, FiscalYearName,
        ClosingPeriodId, ClosingPeriodNumericId, ClosingPeriodName, ClosingPeriodEnd,
        RevenueToGoValue, RevenueToDateValue, TotalRevenue, ValueData,
        BudgetHours, ChargedHours, FYTDHours, AdditionalHours,
        ExpenseBudget, ExpensesToDate, FYTDExpenses,
        ToDateMargin, FYTDMargin, MarginPercentage,
        PrimaryManagerId, PrimaryManagerName,
        PrimaryPapdId, PrimaryPapdName,
        LastRefresh
    )
    WITH ranked_financials AS (
        SELECT
            e.Id AS EngagementId,
            e.EngagementId AS EngagementCode,
            e.Description AS EngagementDescription,
            e.Source,
            e.CustomerId,
            c.Name AS CustomerName,
            fy.Id AS FiscalYearId,
            fy.Name AS FiscalYearName,
            CASE
                WHEN TRIM(fe.ClosingPeriodId) IS NULL OR TRIM(fe.ClosingPeriodId) = '' THEN CONCAT('FE-', e.Id, '-FY', LPAD(fy.Id, 4, '0'))
                ELSE TRIM(fe.ClosingPeriodId)
            END AS ClosingPeriodId,
            CASE
                WHEN TRIM(fe.ClosingPeriodId) REGEXP '^[0-9]+$' THEN CAST(TRIM(fe.ClosingPeriodId) AS UNSIGNED)
                ELSE NULL
            END AS ClosingPeriodNumericId,
            COALESCE(cp.Name, CONCAT('Closing ', fy.Name)) AS ClosingPeriodName,
            COALESCE(cp.PeriodEnd, fy.EndDate) AS ClosingPeriodEnd,
            COALESCE(fe.RevenueToGoValue, 0) AS RevenueToGoValue,
            COALESCE(fe.RevenueToDateValue, 0) AS RevenueToDateValue,
            COALESCE(fe.RevenueToGoValue, 0) + COALESCE(fe.RevenueToDateValue, 0) AS TotalRevenue,
            COALESCE(fe.ValueData, 0) AS ValueData,
            COALESCE(fe.BudgetHours, 0) AS BudgetHours,
            COALESCE(fe.ChargedHours, 0) AS ChargedHours,
            COALESCE(fe.FYTDHours, 0) AS FYTDHours,
            COALESCE(fe.AdditionalHours, 0) AS AdditionalHours,
            COALESCE(fe.ExpenseBudget, 0) AS ExpenseBudget,
            COALESCE(fe.ExpensesToDate, 0) AS ExpensesToDate,
            COALESCE(fe.FYTDExpenses, 0) AS FYTDExpenses,
            COALESCE(fe.ToDateMargin, 0) AS ToDateMargin,
            COALESCE(fe.FYTDMargin, 0) AS FYTDMargin,
            CASE
                WHEN NULLIF(COALESCE(fe.RevenueToDateValue, 0), 0) IS NULL THEN 0
                ELSE ROUND(COALESCE(fe.ToDateMargin, 0) / NULLIF(COALESCE(fe.RevenueToDateValue, 0), 0) * 100, 4)
            END AS MarginPercentage,
            ROW_NUMBER() OVER (
                PARTITION BY e.Id, fy.Id
                ORDER BY COALESCE(cp.PeriodEnd, fy.EndDate) DESC,
                         CASE
                             WHEN TRIM(fe.ClosingPeriodId) REGEXP '^[0-9]+$' THEN CAST(TRIM(fe.ClosingPeriodId) AS UNSIGNED)
                             ELSE 0
                         END DESC,
                         fe.Id DESC
            ) AS RowRank
        FROM FinancialEvolution fe
        INNER JOIN Engagements e ON e.Id = fe.EngagementId
        LEFT JOIN Customers c ON c.Id = e.CustomerId
        LEFT JOIN ClosingPeriods cp ON (
            TRIM(fe.ClosingPeriodId) REGEXP '^[0-9]+$'
            AND cp.Id = CAST(TRIM(fe.ClosingPeriodId) AS UNSIGNED)
        )
        LEFT JOIN FiscalYears fy ON fy.Id = COALESCE(fe.FiscalYearId, cp.FiscalYearId)
        WHERE fy.Id IS NOT NULL
    )
    SELECT
        rf.EngagementId,
        rf.EngagementCode,
        rf.EngagementDescription,
        rf.Source,
        rf.CustomerId,
        rf.CustomerName,
        rf.FiscalYearId,
        rf.FiscalYearName,
        rf.ClosingPeriodId,
        rf.ClosingPeriodNumericId,
        rf.ClosingPeriodName,
        rf.ClosingPeriodEnd,
        rf.RevenueToGoValue,
        rf.RevenueToDateValue,
        rf.TotalRevenue,
        rf.ValueData,
        rf.BudgetHours,
        rf.ChargedHours,
        rf.FYTDHours,
        rf.AdditionalHours,
        rf.ExpenseBudget,
        rf.ExpensesToDate,
        rf.FYTDExpenses,
        rf.ToDateMargin,
        rf.FYTDMargin,
        rf.MarginPercentage,
        mgr.PrimaryManagerId,
        mgr.PrimaryManagerName,
        papd.PrimaryPapdId,
        papd.PrimaryPapdName,
        CURRENT_TIMESTAMP AS LastRefresh
    FROM ranked_financials rf
    LEFT JOIN (
        SELECT ema.EngagementId,
               MIN(m.Id) AS PrimaryManagerId,
               GROUP_CONCAT(DISTINCT m.Name ORDER BY m.Name SEPARATOR ', ') AS PrimaryManagerName
        FROM EngagementManagerAssignments ema
        INNER JOIN Managers m ON m.Id = ema.ManagerId
        GROUP BY ema.EngagementId
    ) mgr ON mgr.EngagementId = rf.EngagementId
    LEFT JOIN (
        SELECT ep.EngagementId,
               MIN(p.Id) AS PrimaryPapdId,
               GROUP_CONCAT(DISTINCT p.Name ORDER BY p.Name SEPARATOR ', ') AS PrimaryPapdName
        FROM EngagementPapds ep
        INNER JOIN Papds p ON p.Id = ep.PapdId
        GROUP BY ep.EngagementId
    ) papd ON papd.EngagementId = rf.EngagementId
    WHERE rf.RowRank = 1;

    -- Step 3: project future fiscal-year allocations using planning/backlog snapshots
    INSERT INTO EngagementLatestFinancials (
        EngagementId, EngagementCode, EngagementDescription, Source,
        CustomerId, CustomerName,
        FiscalYearId, FiscalYearName,
        ClosingPeriodId, ClosingPeriodNumericId, ClosingPeriodName, ClosingPeriodEnd,
        RevenueToGoValue, RevenueToDateValue, TotalRevenue, ValueData,
        BudgetHours, ChargedHours, FYTDHours, AdditionalHours,
        ExpenseBudget, ExpensesToDate, FYTDExpenses,
        ToDateMargin, FYTDMargin, MarginPercentage,
        PrimaryManagerId, PrimaryManagerName,
        PrimaryPapdId, PrimaryPapdName,
        LastRefresh
    )
    WITH ranked_allocations AS (
        SELECT
            ra.EngagementId,
            ra.FiscalYearId,
            fy.Name AS FiscalYearName,
            cp.Id AS ClosingPeriodNumericId,
            cp.Name AS ClosingPeriodName,
            cp.PeriodEnd AS ClosingPeriodEnd,
            COALESCE(ra.ToGoValue, 0) AS RevenueToGoValue,
            COALESCE(ra.ToDateValue, 0) AS RevenueToDateValue,
            ROW_NUMBER() OVER (
                PARTITION BY ra.EngagementId, ra.FiscalYearId
                ORDER BY cp.PeriodEnd DESC, ra.UpdatedAt DESC, ra.Id DESC
            ) AS RowRank
        FROM EngagementFiscalYearRevenueAllocations ra
        INNER JOIN ClosingPeriods cp ON cp.Id = ra.ClosingPeriodId
        INNER JOIN FiscalYears fy ON fy.Id = ra.FiscalYearId
    )
    SELECT
        ra.EngagementId,
        e.EngagementId AS EngagementCode,
        e.Description AS EngagementDescription,
        e.Source,
        e.CustomerId,
        c.Name AS CustomerName,
        ra.FiscalYearId,
        ra.FiscalYearName,
        CONCAT('ALLOC-', LPAD(ra.ClosingPeriodNumericId, 6, '0'), '-FY', LPAD(ra.FiscalYearId, 4, '0')) AS ClosingPeriodId,
        ra.ClosingPeriodNumericId,
        ra.ClosingPeriodName,
        ra.ClosingPeriodEnd,
        ra.RevenueToGoValue,
        ra.RevenueToDateValue,
        ra.RevenueToGoValue + ra.RevenueToDateValue AS TotalRevenue,
        0 AS ValueData,
        0 AS BudgetHours,
        0 AS ChargedHours,
        0 AS FYTDHours,
        0 AS AdditionalHours,
        0 AS ExpenseBudget,
        0 AS ExpensesToDate,
        0 AS FYTDExpenses,
        0 AS ToDateMargin,
        0 AS FYTDMargin,
        0 AS MarginPercentage,
        mgr.PrimaryManagerId,
        mgr.PrimaryManagerName,
        papd.PrimaryPapdId,
        papd.PrimaryPapdName,
        CURRENT_TIMESTAMP AS LastRefresh
    FROM ranked_allocations ra
    INNER JOIN Engagements e ON e.Id = ra.EngagementId
    LEFT JOIN Customers c ON c.Id = e.CustomerId
    LEFT JOIN (
        SELECT ema.EngagementId,
               MIN(m.Id) AS PrimaryManagerId,
               GROUP_CONCAT(DISTINCT m.Name ORDER BY m.Name SEPARATOR ', ') AS PrimaryManagerName
        FROM EngagementManagerAssignments ema
        INNER JOIN Managers m ON m.Id = ema.ManagerId
        GROUP BY ema.EngagementId
    ) mgr ON mgr.EngagementId = ra.EngagementId
    LEFT JOIN (
        SELECT ep.EngagementId,
               MIN(p.Id) AS PrimaryPapdId,
               GROUP_CONCAT(DISTINCT p.Name ORDER BY p.Name SEPARATOR ', ') AS PrimaryPapdName
        FROM EngagementPapds ep
        INNER JOIN Papds p ON p.Id = ep.PapdId
        GROUP BY ep.EngagementId
    ) papd ON papd.EngagementId = ra.EngagementId
    WHERE ra.RowRank = 1
      AND NOT EXISTS (
        SELECT 1
        FROM EngagementLatestFinancials existing
        WHERE existing.EngagementId = ra.EngagementId
          AND existing.FiscalYearId = ra.FiscalYearId
    );

    -- Step 4: add zero-value placeholders for missing engagement/fiscal-year pairs
    INSERT INTO EngagementLatestFinancials (
        EngagementId, EngagementCode, EngagementDescription, Source,
        CustomerId, CustomerName,
        FiscalYearId, FiscalYearName,
        ClosingPeriodId, ClosingPeriodNumericId, ClosingPeriodName, ClosingPeriodEnd,
        RevenueToGoValue, RevenueToDateValue, TotalRevenue, ValueData,
        BudgetHours, ChargedHours, FYTDHours, AdditionalHours,
        ExpenseBudget, ExpensesToDate, FYTDExpenses,
        ToDateMargin, FYTDMargin, MarginPercentage,
        PrimaryManagerId, PrimaryManagerName,
        PrimaryPapdId, PrimaryPapdName,
        LastRefresh
    )
    SELECT
        e.Id AS EngagementId,
        e.EngagementId AS EngagementCode,
        e.Description AS EngagementDescription,
        e.Source,
        e.CustomerId,
        c.Name AS CustomerName,
        fy.Id AS FiscalYearId,
        fy.Name AS FiscalYearName,
        CONCAT('FY', LPAD(fy.Id, 4, '0'), '-PLACEHOLDER') AS ClosingPeriodId,
        NULL AS ClosingPeriodNumericId,
        CONCAT(fy.Name, ' Placeholder') AS ClosingPeriodName,
        fy.EndDate AS ClosingPeriodEnd,
        0 AS RevenueToGoValue,
        0 AS RevenueToDateValue,
        0 AS TotalRevenue,
        0 AS ValueData,
        0 AS BudgetHours,
        0 AS ChargedHours,
        0 AS FYTDHours,
        0 AS AdditionalHours,
        0 AS ExpenseBudget,
        0 AS ExpensesToDate,
        0 AS FYTDExpenses,
        0 AS ToDateMargin,
        0 AS FYTDMargin,
        0 AS MarginPercentage,
        mgr.PrimaryManagerId,
        mgr.PrimaryManagerName,
        papd.PrimaryPapdId,
        papd.PrimaryPapdName,
        CURRENT_TIMESTAMP AS LastRefresh
    FROM Engagements e
    CROSS JOIN FiscalYears fy
    LEFT JOIN Customers c ON c.Id = e.CustomerId
    LEFT JOIN (
        SELECT ema.EngagementId,
               MIN(m.Id) AS PrimaryManagerId,
               GROUP_CONCAT(DISTINCT m.Name ORDER BY m.Name SEPARATOR ', ') AS PrimaryManagerName
        FROM EngagementManagerAssignments ema
        INNER JOIN Managers m ON m.Id = ema.ManagerId
        GROUP BY ema.EngagementId
    ) mgr ON mgr.EngagementId = e.Id
    LEFT JOIN (
        SELECT ep.EngagementId,
               MIN(p.Id) AS PrimaryPapdId,
               GROUP_CONCAT(DISTINCT p.Name ORDER BY p.Name SEPARATOR ', ') AS PrimaryPapdName
        FROM EngagementPapds ep
        INNER JOIN Papds p ON p.Id = ep.PapdId
        GROUP BY ep.EngagementId
    ) papd ON papd.EngagementId = e.Id
    WHERE NOT EXISTS (
        SELECT 1
        FROM EngagementLatestFinancials existing
        WHERE existing.EngagementId = e.Id
          AND existing.FiscalYearId = fy.Id
    );

    COMMIT;
END$$
DELIMITER ;

-- ===========================================================
-- Procedure: sp_RefreshAllMaterializedTables
-- ===========================================================

-- Table: CustomerSummaryCache - materialized customer-level dashboard snapshot.
CREATE TABLE `CustomerSummaryCache` (
    `CustomerId` INT NOT NULL,
    `CustomerName` VARCHAR(200) NOT NULL,
    `CustomerCode` VARCHAR(20) NOT NULL,
    `TotalEngagements` INT DEFAULT 0,
    `ActiveEngagements` INT DEFAULT 0,
    `RevenueToGoValue` DECIMAL(18, 2) DEFAULT 0,
    `RevenueToDateValue` DECIMAL(18, 2) DEFAULT 0,
    `TotalRevenue` DECIMAL(18, 2) DEFAULT 0,
    `ValueData` DECIMAL(18, 2) DEFAULT 0,
    `ChargedHours` DECIMAL(18, 2) DEFAULT 0,
    `FYTDHours` DECIMAL(18, 2) DEFAULT 0,
    `FYTDExpenses` DECIMAL(18, 2) DEFAULT 0,
    `FYTDMargin` DECIMAL(18, 2) DEFAULT 0,
    `TotalInvoiced` DECIMAL(18, 2) DEFAULT 0,
    `OutstandingInvoices` INT DEFAULT 0,
    `LastActivityDate` DATETIME NULL,
    `LatestClosingPeriodEnd` DATETIME(6) NULL,
    CONSTRAINT `PK_CustomerSummaryCache` PRIMARY KEY (`CustomerId`),
    INDEX `IX_CustomerSummaryCache_Code` (`CustomerCode`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

DROP PROCEDURE IF EXISTS sp_RefreshAllMaterializedTables;
DELIMITER $$

CREATE PROCEDURE sp_RefreshAllMaterializedTables()
BEGIN
    CALL sp_RefreshEngagementLatestFinancials();
    CALL sp_RefreshPapdRevenueSummary();
    CALL sp_RefreshManagerRevenueSummary();
    CALL sp_RefreshCustomerSummaryCache();
END$$
DELIMITER ;



DELIMITER $$
CREATE PROCEDURE sp_RefreshCustomerSummaryCache()
BEGIN
    DELETE FROM CustomerSummaryCache
    WHERE CustomerId IS NOT NULL;
    INSERT INTO CustomerSummaryCache (
        CustomerId, CustomerName, CustomerCode, TotalEngagements, ActiveEngagements,
        RevenueToGoValue, RevenueToDateValue, TotalRevenue, ValueData,
        ChargedHours, FYTDHours, FYTDExpenses, FYTDMargin,
        TotalInvoiced, OutstandingInvoices, LastActivityDate, LatestClosingPeriodEnd
    )
    SELECT
        c.Id,
        c.Name,
        c.CustomerCode,
        COUNT(DISTINCT e.Id) AS TotalEngagements,
        COUNT(DISTINCT CASE WHEN e.Status = 1 THEN e.Id END) AS ActiveEngagements,
        COALESCE(SUM(elf_summary.RevenueToGoValue), 0) AS RevenueToGoValue,
        COALESCE(SUM(elf_summary.RevenueToDateValue), 0) AS RevenueToDateValue,
        COALESCE(SUM(elf_summary.TotalRevenue), 0) AS TotalRevenue,
        COALESCE(SUM(elf_summary.ValueData), 0) AS ValueData,
        COALESCE(SUM(elf_summary.ChargedHours), 0) AS ChargedHours,
        COALESCE(SUM(elf_summary.FYTDHours), 0) AS FYTDHours,
        COALESCE(SUM(elf_summary.FYTDExpenses), 0) AS FYTDExpenses,
        COALESCE(SUM(elf_summary.FYTDMargin), 0) AS FYTDMargin,
        COALESCE(SUM(inv_totals.TotalInvoiced), 0) AS TotalInvoiced,
        COALESCE(SUM(inv_totals.OutstandingInvoices), 0) AS OutstandingInvoices,
        GREATEST(
            COALESCE(MAX(ae.MaxActualDate), '1900-01-01 00:00:00'),
            COALESCE(MAX(inv_totals.MaxPlanUpdate), '1900-01-01 00:00:00'),
            COALESCE(MAX(elf_summary.LatestClosingPeriodEnd), '1900-01-01 00:00:00')
        ) AS LastActivityDate,
        MAX(elf_summary.LatestClosingPeriodEnd) AS LatestClosingPeriodEnd
    FROM Customers c
    LEFT JOIN Engagements e ON e.CustomerId = c.Id
    LEFT JOIN (
        SELECT
            EngagementId,
            SUM(RevenueToGoValue) AS RevenueToGoValue,
            SUM(RevenueToDateValue) AS RevenueToDateValue,
            SUM(TotalRevenue) AS TotalRevenue,
            SUM(ValueData) AS ValueData,
            SUM(ChargedHours) AS ChargedHours,
            SUM(FYTDHours) AS FYTDHours,
            SUM(FYTDExpenses) AS FYTDExpenses,
            SUM(FYTDMargin) AS FYTDMargin,
            MAX(ClosingPeriodEnd) AS LatestClosingPeriodEnd
        FROM EngagementLatestFinancials
        GROUP BY EngagementId
    ) elf_summary ON elf_summary.EngagementId = e.Id
    LEFT JOIN (
        SELECT EngagementId, MAX(Date) AS MaxActualDate
        FROM ActualsEntries
        GROUP BY EngagementId
    ) ae ON ae.EngagementId = e.Id
    LEFT JOIN (
        SELECT e2.Id AS EngagementId,
               SUM(CASE WHEN ii.Status = 'Emitted' THEN ii.Amount ELSE 0 END) AS TotalInvoiced,
               SUM(CASE WHEN ii.Status NOT IN ('Emitted','Canceled') THEN 1 ELSE 0 END) AS OutstandingInvoices,
               MAX(ip.UpdatedAt) AS MaxPlanUpdate
        FROM Engagements e2
        LEFT JOIN InvoicePlan ip ON ip.EngagementId = e2.EngagementId
        LEFT JOIN InvoiceItem ii ON ii.PlanId = ip.Id
        GROUP BY e2.Id
    ) inv_totals ON inv_totals.EngagementId = e.Id
    GROUP BY c.Id, c.Name, c.CustomerCode;
END$$
DELIMITER ;


-- ===========================================================
-- Triggers: keep dashboard materializations in sync with FinancialEvolution changes
-- ===========================================================

DROP TRIGGER IF EXISTS trg_FinancialEvolution_AfterInsert;
DELIMITER $$
CREATE TRIGGER trg_FinancialEvolution_AfterInsert
AFTER INSERT ON FinancialEvolution
FOR EACH ROW
BEGIN
    CALL sp_RefreshAllMaterializedTables();
END$$
DELIMITER ;

DROP TRIGGER IF EXISTS trg_FinancialEvolution_AfterUpdate;
DELIMITER $$
CREATE TRIGGER trg_FinancialEvolution_AfterUpdate
AFTER UPDATE ON FinancialEvolution
FOR EACH ROW
BEGIN
    CALL sp_RefreshAllMaterializedTables();
END$$
DELIMITER ;

DROP TRIGGER IF EXISTS trg_FinancialEvolution_AfterDelete;
DELIMITER $$
CREATE TRIGGER trg_FinancialEvolution_AfterDelete
AFTER DELETE ON FinancialEvolution
FOR EACH ROW
BEGIN
    CALL sp_RefreshAllMaterializedTables();
END$$
DELIMITER ;


-- ================================================================
-- SECTION 7: SEED DATA
-- ================================================================
TRUNCATE TABLE RankMappings;

INSERT INTO RankMappings (RankCode, RankName, SpreadsheetRank, CreatedAt, IsActive, LastSeenAt)
VALUES
    ('04-PARTNER', 'PARTNER', 'PARTNER', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('05-EXEC DIRECTOR 1', 'EXEC DIRECTOR', 'EXEC DIRECTOR 1', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('06-SENIOR MANAGER 2', 'SENIOR MANAGER', 'SENIOR MANAGER 2', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('07-SENIOR MANAGER 1', 'SENIOR MANAGER', 'SENIOR MANAGER 1', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('08-MANAGER 3', 'MANAGER', 'MANAGER 3', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('09-MANAGER 2', 'MANAGER', 'MANAGER 2', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('10 -MANAGER 1', 'MANAGER', 'MANAGER 1', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('11-SENIOR 3', 'SENIOR', 'SENIOR 3', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('12-SENIOR 2', 'SENIOR', 'SENIOR 2', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('13-SENIOR 1', 'SENIOR', 'SENIOR 1', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('14-STAFF 3', 'STAFF', 'STAFF 3', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('15-STAFF 2', 'STAFF', 'STAFF 2', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('16-STAFF 1', 'STAFF', 'STAFF 1', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00'),
    ('17-ASSISTANT', 'ASSISTANT', 'ASSISTANT', '2024-01-01 00:00:00', 1, '2024-01-01 00:00:00');


INSERT INTO `FiscalYears` (`Name`, `StartDate`, `EndDate`, `AreaSalesTarget`, `AreaRevenueTarget`) VALUES
    ('FY25', '2024-07-01 00:00:00', '2025-06-30 23:59:59', 20000000, 23000000),
    ('FY26', '2025-07-01 00:00:00', '2026-06-30 23:59:59', 20000000, 23000000),
    ('FY27', '2026-07-01 00:00:00', '2027-06-30 23:59:59', 0, 0);
    
INSERT INTO `ClosingPeriods` (`Name`, `FiscalYearId`, `PeriodStart`, `PeriodEnd`) VALUES
    ('2025-09', 2, '2025-09-01 00:00:00', '2025-09-30 23:59:59'),
    ('2025-10', 2, '2025-10-01 00:00:00', '2025-10-31 23:59:59');

INSERT INTO `Managers` 
    (`Name`, `Email`, `Position`, `EngagementManagerGui`,`WindowsLogin`)
VALUES
    ('Caio Jordão Calisto', 'caio.calisto@br.ey.com', 'SeniorManager', '25008292', 'SA\\caio.calisto'),
    ('Gabriel Cortezia', 'gabriel.cortezia@br.ey.com', 'SeniorManager', '2201322', 'SA\\FW734PN'),
    ('Rafael Gimenis', 'rafael.gimenis@br.ey.com', 'SeniorManager', '25004546', 'SA\\rafael.gimenis'),
    ('Salomão Bruno', 'salomao.bruno@br.ey.com', 'SeniorManager', '2151828', 'SA\\salomao.bruno'),
    ('Mariana Galegale', 'mariana.galegale@br.ey.com', 'Manager', '3048987', 'SA\\BG536BP'),
    ('Thomas Lima', 'thomas.lima@br.ey.com', 'Manager', '3093401', 'SA\\JA983DJ'),
    ('Vinicius Almeida', 'vinicius.almeida@br.ey.com', 'Manager', '', 'SA\\MP311WS');
    
    
INSERT INTO `Papds`
    (`Name`, `Level`, `EngagementPapdGUI`, `WindowsLogin`)
VALUES
    ('Danilo Passos', 'AssociatePartner','25001571',  'SA\\danilo.passos'),
    ('Fernando São Pedro', 'Director', '25002618', 'SA\\fernando.sao-pedro'),
    ('Alexandre Jucá de Paiva', 'AssociatePartner', '25003546', 'SA\\RG563KA');

INSERT INTO `Engagements` (`EngagementId`, `Source`, `Description`, `OpeningValue`, `Status`) VALUES
    ('E-69062277', 'GrcProject', 'Project 1', 0, 1),
    ('E-69069706', 'GrcProject', 'Project 2', 0, 1),
    ('E-68737076', 'GrcProject', 'Project 3', 0, 1),
    ('E-69419868', 'GrcProject', 'Project 4', 0, 1),
    ('E-68890312', 'S4Project', 'Project 5', 0, 1),
    ('E-68806990', 'S4Project', 'Project 6', 0, 1),
    ('E-67004338', 'GrcProject', 'Project 7', 0, 1),
    ('E-69355617', 'GrcProject', 'Project 8', 0, 1),
    ('E-68172281', 'GrcProject', 'Project 9', 0, 1),
    ('E-68543849', 'GrcProject', 'Project 10', 0, 1),
    ('E-68631643', 'GrcProject', 'Project 11', 0, 1),
    ('E-68462036', 'GrcProject', 'Project 12', 0, 1),
    ('E-68611841', 'S4Project', 'Project 13', 0, 1),
    ('E-69121330', 'GrcProject', 'Project 14', 0, 1),
    ('E-69288339', 'GrcProject', 'Project 15', 0, 1),
    ('E-68230167', 'GrcProject', 'Project 16', 0, 1),
    ('E-68251032', 'GrcProject', 'Project 17', 0, 1),
    ('E-69067946', 'GrcProject', 'Project 18', 0, 1),
    ('E-68964575', 'GrcProject', 'Project 19', 0, 1);

-- ================================================================
-- SECTION 8: REFRESH MATERIALIZED TABLES
-- ================================================================

CALL sp_RefreshAllMaterializedTables();

-- ================================================================
-- SECTION 9: ANALYZE TABLES
-- ================================================================

ANALYZE TABLE Engagements;
ANALYZE TABLE FinancialEvolution;
ANALYZE TABLE EngagementRankBudgets;
ANALYZE TABLE ActualsEntries;
ANALYZE TABLE EngagementFiscalYearRevenueAllocations;

SET FOREIGN_KEY_CHECKS = 1;
CREATE OR REPLACE VIEW `vw_PapdRevenueSummary` AS
SELECT
    FiscalYearId,
    FiscalYearName,
    PapdId,
    PapdName,
    TotalToDateValue,
    TotalToGoValue,
    TotalValue,
    ChargedHours,
    FYTDHours,
    FYTDExpenses,
    FYTDMargin,
    LatestClosingPeriodEnd
FROM PapdRevenueSummary;

