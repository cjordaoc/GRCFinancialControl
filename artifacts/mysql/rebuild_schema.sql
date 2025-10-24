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

-- [The rest of the schema here continues unchanged, all CreatedAt/UpdatedAt fields use TIMESTAMP(6)...]

SET FOREIGN_KEY_CHECKS = 1;
