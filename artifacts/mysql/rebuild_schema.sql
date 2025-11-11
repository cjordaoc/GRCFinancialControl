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

-- Uncomment these lines if you want to recreate the database completely
-- DROP DATABASE IF EXISTS blac3289_GRCFinancialControl;
-- CREATE DATABASE blac3289_GRCFinancialControl CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
-- USE blac3289_GRCFinancialControl;

-- If not recreating, just use the database
-- USE blac3289_GRCFinancialControl;

-- ================================================================
-- SECTION 2: DROP ALL EXISTING TABLES
-- ================================================================

SET SESSION group_concat_max_len = 1024 * 1024;
SET FOREIGN_KEY_CHECKS = 0;

-- Drop views first
DROP VIEW IF EXISTS vw_PapdRevenueSummary;
DROP VIEW IF EXISTS vw_ExecutiveDashboard;
DROP VIEW IF EXISTS vw_EngagementDetails;
DROP VIEW IF EXISTS vw_BudgetUtilization;
DROP VIEW IF EXISTS vw_InvoicePipeline;
DROP VIEW IF EXISTS vw_ManagerRevenueSummary;
DROP VIEW IF EXISTS vw_EngagementHoursSummary;
DROP VIEW IF EXISTS vw_FinancialEvolutionSummary;
DROP VIEW IF EXISTS vw_TableSizeMonitoring;

-- Drop procedures
DROP PROCEDURE IF EXISTS sp_RefreshEngagementLatestFinancials;
DROP PROCEDURE IF EXISTS sp_RefreshEngagementSummaryCache;
DROP PROCEDURE IF EXISTS sp_RefreshPapdRevenueSummary;
DROP PROCEDURE IF EXISTS sp_RefreshManagerRevenueSummary;
DROP PROCEDURE IF EXISTS sp_RefreshCustomerSummaryCache;
DROP PROCEDURE IF EXISTS sp_RefreshAllMaterializedTables;
DROP PROCEDURE IF EXISTS sp_GetEngagementSummary;
DROP PROCEDURE IF EXISTS sp_GetFiscalYearSummary;

-- Drop materialized tables
DROP TABLE IF EXISTS EngagementLatestFinancials;
DROP TABLE IF EXISTS EngagementSummaryCache;
DROP TABLE IF EXISTS PapdRevenueSummaryMaterialized;
DROP TABLE IF EXISTS ManagerRevenueSummaryMaterialized;
DROP TABLE IF EXISTS CustomerSummaryCache;

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

-- 3) (Optional) Disable FK checks to avoid dependency issues
SET FOREIGN_KEY_CHECKS = 0;

-- 4) Run it
PREPARE stmt FROM @drop_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;



/* ============================
   Core reference tables
   ============================ */

CREATE TABLE `Customers`
(
    `Id`           INT           NOT NULL AUTO_INCREMENT,
    `Name`         VARCHAR(200)  NOT NULL,
    `CustomerCode` VARCHAR(20)   NOT NULL,
    CONSTRAINT `PK_Customers` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Customers_CustomerCode` UNIQUE (`CustomerCode`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

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
CREATE TABLE `FinancialEvolution`
(
    `Id`                   INT            NOT NULL AUTO_INCREMENT,
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
    CONSTRAINT `FK_FinancialEvolution_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE SET NULL
    -- NOTE: Keeping ClosingPeriodId as VARCHAR(100) to preserve your current external key format.
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;


/* ============================
   Allocations (hours & revenue)
   ============================ */

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

CREATE TABLE `InvoicePlanEmail` (
  `Id`        INT NOT NULL AUTO_INCREMENT,
  `PlanId`    INT NOT NULL,
  `Email`     VARCHAR(200) NOT NULL,
  `CreatedAt` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  KEY `IX_InvoicePlanEmail_Plan` (`PlanId`),
  CONSTRAINT `FK_InvoicePlanEmail_Plan` FOREIGN KEY (`PlanId`) REFERENCES `InvoicePlan`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

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


DELIMITER $$
CREATE PROCEDURE sp_RefreshPapdRevenueSummary()
BEGIN
    TRUNCATE TABLE PapdRevenueSummaryMaterialized;
    INSERT INTO PapdRevenueSummaryMaterialized (
        FiscalYearId, FiscalYearName, PapdId, PapdName, PapdLevel,
        TotalToGoValue, TotalToDateValue, TotalValue, EngagementCount, ActiveEngagementCount
    )
    SELECT 
        fy.Id, fy.Name, p.Id, p.Name, p.Level,
        COALESCE(SUM(ea.ToGoValue), 0),
        COALESCE(SUM(ea.ToDateValue), 0),
        COALESCE(SUM(ea.ToGoValue), 0) + COALESCE(SUM(ea.ToDateValue), 0),
        COUNT(DISTINCT ep.EngagementId),
        COUNT(DISTINCT CASE WHEN e.Status = 1 THEN ep.EngagementId END)
    FROM FiscalYears fy
    CROSS JOIN Papds p
    LEFT JOIN EngagementPapds ep ON ep.PapdId = p.Id
    LEFT JOIN Engagements e ON e.Id = ep.EngagementId
    LEFT JOIN EngagementFiscalYearRevenueAllocations ea 
        ON ea.EngagementId = ep.EngagementId AND ea.FiscalYearId = fy.Id
    GROUP BY fy.Id, fy.Name, p.Id, p.Name, p.Level;
END$$
DELIMITER ;
DELIMITER $$
CREATE PROCEDURE sp_RefreshManagerRevenueSummary()
BEGIN
    TRUNCATE TABLE ManagerRevenueSummaryMaterialized;
    INSERT INTO ManagerRevenueSummaryMaterialized (
        FiscalYearId, FiscalYearName, ManagerId, ManagerName, ManagerPosition,
        TotalToGoValue, TotalToDateValue, TotalValue, EngagementCount, ActiveEngagementCount
    )
    SELECT 
        fy.Id, fy.Name, m.Id, m.Name, m.Position,
        COALESCE(SUM(ea.ToGoValue), 0),
        COALESCE(SUM(ea.ToDateValue), 0),
        COALESCE(SUM(ea.ToGoValue), 0) + COALESCE(SUM(ea.ToDateValue), 0),
        COUNT(DISTINCT ema.EngagementId),
        COUNT(DISTINCT CASE WHEN e.Status = 1 THEN ema.EngagementId END)
    FROM FiscalYears fy
    CROSS JOIN Managers m
    LEFT JOIN EngagementManagerAssignments ema ON ema.ManagerId = m.Id
    LEFT JOIN Engagements e ON e.Id = ema.EngagementId
    LEFT JOIN EngagementFiscalYearRevenueAllocations ea 
        ON ea.EngagementId = ema.EngagementId AND ea.FiscalYearId = fy.Id
    GROUP BY fy.Id, fy.Name, m.Id, m.Name, m.Position;
END$$
DELIMITER ;
USE blac3289_GRCFinancialControl;


USE blac3289_GRCFinancialControl;

USE blac3289_GRCFinancialControl;

-- ===========================================================
-- Procedure: sp_RefreshPapdRevenueSummary
-- Purpose  : Truncate & repopulate the physical summary table PapdRevenueSummary
-- ===========================================================

DROP PROCEDURE IF EXISTS sp_RefreshPapdRevenueSummary;
DELIMITER $$

CREATE PROCEDURE sp_RefreshPapdRevenueSummary()
BEGIN
    -- Ensure the summary table exists
    CREATE TABLE IF NOT EXISTS PapdRevenueSummary (
        FiscalYearId INT NOT NULL,
        FiscalYearName VARCHAR(100) NOT NULL,
        PapdId INT NOT NULL,
        PapdName VARCHAR(200) NOT NULL,
        TotalToGoValue DECIMAL(18,2) DEFAULT 0,
        TotalToDateValue DECIMAL(18,2) DEFAULT 0,
        TotalValue DECIMAL(18,2) DEFAULT 0,
        PRIMARY KEY (FiscalYearId, PapdId),
        INDEX IX_PapdRevenueSummary_FY (FiscalYearId),
        INDEX IX_PapdRevenueSummary_Papd (PapdId)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Truncate and repopulate
    TRUNCATE TABLE PapdRevenueSummary;

    INSERT INTO PapdRevenueSummary (
        FiscalYearId, FiscalYearName, PapdId, PapdName, 
        TotalToGoValue, TotalToDateValue, TotalValue
    )
    SELECT 
        fy.Id AS FiscalYearId,
        fy.Name AS FiscalYearName,
        p.Id AS PapdId,
        p.Name AS PapdName,
        COALESCE(SUM(ea.ToGoValue),0) AS TotalToGoValue,
        COALESCE(SUM(ea.ToDateValue),0) AS TotalToDateValue,
        COALESCE(SUM(ea.ToGoValue),0) + COALESCE(SUM(ea.ToDateValue),0) AS TotalValue
    FROM Papds p
    LEFT JOIN EngagementPapds ep ON ep.PapdId = p.Id
    LEFT JOIN Engagements e ON e.Id = ep.EngagementId
    LEFT JOIN EngagementFiscalYearRevenueAllocations ea ON ea.EngagementId = e.Id
    LEFT JOIN FiscalYears fy ON fy.Id = ea.FiscalYearId
    GROUP BY fy.Id, fy.Name, p.Id, p.Name
    ORDER BY fy.Name, p.Name;
END$$
DELIMITER ;


-- ===========================================================
-- Procedure: sp_RefreshEngagementLatestFinancials
-- ===========================================================

DROP PROCEDURE IF EXISTS sp_RefreshEngagementLatestFinancials;
DELIMITER $$

CREATE PROCEDURE sp_RefreshEngagementLatestFinancials()
BEGIN
    CREATE TABLE IF NOT EXISTS EngagementLatestFinancials (
        EngagementId INT NOT NULL,
        FiscalYearId INT NOT NULL,
        ClosingPeriodId INT NOT NULL,
        TotalRevenue DECIMAL(18,2) DEFAULT 0,
        TotalHours DECIMAL(18,2) DEFAULT 0,
        MarginPercentage DECIMAL(9,4) DEFAULT 0,
        LastRefresh TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
        PRIMARY KEY (EngagementId, ClosingPeriodId),
        INDEX IX_ELF_FY (FiscalYearId),
        INDEX IX_ELF_CP (ClosingPeriodId)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    TRUNCATE TABLE EngagementLatestFinancials;

    INSERT INTO EngagementLatestFinancials (
        EngagementId, FiscalYearId, ClosingPeriodId, TotalRevenue, TotalHours, MarginPercentage
    )
    SELECT 
        fe.EngagementId,
        fe.FiscalYearId,
        fe.ClosingPeriodId,
        COALESCE(MAX(IFNULL(fe.RevenueToGoValue,0) + IFNULL(fe.RevenueToDateValue,0)),0) AS TotalRevenue,
        COALESCE(MAX(IFNULL(fe.BudgetHours,0) + IFNULL(fe.AdditionalHours,0)),0) AS TotalHours,
        COALESCE(MAX((fe.ToDateMargin / NULLIF(fe.RevenueToDateValue,0)) * 100),0) AS MarginPercentage
    FROM FinancialEvolution fe
    INNER JOIN (
        SELECT EngagementId, MAX(ClosingPeriodId) AS LatestCP
        FROM FinancialEvolution
        GROUP BY EngagementId
    ) latest 
        ON latest.EngagementId = fe.EngagementId 
       AND latest.LatestCP = fe.ClosingPeriodId
    GROUP BY fe.EngagementId, fe.FiscalYearId, fe.ClosingPeriodId;
END$$
DELIMITER ;


-- ===========================================================
-- Procedure: sp_RefreshAllMaterializedTables
-- ===========================================================

DROP PROCEDURE IF EXISTS sp_RefreshAllMaterializedTables;
DELIMITER $$

CREATE PROCEDURE sp_RefreshAllMaterializedTables()
BEGIN
    CALL sp_RefreshEngagementLatestFinancials();
    CALL sp_RefreshPapdRevenueSummary();
END$$
DELIMITER ;



DELIMITER $$
CREATE PROCEDURE sp_RefreshCustomerSummaryCache()
BEGIN
    TRUNCATE TABLE CustomerSummaryCache;
    INSERT INTO CustomerSummaryCache (
        CustomerId, CustomerName, CustomerCode, TotalEngagements, ActiveEngagements,
        TotalRevenue, TotalInvoiced, OutstandingInvoices, LastActivityDate
    )
    SELECT 
        c.Id, c.Name, c.CustomerCode,
        COUNT(DISTINCT e.Id),
        COUNT(DISTINCT CASE WHEN e.Status = 1 THEN e.Id END),
        COALESCE((SELECT SUM(TotalValue) FROM EngagementFiscalYearRevenueAllocations ea
                  INNER JOIN Engagements e2 ON e2.Id = ea.EngagementId WHERE e2.CustomerId = c.Id), 0),
        COALESCE((SELECT SUM(ii.Amount) FROM InvoicePlan ip
                  INNER JOIN InvoiceItem ii ON ii.PlanId = ip.Id
                  INNER JOIN Engagements e3 ON e3.EngagementId = ip.EngagementId
                  WHERE e3.CustomerId = c.Id AND ii.Status = 'Emitted'), 0),
        COALESCE((SELECT COUNT(*) FROM InvoicePlan ip
                  INNER JOIN InvoiceItem ii ON ii.PlanId = ip.Id
                  INNER JOIN Engagements e4 ON e4.EngagementId = ip.EngagementId
                  WHERE e4.CustomerId = c.Id AND ii.Status NOT IN ('Emitted', 'Canceled')), 0),
        GREATEST(
            COALESCE((SELECT MAX(Date) FROM ActualsEntries ae 
                      INNER JOIN Engagements e5 ON e5.Id = ae.EngagementId 
                      WHERE e5.CustomerId = c.Id), '1900-01-01'),
            COALESCE((SELECT MAX(UpdatedAt) FROM InvoicePlan ip
                      INNER JOIN Engagements e6 ON e6.EngagementId = ip.EngagementId
                      WHERE e6.CustomerId = c.Id), '1900-01-01')
        )
    FROM Customers c
    LEFT JOIN Engagements e ON e.CustomerId = c.Id
    GROUP BY c.Id, c.Name, c.CustomerCode;
END$$
DELIMITER ;


-- ================================================================
-- SECTION 7: SEED DATA
-- ================================================================
INSERT INTO RankMappings (Id, RankCode, RankName, SpreadsheetRank, CreatedAt, IsActive, LastSeenAt)
VALUES (1, '05-EXEC DIRECTOR 1', 'EXEC DIRECTOR', 'EXEC DIRECTOR 1', '2025-11-04T15:32:31', 1, '2025-11-04T18:32:49.728508');

INSERT INTO RankMappings (Id, RankCode, RankName, SpreadsheetRank, CreatedAt, IsActive, LastSeenAt)
VALUES (2, '06-SENIOR MANAGER 2', 'SENIOR MANAGER', 'SENIOR MANAGER 2', '2025-11-04T15:32:31', 1, '2025-11-04T18:32:41.58033');

INSERT INTO RankMappings (Id, RankCode, RankName, SpreadsheetRank, CreatedAt, IsActive, LastSeenAt)
VALUES (3, '07-SENIOR MANAGER 1', 'SENIOR MANAGER', 'SENIOR MANAGER 1', '2025-11-04T15:32:31', 1, '2025-11-04T18:32:49.728508');

INSERT INTO RankMappings (Id, RankCode, RankName, SpreadsheetRank, CreatedAt, IsActive, LastSeenAt)
VALUES (4, '11-SENIOR 3', 'SENIOR', 'SENIOR 3', '2025-11-04T15:32:31', 1, '2025-11-04T18:32:49.728508');

INSERT INTO RankMappings (Id, RankCode, RankName, SpreadsheetRank, CreatedAt, IsActive, LastSeenAt)
VALUES (5, '12-SENIOR 2', 'SENIOR', 'SENIOR 2', '2025-11-04T15:32:31', 1, '2025-11-04T18:32:41.58033');

INSERT INTO RankMappings (Id, RankCode, RankName, SpreadsheetRank, CreatedAt, IsActive, LastSeenAt)
VALUES (6, '15-STAFF 2', 'STAFF', 'STAFF 2', '2025-11-04T15:32:31', 1, '2025-11-04T18:32:41.58033');





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
    
    
    INSERT INTO blac3289_GRCFinancialControl.Papds 
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
CREATE OR REPLACE ALGORITHM=UNDEFINED 
DEFINER=`blac3289_GRCFinControl`@`%` 
SQL SECURITY DEFINER 
VIEW `vw_PapdRevenueSummary` AS
SELECT 
    fy.Id AS FiscalYearId,
    fy.Name AS FiscalYearName,
    p.Id AS PapdId,
    p.Name AS PapdName,
    SUM(fe.RevenueToDateValue) AS TotalToDateValue,
    SUM(fe.RevenueToGoValue)   AS TotalToGoValue,
    SUM(fe.RevenueToDateValue + fe.RevenueToGoValue) AS TotalValue
FROM `blac3289_GRCFinancialControl`.`FinancialEvolution` fe
INNER JOIN (
    -- ✅ Latest ClosingPeriod per Engagement based on chronological EndDate
    SELECT 
        fe2.EngagementId,
        cp2.Id AS LatestClosingPeriodId
    FROM `blac3289_GRCFinancialControl`.`FinancialEvolution` fe2
    INNER JOIN `blac3289_GRCFinancialControl`.`ClosingPeriods` cp2 
        ON cp2.Id = fe2.ClosingPeriodId
    INNER JOIN (
        SELECT 
            fe3.EngagementId,
            MAX(cp3.PeriodEnd) AS MaxEndDate
        FROM `blac3289_GRCFinancialControl`.`FinancialEvolution` fe3
        INNER JOIN `blac3289_GRCFinancialControl`.`ClosingPeriods` cp3 
            ON cp3.Id = fe3.ClosingPeriodId
        GROUP BY fe3.EngagementId
    ) latest_dates
        ON latest_dates.EngagementId = fe2.EngagementId
       AND cp2.PeriodEnd = latest_dates.MaxEndDate
) latest
    ON latest.EngagementId = fe.EngagementId
   AND latest.LatestClosingPeriodId = fe.ClosingPeriodId
INNER JOIN `blac3289_GRCFinancialControl`.`Engagements` e 
    ON e.Id = fe.EngagementId
INNER JOIN `blac3289_GRCFinancialControl`.`EngagementPapds` ep 
    ON ep.EngagementId = e.Id
INNER JOIN `blac3289_GRCFinancialControl`.`Papds` p 
    ON p.Id = ep.PapdId
INNER JOIN `blac3289_GRCFinancialControl`.`FiscalYears` fy 
    ON fy.Id = fe.FiscalYearId
GROUP BY 
    fy.Id,
    fy.Name,
    p.Id,
    p.Name;

SELECT 'Database recreation complete with all optimizations!' AS Status;
SELECT 'Total tables created:' AS Info, COUNT(*) AS Count FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE';
SELECT 'Total views created:' AS Info, COUNT(*) AS Count FROM information_schema.VIEWS WHERE TABLE_SCHEMA = DATABASE();
SELECT 'Total indexes created:' AS Info, COUNT(DISTINCT INDEX_NAME) AS Count FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME != 'PRIMARY';
