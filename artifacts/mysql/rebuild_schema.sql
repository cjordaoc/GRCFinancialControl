-- Drop ALL base tables in the current database in one statement

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



CREATE TABLE `Customers`
(
    `Id`          INT           NOT NULL AUTO_INCREMENT,
    `Name`        VARCHAR(200)  NOT NULL,
    `CustomerCode` VARCHAR(20)   NOT NULL,
    CONSTRAINT `PK_Customers` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Customers_CustomerCode` UNIQUE (`CustomerCode`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `ClosingPeriods`
(
    `Id`            INT           NOT NULL AUTO_INCREMENT,
    `Name`          VARCHAR(100)  NOT NULL,
    `PeriodStart`   DATETIME(6)   NOT NULL,
    `PeriodEnd`     DATETIME(6)   NOT NULL,
    CONSTRAINT `PK_ClosingPeriods` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_ClosingPeriods_Name` UNIQUE (`Name`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Papds`
(
    `Id`    INT          NOT NULL AUTO_INCREMENT,
    `Name`  VARCHAR(200) NOT NULL,
    `Level` VARCHAR(100) NOT NULL,
    CONSTRAINT `PK_Papds` PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Managers`
(
    `Id`       INT           NOT NULL AUTO_INCREMENT,
    `Name`     VARCHAR(200)  NOT NULL,
    `Email`    VARCHAR(254)  NOT NULL,
    `Position` VARCHAR(50)   NOT NULL,
    CONSTRAINT `PK_Managers` PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Engagements`
(
    `Id`                 INT            NOT NULL AUTO_INCREMENT,
    `EngagementId`       VARCHAR(64)    NOT NULL,
    `Description`        VARCHAR(255)   NOT NULL,
    `Currency`           VARCHAR(16)    NOT NULL DEFAULT '',
    `MarginPctBudget`    DECIMAL(9, 4)  NULL,
    `MarginPctEtcp`      DECIMAL(9, 4)  NULL,
    `LastEtcDate`        DATETIME(6)    NULL,
    `ProposedNextEtcDate` DATETIME(6)   NULL,
    `StatusText`         VARCHAR(100)   NULL,
    `CustomerId`         INT            NULL,
    `OpeningValue`       DECIMAL(18, 2) NOT NULL,
    `OpeningExpenses`    DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `Status`             INT            NOT NULL,
    `Source`             VARCHAR(20)    NOT NULL DEFAULT 'GrcProject',
    `InitialHoursBudget` DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `EtcpHours`          DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `ValueEtcp`          DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `ExpensesEtcp`       DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `LastClosingPeriodId` VARCHAR(100)  NULL,
    CONSTRAINT `PK_Engagements` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Engagements_EngagementId` UNIQUE (`EngagementId`),
    CONSTRAINT `FK_Engagements_Customers` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `EngagementPapds`
(
    `Id`            INT  NOT NULL AUTO_INCREMENT,
    `EngagementId`  INT  NOT NULL,
    `PapdId`        INT  NOT NULL,
    `EffectiveDate` DATETIME(6) NOT NULL,
    CONSTRAINT `PK_EngagementPapds` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementPapds_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementPapds_Papds` FOREIGN KEY (`PapdId`) REFERENCES `Papds` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementPapds_Assignment` UNIQUE (`EngagementId`, `PapdId`, `EffectiveDate`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `EngagementManagerAssignments`
(
    `Id`           INT         NOT NULL AUTO_INCREMENT,
    `EngagementId` INT         NOT NULL,
    `ManagerId`    INT         NOT NULL,
    `BeginDate`    DATETIME(6) NOT NULL,
    `EndDate`      DATETIME(6) NULL,
    CONSTRAINT `PK_EngagementManagerAssignments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementManagerAssignments_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementManagerAssignments_Managers` FOREIGN KEY (`ManagerId`) REFERENCES `Managers` (`Id`) ON DELETE CASCADE,
    INDEX `IX_EngagementManagerAssignments_Engagement_Manager_Begin` (`EngagementId`, `ManagerId`, `BeginDate`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `EngagementRankBudgets`
(
    `Id`            BIGINT          NOT NULL AUTO_INCREMENT,
    `EngagementId`  INT             NOT NULL,
    `RankName`      VARCHAR(100)    NOT NULL,
    `Hours`         DECIMAL(18, 2)  NOT NULL DEFAULT 0,
    `CreatedAtUtc`  DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAtUtc`  DATETIME(6)     NULL,
    CONSTRAINT `PK_EngagementRankBudgets` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementRankBudgets_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementRankBudgets_EngagementRank` UNIQUE (`EngagementId`, `RankName`),
    INDEX `IX_EngagementRankBudgets_EngagementId` (`EngagementId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `FinancialEvolution`
(
    `Id`              INT            NOT NULL AUTO_INCREMENT,
    `ClosingPeriodId` VARCHAR(100)   NOT NULL,
    `EngagementId`    VARCHAR(64)    NOT NULL,
    `HoursData`       DECIMAL(18, 2) NULL,
    `ValueData`       DECIMAL(18, 2) NULL,
    `MarginData`      DECIMAL(9, 4)  NULL,
    `ExpenseData`     DECIMAL(18, 2) NULL,
    CONSTRAINT `PK_FinancialEvolution` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_FinancialEvolution_Key` UNIQUE (`EngagementId`, `ClosingPeriodId`),
    CONSTRAINT `FK_FinancialEvolution_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`EngagementId`) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `PlannedAllocations`
(
    `Id`              INT            NOT NULL AUTO_INCREMENT,
    `EngagementId`    INT            NOT NULL,
    `ClosingPeriodId` INT            NOT NULL,
    `AllocatedHours`  DECIMAL(18, 2) NOT NULL,
    CONSTRAINT `PK_PlannedAllocations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PlannedAllocations_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_PlannedAllocations_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_PlannedAllocations_EngagementPeriod` UNIQUE (`EngagementId`, `ClosingPeriodId`)
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
    INDEX `IX_ActualsEntries_ClosingPeriodId` (`ClosingPeriodId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Exceptions`
(
    `Id`         INT           NOT NULL AUTO_INCREMENT,
    `Timestamp`  DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `SourceFile` VARCHAR(260)  NOT NULL,
    `RowData`    TEXT          NOT NULL,
    `Reason`     VARCHAR(500)  NOT NULL,
    CONSTRAINT `PK_Exceptions` PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Settings`
(
    `Id`    INT           NOT NULL AUTO_INCREMENT,
    `Key`   VARCHAR(128)  NOT NULL,
    `Value` TEXT          NOT NULL,
    CONSTRAINT `PK_Settings` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Settings_Key` UNIQUE (`Key`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `FiscalYears`
(
    `Id`                INT             NOT NULL AUTO_INCREMENT,
    `Name`              VARCHAR(100)    NOT NULL,
    `StartDate`         DATETIME(6)     NOT NULL,
    `EndDate`           DATETIME(6)     NOT NULL,
    `AreaSalesTarget`   DECIMAL(18, 2)  NOT NULL DEFAULT 0,
    `AreaRevenueTarget` DECIMAL(18, 2)  NOT NULL DEFAULT 0,
    CONSTRAINT `PK_FiscalYears` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_FiscalYears_Name` UNIQUE (`Name`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `EngagementFiscalYearAllocations`
(
    `Id`             INT             NOT NULL AUTO_INCREMENT,
    `EngagementId`   INT             NOT NULL,
    `FiscalYearId`   INT             NOT NULL,
    `PlannedHours`   DECIMAL(18, 2)  NOT NULL,
    CONSTRAINT `PK_EngagementFiscalYearAllocations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementFiscalYearAllocations_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementFiscalYearAllocations_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementFiscalYearAllocations_Allocation` UNIQUE (`EngagementId`, `FiscalYearId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `EngagementFiscalYearRevenueAllocations`
(
    `Id`             INT             NOT NULL AUTO_INCREMENT,
    `EngagementId`   INT             NOT NULL,
    `FiscalYearId`   INT             NOT NULL,
    `PlannedValue`   DECIMAL(18, 2)  NOT NULL,
    CONSTRAINT `PK_EngagementFiscalYearRevenueAllocations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementFiscalYearRevenueAllocations_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementFiscalYearRevenueAllocations_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementFiscalYearRevenueAllocations_Allocation` UNIQUE (`EngagementId`, `FiscalYearId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

INSERT INTO `ClosingPeriods` (`Name`, `PeriodStart`, `PeriodEnd`) VALUES
      ('2025-10', '2025-10-01 00:00:00', '2025-10-31 00:00:00'),
    ('2025-11', '2025-11-01 00:00:00', '2025-11-30 00:00:00'),
    ('2025-12', '2025-12-01 00:00:00', '2025-12-31 00:00:00'),
    ('2026-01', '2026-01-01 00:00:00', '2026-01-31 00:00:00');

INSERT INTO `FiscalYears` (`Name`, `StartDate`, `EndDate`, `AreaSalesTarget`, `AreaRevenueTarget`) VALUES
    ('FY25', '2024-07-01', '2025-06-30', 0, 0),
    ('FY26', '2025-07-01', '2026-06-30', 0, 0),
    ('FY27', '2026-07-01', '2027-06-30', 0, 0);
    
    INSERT INTO `blac3289_GRCFinancialControl`.`Papds` (`Name`,`Level`) VALUES
('Danilo Passos','AssociatePartner'),
('Fernando São Pedro','Director'),
('Alexandre Jucá de Paiva','AssociatePartner');

INSERT INTO `blac3289_GRCFinancialControl`.`Managers` (`Name`,`Email`,`Position`) VALUES
('Caio Jordão Calisto','caio.calisto@br.ey.com','SeniorManager'),
('Gabriel Cortezia','gabriel.cortezia@br.ey.com','SeniorManager'),
('Rafael Gimenis','rafael.gimenis@br.ey.com','SeniorManager'),
('Salomão Bruno','salomao.bruno@br.ey.com','SeniorManager'),
('Mariana Galegale','mariana.galegale@br.ey.com','Manager'),
('Thomas Lima','thomas.lima@br.ey.com','Manager'),
('Vinicius Almeida','vinicius.almeida@br.ey.com','SeniorManager');


SET FOREIGN_KEY_CHECKS = 1;

