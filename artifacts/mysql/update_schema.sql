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

-- FinancialEvolution: Restructure to granular metrics
-- BREAKING CHANGE: Drops old single-column metrics, adds Budget/ETD/FYTD breakdowns

-- Step 1: Drop old columns (HoursData, MarginData, ExpenseData)
ALTER TABLE `FinancialEvolution`
    DROP COLUMN `HoursData`,
    DROP COLUMN `MarginData`,
    DROP COLUMN `ExpenseData`;

-- Step 2: Add new granular columns
-- Hours Metrics
ALTER TABLE `FinancialEvolution`
    ADD COLUMN `BudgetHours` DECIMAL(18, 2) NULL AFTER `EngagementId`,
    ADD COLUMN `ChargedHours` DECIMAL(18, 2) NULL AFTER `BudgetHours`,
    ADD COLUMN `FYTDHours` DECIMAL(18, 2) NULL AFTER `ChargedHours`,
    ADD COLUMN `AdditionalHours` DECIMAL(18, 2) NULL AFTER `FYTDHours`;

-- Revenue Metrics (ValueData already exists, add others)
ALTER TABLE `FinancialEvolution`
    ADD COLUMN `FiscalYearId` INT NULL AFTER `AdditionalHours`,
    ADD COLUMN `RevenueToGoValue` DECIMAL(18, 2) NULL AFTER `FiscalYearId`,
    ADD COLUMN `RevenueToDateValue` DECIMAL(18, 2) NULL AFTER `RevenueToGoValue`;

-- Margin Metrics
ALTER TABLE `FinancialEvolution`
    ADD COLUMN `BudgetMargin` DECIMAL(18, 2) NULL AFTER `RevenueToDateValue`,
    ADD COLUMN `ToDateMargin` DECIMAL(18, 2) NULL AFTER `BudgetMargin`,
    ADD COLUMN `FYTDMargin` DECIMAL(18, 2) NULL AFTER `ToDateMargin`;

-- Expense Metrics
ALTER TABLE `FinancialEvolution`
    ADD COLUMN `ExpenseBudget` DECIMAL(18, 2) NULL AFTER `FYTDMargin`,
    ADD COLUMN `ExpensesToDate` DECIMAL(18, 2) NULL AFTER `ExpenseBudget`,
    ADD COLUMN `FYTDExpenses` DECIMAL(18, 2) NULL AFTER `ExpensesToDate`;

-- Step 3: Add foreign key constraint for FiscalYearId
ALTER TABLE `FinancialEvolution`
    ADD CONSTRAINT `FK_FinancialEvolution_FiscalYears` 
    FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) 
    ON DELETE SET NULL;
