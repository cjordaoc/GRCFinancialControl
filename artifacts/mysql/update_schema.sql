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

-- ========================================
-- Snapshot-Based Allocations Migration
-- ========================================
-- This migration converts Revenue and Hours Allocations to snapshot-based architecture
-- aligned with Financial Evolution closing period snapshots.
--
-- BEFORE: Single "current" allocation per Engagement/FiscalYear
-- AFTER: Historical snapshots per Engagement/FiscalYear/ClosingPeriod

-- ========================================
-- Part 1: Revenue Allocation Snapshots
-- ========================================

-- Step 1.1: Add ClosingPeriodId to EngagementFiscalYearRevenueAllocation
ALTER TABLE `EngagementFiscalYearRevenueAllocations`
    ADD COLUMN `ClosingPeriodId` INT NULL AFTER `FiscalYearId`,
    ADD INDEX `IX_EngagementFiscalYearRevenueAllocations_ClosingPeriodId` (`ClosingPeriodId`);

-- Step 1.2: Populate ClosingPeriodId from Engagement.LastClosingPeriodId
UPDATE `EngagementFiscalYearRevenueAllocations` ra
    INNER JOIN `Engagements` e ON e.Id = ra.EngagementId
SET ra.ClosingPeriodId = e.LastClosingPeriodId
WHERE ra.ClosingPeriodId IS NULL
  AND e.LastClosingPeriodId IS NOT NULL;

-- Step 1.3: Delete orphaned records with no closing period
DELETE FROM `EngagementFiscalYearRevenueAllocations`
WHERE `ClosingPeriodId` IS NULL;

-- Step 1.4: Make ClosingPeriodId non-nullable and add foreign key
ALTER TABLE `EngagementFiscalYearRevenueAllocations`
    MODIFY COLUMN `ClosingPeriodId` INT NOT NULL,
    ADD CONSTRAINT `FK_EngagementFiscalYearRevenueAllocations_ClosingPeriods`
        FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE CASCADE;

-- Step 1.5: Drop old unique constraint and add new one with ClosingPeriodId
ALTER TABLE `EngagementFiscalYearRevenueAllocations`
    DROP INDEX IF EXISTS `IX_EngagementFiscalYearRevenueAllocations_EngagementId_FiscalYearId`;

ALTER TABLE `EngagementFiscalYearRevenueAllocations`
    ADD CONSTRAINT `UX_EngagementFiscalYearRevenueAllocations_Snapshot`
        UNIQUE (`EngagementId`, `FiscalYearId`, `ClosingPeriodId`);

-- Step 1.6: Add index for efficiently finding latest snapshots
CREATE INDEX `IX_EngagementFiscalYearRevenueAllocations_Latest`
    ON `EngagementFiscalYearRevenueAllocations` (`EngagementId`, `FiscalYearId`, `ClosingPeriodId` DESC);

-- ========================================
-- Part 2: Hours Allocation Snapshots
-- ========================================

-- Step 2.1: Add ClosingPeriodId to EngagementRankBudgets
ALTER TABLE `EngagementRankBudgets`
    ADD COLUMN `ClosingPeriodId` INT NULL AFTER `FiscalYearId`,
    ADD INDEX `IX_EngagementRankBudgets_ClosingPeriodId` (`ClosingPeriodId`);

-- Step 2.2: Populate ClosingPeriodId from Engagement.LastClosingPeriodId
UPDATE `EngagementRankBudgets` rb
    INNER JOIN `Engagements` e ON e.Id = rb.EngagementId
SET rb.ClosingPeriodId = e.LastClosingPeriodId
WHERE rb.ClosingPeriodId IS NULL
  AND e.LastClosingPeriodId IS NOT NULL;

-- Step 2.3: Delete orphaned records with no closing period
DELETE FROM `EngagementRankBudgets`
WHERE `ClosingPeriodId` IS NULL;

-- Step 2.4: Make ClosingPeriodId non-nullable and add foreign key
ALTER TABLE `EngagementRankBudgets`
    MODIFY COLUMN `ClosingPeriodId` INT NOT NULL,
    ADD CONSTRAINT `FK_EngagementRankBudgets_ClosingPeriods`
        FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE CASCADE;

-- Step 2.5: Drop old unique constraint and add new one with ClosingPeriodId
ALTER TABLE `EngagementRankBudgets`
    DROP INDEX IF EXISTS `IX_EngagementRankBudgets_EngagementId_FiscalYearId_RankName`;

ALTER TABLE `EngagementRankBudgets`
    ADD CONSTRAINT `UX_EngagementRankBudgets_Snapshot`
        UNIQUE (`EngagementId`, `FiscalYearId`, `RankName`, `ClosingPeriodId`);

-- Step 2.6: Add index for efficiently finding latest snapshots
CREATE INDEX `IX_EngagementRankBudgets_Latest`
    ON `EngagementRankBudgets` (`EngagementId`, `FiscalYearId`, `RankName`, `ClosingPeriodId` DESC);
