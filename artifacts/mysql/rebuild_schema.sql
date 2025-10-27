-- rebuild_schema.sql (refactored with TIMESTAMP(6) for audit fields)
-- Drops all base tables and recreates schema with timestamp-based audit columns

SET SESSION group_concat_max_len = 1024 * 1024;
SELECT COALESCE(
         CONCAT('DROP TABLE IF EXISTS ', GROUP_CONCAT(CONCAT('`', TABLE_SCHEMA, '`.`', TABLE_NAME, '`')
                        ORDER BY TABLE_NAME SEPARATOR ', ')),
         'SELECT 1'
       )
INTO @drop_sql
FROM information_schema.tables
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE';

SET FOREIGN_KEY_CHECKS = 0;
PREPARE stmt FROM @drop_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ============================
-- Core reference tables
-- ============================
CREATE TABLE `Customers`
(
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Name` VARCHAR(200) NOT NULL,
    `CustomerCode` VARCHAR(20) NOT NULL,
    CONSTRAINT `PK_Customers` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Customers_CustomerCode` UNIQUE (`CustomerCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `FiscalYears`
(
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Name` VARCHAR(100) NOT NULL,
    `StartDate` DATETIME(6) NOT NULL,
    `EndDate` DATETIME(6) NOT NULL,
    `AreaSalesTarget` DECIMAL(18,2) NOT NULL DEFAULT 0,
    `AreaRevenueTarget` DECIMAL(18,2) NOT NULL DEFAULT 0,
    `IsLocked` TINYINT(1) NOT NULL DEFAULT 0,
    `LockedAt` DATETIME(6) NULL,
    `LockedBy` VARCHAR(100) NULL,
    CONSTRAINT `PK_FiscalYears` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_FiscalYears_Name` UNIQUE (`Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `ClosingPeriods`
(
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Name` VARCHAR(100) NOT NULL,
    `FiscalYearId` INT NOT NULL,
    `PeriodStart` DATETIME(6) NOT NULL,
    `PeriodEnd` DATETIME(6) NOT NULL,
    CONSTRAINT `PK_ClosingPeriods` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_ClosingPeriods_Name` UNIQUE (`Name`),
    CONSTRAINT `FK_ClosingPeriods_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears`(`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================
-- Invoice planner tables
-- ============================
CREATE TABLE `InvoicePlan`
(
    `Id` INT NOT NULL AUTO_INCREMENT,
    `EngagementId` VARCHAR(64) NOT NULL,
    `Type` VARCHAR(16) NOT NULL,
    `NumInvoices` INT NOT NULL DEFAULT 0,
    `PaymentTermDays` INT NOT NULL DEFAULT 0,
    `CustomerFocalPointName` VARCHAR(120) NOT NULL,
    `CustomerFocalPointEmail` VARCHAR(200) NOT NULL,
    `CustomInstructions` TEXT NULL,
    `FirstEmissionDate` DATE NULL,
    `CreatedAt` TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_InvoicePlan` PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_InvoicePlan_Engagement`
    ON `InvoicePlan` (`EngagementId`);

CREATE TABLE `InvoicePlanEmail`
(
    `Id` INT NOT NULL AUTO_INCREMENT,
    `PlanId` INT NOT NULL,
    `Email` VARCHAR(200) NOT NULL,
    `CreatedAt` TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_InvoicePlanEmail` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InvoicePlanEmail_Plan` FOREIGN KEY (`PlanId`) REFERENCES `InvoicePlan`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_InvoicePlanEmail_Plan`
    ON `InvoicePlanEmail` (`PlanId`);

CREATE TABLE `InvoiceItem`
(
    `Id` INT NOT NULL AUTO_INCREMENT,
    `PlanId` INT NOT NULL,
    `SeqNo` INT NOT NULL,
    `Percentage` DECIMAL(9,4) NOT NULL DEFAULT 0,
    `Amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
    `EmissionDate` DATE NULL,
    `DueDate` DATE NULL,
    `PayerCnpj` VARCHAR(20) NOT NULL,
    `PoNumber` VARCHAR(64) NULL,
    `FrsNumber` VARCHAR(64) NULL,
    `CustomerTicket` VARCHAR(64) NULL,
    `AdditionalInfo` TEXT NULL,
    `DeliveryDescription` VARCHAR(255) NULL,
    `Status` VARCHAR(16) NOT NULL,
    `RitmNumber` VARCHAR(64) NULL,
    `CoeResponsible` VARCHAR(120) NULL,
    `RequestDate` DATE NULL,
    `CreatedAt` TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_InvoiceItem` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InvoiceItem_Plan` FOREIGN KEY (`PlanId`) REFERENCES `InvoicePlan`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE UNIQUE INDEX `UQ_InvoiceItem_PlanSeq`
    ON `InvoiceItem` (`PlanId`, `SeqNo`);

CREATE INDEX `IX_InvoiceItem_EmissionDate`
    ON `InvoiceItem` (`EmissionDate`);

CREATE INDEX `IX_InvoiceItem_Status`
    ON `InvoiceItem` (`Status`);

CREATE TABLE `InvoiceEmission`
(
    `Id` INT NOT NULL AUTO_INCREMENT,
    `InvoiceItemId` INT NOT NULL,
    `BzCode` VARCHAR(64) NOT NULL,
    `EmittedAt` DATE NOT NULL,
    `CreatedAt` TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    `CanceledAt` DATE NULL,
    `CancelReason` VARCHAR(255) NULL,
    CONSTRAINT `PK_InvoiceEmission` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InvoiceEmission_Item` FOREIGN KEY (`InvoiceItemId`) REFERENCES `InvoiceItem`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_InvoiceEmission_Item`
    ON `InvoiceEmission` (`InvoiceItemId`);

CREATE TABLE `MailOutbox`
(
    `Id` INT NOT NULL AUTO_INCREMENT,
    `NotificationDate` DATE NOT NULL,
    `InvoiceItemId` INT NOT NULL,
    `ToName` VARCHAR(120) NOT NULL,
    `ToEmail` VARCHAR(200) NOT NULL,
    `CcCsv` TEXT NULL,
    `Subject` VARCHAR(255) NOT NULL,
    `BodyText` TEXT NOT NULL,
    `SendToken` VARCHAR(36) NULL,
    `CreatedAt` TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `SentAt` TIMESTAMP(6) NULL,
    CONSTRAINT `PK_MailOutbox` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_MailOutbox_Item` FOREIGN KEY (`InvoiceItemId`) REFERENCES `InvoiceItem`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_MailOutbox_Notification`
    ON `MailOutbox` (`NotificationDate`);

CREATE INDEX `IX_MailOutbox_Pending`
    ON `MailOutbox` (`NotificationDate`, `SentAt`, `SendToken`);

CREATE TABLE `MailOutboxLog`
(
    `Id` INT NOT NULL AUTO_INCREMENT,
    `OutboxId` INT NOT NULL,
    `AttemptAt` TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `Success` TINYINT(1) NOT NULL DEFAULT 0,
    `ErrorMessage` VARCHAR(500) NULL,
    CONSTRAINT `PK_MailOutboxLog` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_MailOutboxLog_Outbox` FOREIGN KEY (`OutboxId`) REFERENCES `MailOutbox`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_MailOutboxLog_Outbox`
    ON `MailOutboxLog` (`OutboxId`);

SET FOREIGN_KEY_CHECKS = 1;
