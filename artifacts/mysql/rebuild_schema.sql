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
    `Id`           INT           NOT NULL AUTO_INCREMENT,
    `Name`         VARCHAR(200)  NOT NULL,
    `ClientIdText` VARCHAR(64)   NULL,
    CONSTRAINT `PK_Customers` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Customers_Name` UNIQUE (`Name`),
    CONSTRAINT `UX_Customers_ClientIdText` UNIQUE (`ClientIdText`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `ClosingPeriods`
(
    `Id`            INT           NOT NULL AUTO_INCREMENT,
    `Name`          VARCHAR(100)  NOT NULL,
    `PeriodStart`   DATE          NOT NULL,
    `PeriodEnd`     DATE          NOT NULL,
    CONSTRAINT `PK_ClosingPeriods` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_ClosingPeriods_Name` UNIQUE (`Name`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Papds`
(
    `Id`    INT          NOT NULL AUTO_INCREMENT,
    `Name`  VARCHAR(200) NOT NULL,
    `Level` VARCHAR(50)  NOT NULL,
    CONSTRAINT `PK_Papds` PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Engagements`
(
    `Id`                 INT            NOT NULL AUTO_INCREMENT,
    `EngagementId`       VARCHAR(64)    NOT NULL,
    `Description`        VARCHAR(255)   NOT NULL,
    `CustomerKey`        VARCHAR(64)    NOT NULL,
    `Currency`           VARCHAR(16)    NOT NULL DEFAULT '',
    `MarginPctBudget`    DECIMAL(9, 4)  NULL,
    `MarginPctEtcp`      DECIMAL(9, 4)  NULL,
    `EtcpAgeDays`        INT            NULL,
    `LatestEtcDate`      DATETIME(6)    NULL,
    `NextEtcDate`        DATETIME(6)    NULL,
    `StatusText`         VARCHAR(100)   NULL,
    `CustomerId`         INT            NULL,
    `OpeningMargin`      DECIMAL(18, 2) NOT NULL,
    `OpeningValue`       DECIMAL(18, 2) NOT NULL,
    `Status`             INT            NOT NULL,
    `InitialHoursBudget` DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `ActualHours`        DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `TotalPlannedHours`  DOUBLE         NOT NULL,
    CONSTRAINT `PK_Engagements` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Engagements_EngagementId` UNIQUE (`EngagementId`),
    CONSTRAINT `FK_Engagements_Customers` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `EngagementPapds`
(
    `Id`            INT  NOT NULL AUTO_INCREMENT,
    `EngagementId`  INT  NOT NULL,
    `PapdId`        INT  NOT NULL,
    `EffectiveDate` DATE NOT NULL,
    CONSTRAINT `PK_EngagementPapds` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementPapds_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementPapds_Papds` FOREIGN KEY (`PapdId`) REFERENCES `Papds` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementPapds_Assignment` UNIQUE (`EngagementId`, `PapdId`, `EffectiveDate`)
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

CREATE TABLE `MarginEvolutions`
(
    `Id`               INT           NOT NULL AUTO_INCREMENT,
    `EngagementId`     INT           NOT NULL,
    `EntryType`        INT           NOT NULL,
    `MarginPercentage` DECIMAL(9, 4) NOT NULL,
    `CreatedAtUtc`     DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `EffectiveDate`    DATETIME(6)   NULL,
    `ClosingPeriodId`  INT           NULL,
    CONSTRAINT `PK_MarginEvolutions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_MarginEvolutions_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_MarginEvolutions_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE SET NULL,
    INDEX `IX_MarginEvolutions_EngagementTypeCreated` (`EngagementId`, `EntryType`, `CreatedAtUtc`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `PlannedAllocations`
(
    `Id`              INT     NOT NULL AUTO_INCREMENT,
    `EngagementId`    INT     NOT NULL,
    `ClosingPeriodId` INT     NOT NULL,
    `AllocatedHours`  DOUBLE  NOT NULL,
    CONSTRAINT `PK_PlannedAllocations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PlannedAllocations_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_PlannedAllocations_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_PlannedAllocations_EngagementPeriod` UNIQUE (`EngagementId`, `ClosingPeriodId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `ActualsEntries`
(
    `Id`              INT           NOT NULL AUTO_INCREMENT,
    `EngagementId`    INT           NOT NULL,
    `PapdId`          INT           NULL,
    `ClosingPeriodId` INT           NOT NULL,
    `Date`            DATE          NOT NULL,
    `Hours`           DOUBLE        NOT NULL,
    `ImportBatchId`   VARCHAR(100)  NOT NULL,
    CONSTRAINT `PK_ActualsEntries` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ActualsEntries_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ActualsEntries_Papds` FOREIGN KEY (`PapdId`) REFERENCES `Papds` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_ActualsEntries_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE RESTRICT
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Exceptions`
(
    `Id`        INT          NOT NULL AUTO_INCREMENT,
    `Timestamp` timestamp(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `SourceFile` VARCHAR(260) NOT NULL,
    `RowData`    TEXT        NOT NULL,
    `Reason`     VARCHAR(500) NOT NULL,
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
    `StartDate`         DATE            NOT NULL,
    `EndDate`           DATE            NOT NULL,
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

INSERT INTO `ClosingPeriods` (`Name`, `PeriodStart`, `PeriodEnd`) VALUES
    ('2024-07', '2024-07-01', '2024-07-31'),
    ('2024-08', '2024-08-01', '2024-08-31'),
    ('2024-09', '2024-09-01', '2024-09-30'),
    ('2024-10', '2024-10-01', '2024-10-31'),
    ('2024-11', '2024-11-01', '2024-11-30'),
    ('2024-12', '2024-12-01', '2024-12-31'),
    ('2025-01', '2025-01-01', '2025-01-31'),
    ('2025-02', '2025-02-01', '2025-02-28'),
    ('2025-03', '2025-03-01', '2025-03-31'),
    ('2025-04', '2025-04-01', '2025-04-30'),
    ('2025-05', '2025-05-01', '2025-05-31'),
    ('2025-06', '2025-06-01', '2025-06-30'),
    ('2025-07', '2025-07-01', '2025-07-31'),
    ('2025-08', '2025-08-01', '2025-08-31'),
    ('2025-09', '2025-09-01', '2025-09-30'),
    ('2025-10', '2025-10-01', '2025-10-31'),
    ('2025-11', '2025-11-01', '2025-11-30'),
    ('2025-12', '2025-12-01', '2025-12-31'),
    ('2026-01', '2026-01-01', '2026-01-31'),
    ('2026-02', '2026-02-01', '2026-02-28'),
    ('2026-03', '2026-03-01', '2026-03-31'),
    ('2026-04', '2026-04-01', '2026-04-30'),
    ('2026-05', '2026-05-01', '2026-05-31'),
    ('2026-06', '2026-06-01', '2026-06-30'),
    ('2026-07', '2026-07-01', '2026-07-31'),
    ('2026-08', '2026-08-01', '2026-08-31'),
    ('2026-09', '2026-09-01', '2026-09-30'),
    ('2026-10', '2026-10-01', '2026-10-31'),
    ('2026-11', '2026-11-01', '2026-11-30'),
    ('2026-12', '2026-12-01', '2026-12-31'),
    ('2027-01', '2027-01-01', '2027-01-31'),
    ('2027-02', '2027-02-01', '2027-02-28'),
    ('2027-03', '2027-03-01', '2027-03-31'),
    ('2027-04', '2027-04-01', '2027-04-30'),
    ('2027-05', '2027-05-01', '2027-05-31'),
    ('2027-06', '2027-06-01', '2027-06-30'),
    ('2027-07', '2027-07-01', '2027-07-31'),
    ('2027-08', '2027-08-01', '2027-08-31'),
    ('2027-09', '2027-09-01', '2027-09-30'),
    ('2027-10', '2027-10-01', '2027-10-31'),
    ('2027-11', '2027-11-01', '2027-11-30'),
    ('2027-12', '2027-12-01', '2027-12-31'),
    ('2028-01', '2028-01-01', '2028-01-31'),
    ('2028-02', '2028-02-01', '2028-02-29'),
    ('2028-03', '2028-03-01', '2028-03-31'),
    ('2028-04', '2028-04-01', '2028-04-30'),
    ('2028-05', '2028-05-01', '2028-05-31'),
    ('2028-06', '2028-06-01', '2028-06-30'),
    ('2028-07', '2028-07-01', '2028-07-31'),
    ('2028-08', '2028-08-01', '2028-08-31'),
    ('2028-09', '2028-09-01', '2028-09-30'),
    ('2028-10', '2028-10-01', '2028-10-31'),
    ('2028-11', '2028-11-01', '2028-11-30'),
    ('2028-12', '2028-12-01', '2028-12-31'),
    ('2029-01', '2029-01-01', '2029-01-31'),
    ('2029-02', '2029-02-01', '2029-02-28'),
    ('2029-03', '2029-03-01', '2029-03-31'),
    ('2029-04', '2029-04-01', '2029-04-30'),
    ('2029-05', '2029-05-01', '2029-05-31'),
    ('2029-06', '2029-06-01', '2029-06-30'),
    ('2029-07', '2029-07-01', '2029-07-31'),
    ('2029-08', '2029-08-01', '2029-08-31'),
    ('2029-09', '2029-09-01', '2029-09-30'),
    ('2029-10', '2029-10-01', '2029-10-31'),
    ('2029-11', '2029-11-01', '2029-11-30'),
    ('2029-12', '2029-12-01', '2029-12-31'),
    ('2030-01', '2030-01-01', '2030-01-31'),
    ('2030-02', '2030-02-01', '2030-02-28'),
    ('2030-03', '2030-03-01', '2030-03-31'),
    ('2030-04', '2030-04-01', '2030-04-30'),
    ('2030-05', '2030-05-01', '2030-05-31'),
    ('2030-06', '2030-06-01', '2030-06-30'),
    ('2030-07', '2030-07-01', '2030-07-31'),
    ('2030-08', '2030-08-01', '2030-08-31'),
    ('2030-09', '2030-09-01', '2030-09-30'),
    ('2030-10', '2030-10-01', '2030-10-31'),
    ('2030-11', '2030-11-01', '2030-11-30'),
    ('2030-12', '2030-12-01', '2030-12-31'),
    ('2031-01', '2031-01-01', '2031-01-31'),
    ('2031-02', '2031-02-01', '2031-02-28'),
    ('2031-03', '2031-03-01', '2031-03-31'),
    ('2031-04', '2031-04-01', '2031-04-30'),
    ('2031-05', '2031-05-01', '2031-05-31'),
    ('2031-06', '2031-06-01', '2031-06-30');

INSERT INTO `FiscalYears` (`Name`, `StartDate`, `EndDate`, `AreaSalesTarget`, `AreaRevenueTarget`) VALUES
    ('FY25', '2024-07-01', '2025-06-30', 0, 0),
    ('FY26', '2025-07-01', '2026-06-30', 0, 0),
    ('FY27', '2026-07-01', '2027-06-30', 0, 0),
    ('FY28', '2027-07-01', '2028-06-30', 0, 0),
    ('FY29', '2028-07-01', '2029-06-30', 0, 0),
    ('FY30', '2029-07-01', '2030-06-30', 0, 0),
    ('FY31', '2030-07-01', '2031-06-30', 0, 0);

SET FOREIGN_KEY_CHECKS = 1;

