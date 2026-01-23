-- Migration: AddGuiCodeToManagerAndPapd
-- Date: 2026-01-21
-- Description: Adds GuiCode column to Managers and Papds tables for unique numeric identifiers from spreadsheet

-- Add GuiCode to Managers table
ALTER TABLE `Managers` 
ADD COLUMN `GuiCode` varchar(50) NULL;

-- Create unique index on Managers.GuiCode
CREATE UNIQUE INDEX `IX_Managers_GuiCode` ON `Managers` (`GuiCode`);

-- Add GuiCode to Papds table
ALTER TABLE `Papds` 
ADD COLUMN `GuiCode` varchar(50) NULL;

-- Create unique index on Papds.GuiCode
CREATE UNIQUE INDEX `IX_Papds_GuiCode` ON `Papds` (`GuiCode`);
