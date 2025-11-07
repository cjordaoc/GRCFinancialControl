-- Simple migration to add missing columns to FinancialEvolution table
-- Execute this script against your MySQL database

-- Add Hours Metrics columns
ALTER TABLE `FinancialEvolution`
    ADD COLUMN `BudgetHours` DECIMAL(18, 2) NULL AFTER `EngagementId`;

ALTER TABLE `FinancialEvolution`
    ADD COLUMN `ChargedHours` DECIMAL(18, 2) NULL AFTER `BudgetHours`;

ALTER TABLE `FinancialEvolution`
    ADD COLUMN `FYTDHours` DECIMAL(18, 2) NULL AFTER `ChargedHours`;

ALTER TABLE `FinancialEvolution`
    ADD COLUMN `AdditionalHours` DECIMAL(18, 2) NULL AFTER `FYTDHours`;

-- Add FiscalYearId
ALTER TABLE `FinancialEvolution`
    ADD COLUMN `FiscalYearId` INT NULL AFTER `AdditionalHours`;

-- Add Revenue Metrics columns
ALTER TABLE `FinancialEvolution`
    ADD COLUMN `RevenueToGoValue` DECIMAL(18, 2) NULL AFTER `FiscalYearId`;

ALTER TABLE `FinancialEvolution`
    ADD COLUMN `RevenueToDateValue` DECIMAL(18, 2) NULL AFTER `RevenueToGoValue`;

-- Add Margin Metrics columns
ALTER TABLE `FinancialEvolution`
    ADD COLUMN `BudgetMargin` DECIMAL(18, 2) NULL AFTER `RevenueToDateValue`;

ALTER TABLE `FinancialEvolution`
    ADD COLUMN `ToDateMargin` DECIMAL(18, 2) NULL AFTER `BudgetMargin`;

ALTER TABLE `FinancialEvolution`
    ADD COLUMN `FYTDMargin` DECIMAL(18, 2) NULL AFTER `ToDateMargin`;

-- Add Expense Metrics columns
ALTER TABLE `FinancialEvolution`
    ADD COLUMN `ExpenseBudget` DECIMAL(18, 2) NULL AFTER `FYTDMargin`;

ALTER TABLE `FinancialEvolution`
    ADD COLUMN `ExpensesToDate` DECIMAL(18, 2) NULL AFTER `ExpenseBudget`;

ALTER TABLE `FinancialEvolution`
    ADD COLUMN `FYTDExpenses` DECIMAL(18, 2) NULL AFTER `ExpensesToDate`;

-- Add foreign key constraint for FiscalYearId
ALTER TABLE `FinancialEvolution`
    ADD CONSTRAINT `FK_FinancialEvolution_FiscalYears` 
    FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) 
    ON DELETE SET NULL;
