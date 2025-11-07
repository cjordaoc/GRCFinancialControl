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

-- Step 1.5: Add unique constraint for snapshot composite key
ALTER TABLE `EngagementFiscalYearRevenueAllocations`
    ADD CONSTRAINT `UX_EngagementFiscalYearRevenueAllocations_Snapshot`
        UNIQUE (`EngagementId`, `FiscalYearId`, `ClosingPeriodId`);

-- Step 1.6: Add LastUpdateDate for audit trail
ALTER TABLE `EngagementFiscalYearRevenueAllocations`
    ADD COLUMN `LastUpdateDate` DATETIME(6) NULL AFTER `ToDateValue`;

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

-- Step 2.5: Add unique constraint for snapshot composite key
ALTER TABLE `EngagementRankBudgets`
    ADD CONSTRAINT `UX_EngagementRankBudgets_Snapshot`
        UNIQUE (`EngagementId`, `FiscalYearId`, `RankName`, `ClosingPeriodId`);

-- ========================================
-- Part 3: Performance Indexes
-- ========================================

-- Index for finding latest snapshots efficiently
CREATE INDEX `IX_EngagementFiscalYearRevenueAllocations_Latest`
    ON `EngagementFiscalYearRevenueAllocations` (`EngagementId`, `FiscalYearId`, `ClosingPeriodId` DESC);

CREATE INDEX `IX_EngagementRankBudgets_Latest`
    ON `EngagementRankBudgets` (`EngagementId`, `FiscalYearId`, `RankName`, `ClosingPeriodId` DESC);

-- ========================================
-- Part 4: Verification Queries (for testing)
-- ========================================

-- Verify Revenue Allocation snapshots
-- SELECT
--     e.EngagementId,
--     fy.Name AS FiscalYear,
--     cp.Name AS ClosingPeriod,
--     ra.ToGoValue,
--     ra.ToDateValue,
--     ra.UpdatedAt
-- FROM EngagementFiscalYearRevenueAllocations ra
-- INNER JOIN Engagements e ON e.Id = ra.EngagementId
-- INNER JOIN FiscalYears fy ON fy.Id = ra.FiscalYearId
-- INNER JOIN ClosingPeriods cp ON cp.Id = ra.ClosingPeriodId
-- ORDER BY e.EngagementId, fy.StartDate, cp.PeriodEnd;

-- Verify Hours Allocation snapshots
-- SELECT
--     e.EngagementId,
--     fy.Name AS FiscalYear,
--     cp.Name AS ClosingPeriod,
--     rb.RankName,
--     rb.BudgetHours,
--     rb.ConsumedHours,
--     rb.AdditionalHours,
--     rb.UpdatedAtUtc
-- FROM EngagementRankBudgets rb
-- INNER JOIN Engagements e ON e.Id = rb.EngagementId
-- INNER JOIN FiscalYears fy ON fy.Id = rb.FiscalYearId
-- INNER JOIN ClosingPeriods cp ON cp.Id = rb.ClosingPeriodId
-- ORDER BY e.EngagementId, fy.StartDate, cp.PeriodEnd, rb.RankName;
