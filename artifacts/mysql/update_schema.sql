ALTER TABLE `InvoicePlan`
    ADD COLUMN `AdditionalDetails` TEXT NULL AFTER `CustomInstructions`;

CREATE TABLE IF NOT EXISTS `EngagementAdditionalSales`
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

-- Add new columns to FinancialEvolution table
ALTER TABLE `FinancialEvolution`
    ADD COLUMN `FiscalYearId` INT NULL AFTER `ExpenseData`,
    ADD COLUMN `RevenueToGoValue` DECIMAL(18, 2) NULL AFTER `FiscalYearId`,
    ADD COLUMN `RevenueToDateValue` DECIMAL(18, 2) NULL AFTER `RevenueToGoValue`;

-- Add foreign key constraint for FiscalYearId
ALTER TABLE `FinancialEvolution`
    ADD CONSTRAINT `FK_FinancialEvolution_FiscalYears` 
    FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) 
    ON DELETE SET NULL;
