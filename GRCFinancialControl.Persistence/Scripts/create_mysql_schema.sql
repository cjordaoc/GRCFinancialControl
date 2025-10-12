-- MySQL schema reset script generated from the domain POCOs.
-- Execute USE <database>; before running this script or replace the placeholder below.

SET FOREIGN_KEY_CHECKS = 0;
-- Dynamically drop any tables in the current schema that start with the TBL_ prefix
SET @drop_sql = (
    SELECT IFNULL(
        CONCAT('DROP TABLE IF EXISTS ', GROUP_CONCAT(CONCAT('`', table_name, '`'))),
        'SELECT 1'
    )
    FROM information_schema.tables
    WHERE table_schema = DATABASE()
      AND table_name LIKE 'TBL_%'
);
DROP TABLE IF EXISTS `ActualsEntries`;
DROP TABLE IF EXISTS `ClosingPeriods`;
DROP TABLE IF EXISTS `PlannedAllocations`;
DROP TABLE IF EXISTS `EngagementPapds`;
DROP TABLE IF EXISTS `EngagementRankBudgets`;
DROP TABLE IF EXISTS `MarginEvolutions`;
DROP TABLE IF EXISTS `Exceptions`;
DROP TABLE IF EXISTS `FiscalYears`;
DROP TABLE IF EXISTS `Papds`;
DROP TABLE IF EXISTS `Engagements`;
DROP TABLE IF EXISTS `Customers`;
DROP TABLE IF EXISTS `Settings`;

SET FOREIGN_KEY_CHECKS = 1;

CREATE TABLE `Customers`
(
    `Id`           INT           NOT NULL AUTO_INCREMENT,
    `Name`         VARCHAR(200)  NOT NULL,
    `ClientIdText` VARCHAR(64)   NULL,
    CONSTRAINT `PK_Customers` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Customers_Name` UNIQUE (`Name`),
    CONSTRAINT `UX_Customers_ClientIdText` UNIQUE (`ClientIdText`)
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

CREATE TABLE `Papds`
(
    `Id`    INT          NOT NULL AUTO_INCREMENT,
    `Name`  VARCHAR(200) NOT NULL,
    `Level` VARCHAR(50)  NOT NULL,
    CONSTRAINT `PK_Papds` PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `FiscalYears`
(
    `Id`        INT          NOT NULL AUTO_INCREMENT,
    `Name`      VARCHAR(32)  NOT NULL,
    `StartDate` DATE         NOT NULL,
    `EndDate`   DATE         NOT NULL,
    CONSTRAINT `PK_FiscalYears` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_FiscalYears_Name` UNIQUE (`Name`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `ClosingPeriods`
(
    `Id`          INT          NOT NULL AUTO_INCREMENT,
    `Name`        VARCHAR(100) NOT NULL,
    `PeriodStart` DATE         NOT NULL,
    `PeriodEnd`   DATE         NOT NULL,
    CONSTRAINT `PK_ClosingPeriods` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_ClosingPeriods_Name` UNIQUE (`Name`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `EngagementPapds`
(
    `Id`           INT  NOT NULL AUTO_INCREMENT,
    `EngagementId` INT  NOT NULL,
    `PapdId`       INT  NOT NULL,
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
    `Id`            INT           NOT NULL AUTO_INCREMENT,
    `EngagementId`  INT           NOT NULL,
    `PapdId`        INT           NULL,
    `ClosingPeriodId` INT         NOT NULL,
    `Date`          DATE          NOT NULL,
    `Hours`         DOUBLE        NOT NULL,
    `ImportBatchId` VARCHAR(100)  NOT NULL,
    CONSTRAINT `PK_ActualsEntries` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ActualsEntries_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ActualsEntries_Papds` FOREIGN KEY (`PapdId`) REFERENCES `Papds` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_ActualsEntries_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE RESTRICT
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Exceptions`
(
    `Id`         INT            NOT NULL AUTO_INCREMENT,
    `Timestamp`  DATETIME(6)    NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `SourceFile` VARCHAR(260)   NOT NULL,
    `RowData`    TEXT           NOT NULL,
    `Reason`     VARCHAR(500)   NOT NULL,
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

SET FOREIGN_KEY_CHECKS = 1;
