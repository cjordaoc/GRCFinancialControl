-- Add missing columns to FinancialEvolution table
-- This is a minimal migration that adds only the new columns without dropping existing ones

-- Check if we need to drop old columns first (comment out if they don't exist)
-- ALTER TABLE `FinancialEvolution` DROP COLUMN IF EXISTS `HoursData`;
-- ALTER TABLE `FinancialEvolution` DROP COLUMN IF EXISTS `MarginData`;
-- ALTER TABLE `FinancialEvolution` DROP COLUMN IF EXISTS `ExpenseData`;

-- Add new granular columns
ALTER TABLE `FinancialEvolution`
    ADD COLUMN IF NOT EXISTS `BudgetHours` DECIMAL(18, 2) NULL AFTER `EngagementId`,
    ADD COLUMN IF NOT EXISTS `ChargedHours` DECIMAL(18, 2) NULL AFTER `BudgetHours`,
    ADD COLUMN IF NOT EXISTS `FYTDHours` DECIMAL(18, 2) NULL AFTER `ChargedHours`,
    ADD COLUMN IF NOT EXISTS `AdditionalHours` DECIMAL(18, 2) NULL AFTER `FYTDHours`;

-- Add FiscalYearId and Revenue Metrics
ALTER TABLE `FinancialEvolution`
    ADD COLUMN IF NOT EXISTS `FiscalYearId` INT NULL AFTER `AdditionalHours`,
    ADD COLUMN IF NOT EXISTS `RevenueToGoValue` DECIMAL(18, 2) NULL AFTER `FiscalYearId`,
    ADD COLUMN IF NOT EXISTS `RevenueToDateValue` DECIMAL(18, 2) NULL AFTER `RevenueToGoValue`;

-- Add Margin Metrics
ALTER TABLE `FinancialEvolution`
    ADD COLUMN IF NOT EXISTS `BudgetMargin` DECIMAL(18, 2) NULL AFTER `RevenueToDateValue`,
    ADD COLUMN IF NOT EXISTS `ToDateMargin` DECIMAL(18, 2) NULL AFTER `BudgetMargin`,
    ADD COLUMN IF NOT EXISTS `FYTDMargin` DECIMAL(18, 2) NULL AFTER `ToDateMargin`;

-- Add Expense Metrics
ALTER TABLE `FinancialEvolution`
    ADD COLUMN IF NOT EXISTS `ExpenseBudget` DECIMAL(18, 2) NULL AFTER `FYTDMargin`,
    ADD COLUMN IF NOT EXISTS `ExpensesToDate` DECIMAL(18, 2) NULL AFTER `ExpenseBudget`,
    ADD COLUMN IF NOT EXISTS `FYTDExpenses` DECIMAL(18, 2) NULL AFTER `ExpensesToDate`;

-- Add foreign key constraint for FiscalYearId (only if it doesn't exist)
-- Note: This will fail silently if the constraint already exists
SET @constraint_exists = (
    SELECT COUNT(*)
    FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
    AND CONSTRAINT_NAME = 'FK_FinancialEvolution_FiscalYears'
    AND TABLE_NAME = 'FinancialEvolution'
);

SET @sql = IF(@constraint_exists = 0,
    'ALTER TABLE `FinancialEvolution` ADD CONSTRAINT `FK_FinancialEvolution_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE SET NULL',
    'SELECT ''Constraint already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
