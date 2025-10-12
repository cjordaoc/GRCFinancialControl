-- MySQL schema reset script generated from the domain POCOs.
-- Execute USE <database>; before running this script or replace the placeholder below.

SET FOREIGN_KEY_CHECKS = 0;

DROP TABLE IF EXISTS `ActualsEntries`;
DROP TABLE IF EXISTS `ClosingPeriods`;
DROP TABLE IF EXISTS `PlannedAllocations`;
DROP TABLE IF EXISTS `EngagementPapds`;
DROP TABLE IF EXISTS `Exceptions`;
DROP TABLE IF EXISTS `FiscalYears`;
DROP TABLE IF EXISTS `Papds`;
DROP TABLE IF EXISTS `Engagements`;
DROP TABLE IF EXISTS `Settings`;

SET FOREIGN_KEY_CHECKS = 1;

CREATE TABLE `Engagements`
(
    `Id`                INT             NOT NULL AUTO_INCREMENT,
    `EngagementId`      VARCHAR(64)     NOT NULL,
    `Description`       VARCHAR(255)    NOT NULL,
    `CustomerKey`       VARCHAR(64)     NOT NULL,
    `OpeningMargin`     DECIMAL(18, 2)  NOT NULL,
    `OpeningValue`      DECIMAL(18, 2)  NOT NULL,
    `Status`            VARCHAR(32)     NOT NULL,
    `TotalPlannedHours` DOUBLE          NOT NULL,
    CONSTRAINT `PK_Engagements` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Engagements_EngagementId` UNIQUE (`EngagementId`)
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

CREATE TABLE `PlannedAllocations`
(
    `Id`            INT     NOT NULL AUTO_INCREMENT,
    `EngagementId`  INT     NOT NULL,
    `FiscalYearId`  INT     NOT NULL,
    `AllocatedHours` DOUBLE NOT NULL,
    CONSTRAINT `PK_PlannedAllocations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PlannedAllocations_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_PlannedAllocations_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_PlannedAllocations_EngagementYear` UNIQUE (`EngagementId`, `FiscalYearId`)
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
