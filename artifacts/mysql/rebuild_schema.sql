-- Drop ALL base tables in the current database in one statement

-- 1) Allow a big enough GROUP_CONCAT so the list of tables isn't cut off
SET SESSION group_concat_max_len = 1024 * 1024;  -- 1 MB

-- 2) Build the DROP statement safely (schema-qualified + backticks)
SELECT COALESCE(
         CONCAT(
           'DROP TABLE IF EXISTS ',
           GROUP_CONCAT(CONCAT('`', TABLE_SCHEMA, '`.`', TABLE_NAME, '`')
                        ORDER BY TABLE_NAME SEPARATOR ', ')
         ),
         'SELECT 1'
       )
INTO @drop_sql
FROM information_schema.tables
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_TYPE = 'BASE TABLE';

-- 3) (Optional) Disable FK checks to avoid dependency issues
SET FOREIGN_KEY_CHECKS = 0;

-- 4) Run it
PREPARE stmt FROM @drop_sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;



/* ============================
   Core reference tables
   ============================ */

CREATE TABLE `Customers`
(
    `Id`           INT           NOT NULL AUTO_INCREMENT,
    `Name`         VARCHAR(200)  NOT NULL,
    `CustomerCode` VARCHAR(20)   NOT NULL,
    CONSTRAINT `PK_Customers` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Customers_CustomerCode` UNIQUE (`CustomerCode`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `FiscalYears`
(
    `Id`                INT             NOT NULL AUTO_INCREMENT,
    `Name`              VARCHAR(100)    NOT NULL,
    `StartDate`         DATETIME(6)     NOT NULL,
    `EndDate`           DATETIME(6)     NOT NULL,
    `AreaSalesTarget`   DECIMAL(18, 2)  NOT NULL DEFAULT 0,
    `AreaRevenueTarget` DECIMAL(18, 2)  NOT NULL DEFAULT 0,
    `IsLocked`          TINYINT(1)      NOT NULL DEFAULT 0,
    `LockedAt`          DATETIME(6)     NULL,
    `LockedBy`          VARCHAR(100)    NULL,
    CONSTRAINT `PK_FiscalYears` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_FiscalYears_Name` UNIQUE (`Name`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `ClosingPeriods`
(
    `Id`           INT           NOT NULL AUTO_INCREMENT,
    `Name`         VARCHAR(100)  NOT NULL,
    `FiscalYearId` INT           NOT NULL,
    `PeriodStart`  DATETIME(6)   NOT NULL,
    `PeriodEnd`    DATETIME(6)   NOT NULL,
    CONSTRAINT `PK_ClosingPeriods` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_ClosingPeriods_Name` UNIQUE (`Name`),
    CONSTRAINT `FK_ClosingPeriods_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE RESTRICT
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Papds`
(
    `Id`    INT          NOT NULL AUTO_INCREMENT,
    `Name`  VARCHAR(200) NOT NULL,
    `Level` VARCHAR(100) NOT NULL,
    `WindowsLogin` VARCHAR(200) NULL,
    CONSTRAINT `PK_Papds` PRIMARY KEY (`Id`),
    CONSTRAINT `UQ_Papds_WindowsLogin` UNIQUE (`WindowsLogin`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Managers`
(
    `Id`       INT           NOT NULL AUTO_INCREMENT,
    `Name`     VARCHAR(200)  NOT NULL,
    `Email`    VARCHAR(254)  NOT NULL,
    `Position` VARCHAR(50)   NOT NULL,
    `WindowsLogin` VARCHAR(200) NULL,
    CONSTRAINT `PK_Managers` PRIMARY KEY (`Id`),
    CONSTRAINT `UQ_Managers_Email` UNIQUE (`Email`),
    CONSTRAINT `UQ_Managers_WindowsLogin` UNIQUE (`WindowsLogin`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `RankMapping`
(
    `Id`              INT             NOT NULL AUTO_INCREMENT,
    `RawRank`         VARCHAR(100)    NOT NULL,
    `NormalizedRank`  VARCHAR(100)    NOT NULL,
    `IsActive`        TINYINT(1)      NOT NULL DEFAULT 1,
    `LastSeenAt`      DATETIME(6)     NULL,
    CONSTRAINT `PK_RankMapping` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_RankMapping_RawRank` UNIQUE (`RawRank`),
    INDEX `IX_RankMapping_NormalizedRank` (`NormalizedRank`),
    INDEX `IX_RankMapping_IsActive` (`IsActive`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;


CREATE TABLE `Employees`
(
    `Gpn`           VARCHAR(20)    NOT NULL,
    `EmployeeName`  VARCHAR(200)   NOT NULL,
    `IsEyEmployee`  TINYINT(1)     NOT NULL DEFAULT 1,
    `IsContractor`  TINYINT(1)     NOT NULL DEFAULT 0,
    `Office`        VARCHAR(100)   NULL,
    `CostCenter`    VARCHAR(50)    NULL,
    `StartDate`     DATE           NULL,
    `EndDate`       DATE           NULL,
    CONSTRAINT `PK_Employees` PRIMARY KEY (`Gpn`),
    INDEX `IX_Employees_Office` (`Office`),
    INDEX `IX_Employees_CostCenter` (`CostCenter`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;


/* ============================
   Engagements & assignments
   ============================ */

CREATE TABLE `Engagements`
(
    `Id`                   INT            NOT NULL AUTO_INCREMENT,
    `EngagementId`         VARCHAR(64)    NOT NULL,
    `Description`          VARCHAR(255)   NOT NULL,
    `Currency`             VARCHAR(16)    NOT NULL DEFAULT '',
    `MarginPctBudget`      DECIMAL(9, 4)  NULL,
    `MarginPctEtcp`        DECIMAL(9, 4)  NULL,
    `LastEtcDate`          DATETIME(6)    NULL,
    `ProposedNextEtcDate`  DATETIME(6)    NULL,
    `StatusText`           VARCHAR(100)   NULL,
    `CustomerId`           INT            NULL,
    `OpeningValue`         DECIMAL(18, 2) NOT NULL,
    `OpeningExpenses`      DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `Status`               INT            NOT NULL,
    `Source`               VARCHAR(20)    NOT NULL DEFAULT 'GrcProject',
    `InitialHoursBudget`   DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `EtcpHours`            DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `ValueEtcp`            DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `ExpensesEtcp`         DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `LastClosingPeriodId`  INT            NULL,
    CONSTRAINT `PK_Engagements` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_Engagements_EngagementId` UNIQUE (`EngagementId`),
    CONSTRAINT `FK_Engagements_Customers` FOREIGN KEY (`CustomerId`) REFERENCES `Customers` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_Engagements_LastCP` FOREIGN KEY (`LastClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `EngagementPapds`
(
    `Id`           INT         NOT NULL AUTO_INCREMENT,
    `EngagementId` INT         NOT NULL,
    `PapdId`       INT         NOT NULL,
    `EffectiveDate` DATETIME(6) NOT NULL,
    CONSTRAINT `PK_EngagementPapds` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementPapds_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementPapds_Papds` FOREIGN KEY (`PapdId`) REFERENCES `Papds` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementPapds_Assignment` UNIQUE (`EngagementId`, `PapdId`, `EffectiveDate`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `EngagementManagerAssignments`
(
    `Id`           INT         NOT NULL AUTO_INCREMENT,
    `EngagementId` INT         NOT NULL,
    `ManagerId`    INT         NOT NULL,
    `BeginDate`    DATETIME(6) NOT NULL,
    `EndDate`      DATETIME(6) NULL,
    CONSTRAINT `PK_EngagementManagerAssignments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementManagerAssignments_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementManagerAssignments_Managers` FOREIGN KEY (`ManagerId`) REFERENCES `Managers` (`Id`) ON DELETE CASCADE,
    INDEX `IX_EngagementManagerAssignments_Engagement_Manager_Begin` (`EngagementId`, `ManagerId`, `BeginDate`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;


/* ============================
   Budgets & financial evolution
   ============================ */

CREATE TABLE `EngagementRankBudgets`
(
    `Id`            BIGINT         NOT NULL AUTO_INCREMENT,
    `EngagementId`  INT            NOT NULL,
    `RankName`      VARCHAR(100)   NOT NULL,
    `Hours`         DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `ForecastHours` DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `CreatedAtUtc`  DATETIME(6)    NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAtUtc`  DATETIME(6)    NULL,
    CONSTRAINT `PK_EngagementRankBudgets` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementRankBudgets_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementRankBudgets_EngagementRank` UNIQUE (`EngagementId`, `RankName`),
    INDEX `IX_EngagementRankBudgets_EngagementId` (`EngagementId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `StaffAllocationForecasts`
(
    `Id`            BIGINT         NOT NULL AUTO_INCREMENT,
    `EngagementId`  INT            NOT NULL,
    `FiscalYearId`  INT            NOT NULL,
    `RankName`      VARCHAR(100)   NOT NULL,
    `ForecastHours` DECIMAL(18, 2) NOT NULL DEFAULT 0,
    `CreatedAtUtc`  DATETIME(6)    NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAtUtc`  DATETIME(6)    NULL,
    CONSTRAINT `PK_StaffAllocationForecasts` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_StaffAllocationForecasts_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_StaffAllocationForecasts_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_StaffAllocationForecasts_Key` UNIQUE (`EngagementId`, `FiscalYearId`, `RankName`),
    INDEX `IX_StaffAllocationForecasts_FiscalYearId` (`FiscalYearId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Applied Suggestion #2: Use INT FK for EngagementId to keep consistency/performance
CREATE TABLE `FinancialEvolution`
(
    `Id`              INT            NOT NULL AUTO_INCREMENT,
    `ClosingPeriodId` VARCHAR(100)   NOT NULL,
    `EngagementId`    INT            NOT NULL,
    `HoursData`       DECIMAL(18, 2) NULL,
    `ValueData`       DECIMAL(18, 2) NULL,
    `MarginData`      DECIMAL(9, 4)  NULL,
    `ExpenseData`     DECIMAL(18, 2) NULL,
    CONSTRAINT `PK_FinancialEvolution` PRIMARY KEY (`Id`),
    CONSTRAINT `UX_FinancialEvolution_Key` UNIQUE (`EngagementId`, `ClosingPeriodId`),
    CONSTRAINT `FK_FinancialEvolution_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE
    -- NOTE: Keeping ClosingPeriodId as VARCHAR(100) to preserve your current external key format.
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;


/* ============================
   Allocations (hours & revenue)
   ============================ */

CREATE TABLE `PlannedAllocations`
(
    `Id`              INT            NOT NULL AUTO_INCREMENT,
    `EngagementId`    INT            NOT NULL,
    `ClosingPeriodId` INT            NOT NULL,
    `AllocatedHours`  DECIMAL(18, 2) NOT NULL,
    CONSTRAINT `PK_PlannedAllocations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PlannedAllocations_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_PlannedAllocations_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_PlannedAllocations_EngagementPeriod` UNIQUE (`EngagementId`, `ClosingPeriodId`),
    INDEX `IX_PlannedAllocations_ClosingPeriodId` (`ClosingPeriodId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `ActualsEntries`
(
    `Id`              INT            NOT NULL AUTO_INCREMENT,
    `EngagementId`    INT            NOT NULL,
    `PapdId`          INT            NULL,
    `ClosingPeriodId` INT            NOT NULL,
    `Date`            DATETIME(6)    NOT NULL,
    `Hours`           DECIMAL(18, 2) NOT NULL,
    `ImportBatchId`   VARCHAR(100)   NOT NULL,
    CONSTRAINT `PK_ActualsEntries` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ActualsEntries_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ActualsEntries_Papds` FOREIGN KEY (`PapdId`) REFERENCES `Papds` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_ActualsEntries_ClosingPeriods` FOREIGN KEY (`ClosingPeriodId`) REFERENCES `ClosingPeriods` (`Id`) ON DELETE RESTRICT,
    INDEX `IX_ActualsEntries_Engagement_Date` (`EngagementId`, `Date`),
    INDEX `IX_ActualsEntries_ClosingPeriodId` (`ClosingPeriodId`),
    INDEX `IX_ActualsEntries_PapdId` (`PapdId`),
    INDEX `IX_ActualsEntries_ImportBatch` (`ImportBatchId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `Exceptions`
(
    `Id`         INT           NOT NULL AUTO_INCREMENT,
    `Timestamp`  DATETIME(6)   NOT NULL, -- populated via trigger
    `SourceFile` VARCHAR(260)  NOT NULL,
    `RowData`    TEXT          NOT NULL,
    `Reason`     VARCHAR(500)  NOT NULL,
    CONSTRAINT `PK_Exceptions` PRIMARY KEY (`Id`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE `EngagementFiscalYearAllocations`
(
    `Id`            INT             NOT NULL AUTO_INCREMENT,
    `EngagementId`  INT             NOT NULL,
    `FiscalYearId`  INT             NOT NULL,
    `PlannedHours`  DECIMAL(18, 2)  NOT NULL,
    CONSTRAINT `PK_EngagementFiscalYearAllocations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EngagementFiscalYearAllocations_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EngagementFiscalYearAllocations_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UX_EngagementFiscalYearAllocations_Allocation` UNIQUE (`EngagementId`, `FiscalYearId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- Applied Suggestion #3: rename PlannedValue -> ToGoValue and add ToDateValue
CREATE TABLE `EngagementFiscalYearRevenueAllocations`
(
    `Id`            INT             NOT NULL AUTO_INCREMENT,
    `EngagementId`  INT             NOT NULL,
    `FiscalYearId`  INT             NOT NULL,
    `ToGoValue`     DECIMAL(18, 2)  NOT NULL,
    `ToDateValue`   DECIMAL(18, 2)  NOT NULL DEFAULT 0,
    `LastUpdateDate` DATE           NULL,
    `CreatedAt`     DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`     DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_EngagementFiscalYearRevenueAllocations` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EFYRA_Engagements` FOREIGN KEY (`EngagementId`) REFERENCES `Engagements` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_EFYRA_FiscalYears` FOREIGN KEY (`FiscalYearId`) REFERENCES `FiscalYears` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `UQ_EFYRA_Allocation` UNIQUE (`EngagementId`, `FiscalYearId`),
    INDEX `IX_EFYRA_FiscalYearId` (`FiscalYearId`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;


/* ===========================================================
   INVOICE PLANNER + NOTIFICATION INFRA
   (unchanged behavior; TIMESTAMP where auto-defaults are desired)
   =========================================================== */

CREATE TABLE `InvoicePlan` (
  `Id`                      INT NOT NULL AUTO_INCREMENT,
  `EngagementId`            VARCHAR(64) NOT NULL,
  `Type`                    VARCHAR(16) NOT NULL,
  `NumInvoices`             INT NOT NULL,
  `PaymentTermDays`         INT NOT NULL,
  `CustomerFocalPointName`  VARCHAR(120) NOT NULL,
  `CustomerFocalPointEmail` VARCHAR(200) NOT NULL,
  `CustomInstructions`      TEXT NULL,
  `FirstEmissionDate`       DATE NULL,
  `CreatedAt`               TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt`               TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  KEY `IX_InvoicePlan_Engagement` (`EngagementId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `InvoicePlanEmail` (
  `Id`        INT NOT NULL AUTO_INCREMENT,
  `PlanId`    INT NOT NULL,
  `Email`     VARCHAR(200) NOT NULL,
  `CreatedAt` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  KEY `IX_InvoicePlanEmail_Plan` (`PlanId`),
  CONSTRAINT `FK_InvoicePlanEmail_Plan` FOREIGN KEY (`PlanId`) REFERENCES `InvoicePlan`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `InvoiceItem` (
  `Id`                  INT NOT NULL AUTO_INCREMENT,
  `PlanId`              INT NOT NULL,
  `SeqNo`               INT NOT NULL,
  `Percentage`          DECIMAL(9,4) NOT NULL DEFAULT 0,
  `Amount`              DECIMAL(18,2) NOT NULL DEFAULT 0,
  `EmissionDate`        DATE NULL,
  `DueDate`             DATE NULL,
  `PayerCnpj`           VARCHAR(20) NOT NULL,
  `PoNumber`            VARCHAR(64) NULL,
  `FrsNumber`           VARCHAR(64) NULL,
  `CustomerTicket`      VARCHAR(64) NULL,
  `AdditionalInfo`      TEXT NULL,
  `DeliveryDescription` VARCHAR(255) NULL,
  `Status`              VARCHAR(16) NOT NULL DEFAULT 'Planned',
  `RitmNumber`          VARCHAR(64) NULL,
  `CoeResponsible`      VARCHAR(120) NULL,
  `RequestDate`         DATE NULL,
  `BzCode`              VARCHAR(64) NULL,
  `EmittedAt`           DATE NULL,
  `CanceledAt`          DATE NULL,
  `CancelReason`        VARCHAR(255) NULL,
  `ReplacementItemId`   INT NULL,
  `CreatedAt`           TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt`           TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  CONSTRAINT `FK_InvoiceItem_Plan` FOREIGN KEY (`PlanId`) REFERENCES `InvoicePlan`(`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_InvoiceItem_Replacement` FOREIGN KEY (`ReplacementItemId`) REFERENCES `InvoiceItem`(`Id`) ON DELETE SET NULL,
  UNIQUE KEY `UQ_InvoiceItem_PlanSeq` (`PlanId`,`SeqNo`),
  KEY `IX_InvoiceItem_EmissionDate` (`EmissionDate`),
  KEY `IX_InvoiceItem_Status` (`Status`),
  KEY `IX_InvoiceItem_Replacement` (`ReplacementItemId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `MailOutbox` (
  `Id`               INT NOT NULL AUTO_INCREMENT,
  `NotificationDate` DATE NOT NULL,
  `InvoiceItemId`    INT NOT NULL,
  `ToName`           VARCHAR(120) NOT NULL,
  `ToEmail`          VARCHAR(200) NOT NULL,
  `CcCsv`            TEXT NULL,
  `Subject`          VARCHAR(255) NOT NULL,
  `BodyText`         TEXT NOT NULL,
  `SendToken`        CHAR(36) NULL,
  `CreatedAt`        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `SentAt`           TIMESTAMP NULL,
  PRIMARY KEY (`Id`),
  CONSTRAINT `FK_MailOutbox_InvoiceItem` FOREIGN KEY (`InvoiceItemId`) REFERENCES `InvoiceItem`(`Id`) ON DELETE CASCADE,
  KEY `IX_MailOutbox_Notification` (`NotificationDate`),
  KEY `IX_MailOutbox_Pending` (`NotificationDate`,`SentAt`,`SendToken`),
  KEY `IX_MailOutbox_InvoiceItem` (`InvoiceItemId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `MailOutboxLog` (
  `Id`           INT NOT NULL AUTO_INCREMENT,
  `OutboxId`     INT NOT NULL,
  `AttemptAt`    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `Success`      TINYINT(1) NOT NULL,
  `ErrorMessage` VARCHAR(500) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_MailOutboxLog_Outbox` (`OutboxId`),
  CONSTRAINT `FK_MailOutboxLog_Outbox` FOREIGN KEY (`OutboxId`) REFERENCES `MailOutbox`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- View
DROP VIEW IF EXISTS `vw_InvoiceNotifyOnDate`;
CREATE VIEW `vw_InvoiceNotifyOnDate` AS
SELECT
  ii.Id AS InvoiceItemId,
  DATE_SUB(DATE_SUB(ii.EmissionDate, INTERVAL 7 DAY),
           INTERVAL WEEKDAY(DATE_SUB(ii.EmissionDate, INTERVAL 7 DAY)) DAY) AS NotifyDate,
  ip.Id AS PlanId,
  ip.EngagementId,
  ip.NumInvoices,
  ip.PaymentTermDays,
  e.Id AS EngagementIntId,
  e.Description AS EngagementDescription,
  c.Name AS CustomerName,
  ii.SeqNo,
  ii.EmissionDate,
  COALESCE(ii.DueDate, DATE_ADD(ii.EmissionDate, INTERVAL ip.PaymentTermDays DAY)) AS ComputedDueDate,
  ii.Amount,
  ip.CustomerFocalPointName,
  ip.CustomerFocalPointEmail,
  GROUP_CONCAT(DISTINCT ipe.Email ORDER BY ipe.Id SEPARATOR ';') AS ExtraEmails,
  GROUP_CONCAT(DISTINCT m.Email ORDER BY m.Id SEPARATOR ';') AS ManagerEmails,
  GROUP_CONCAT(DISTINCT m.Name ORDER BY m.Id SEPARATOR ';') AS ManagerNames,
  ii.PoNumber,
  ii.FrsNumber,
  ii.RitmNumber
FROM InvoiceItem ii
JOIN InvoicePlan ip ON ip.Id = ii.PlanId
LEFT JOIN InvoicePlanEmail ipe ON ipe.PlanId = ip.Id
LEFT JOIN Engagements e ON e.EngagementId = ip.EngagementId
LEFT JOIN Customers c ON c.Id = e.CustomerId
LEFT JOIN EngagementManagerAssignments ema ON ema.EngagementId = e.Id
      AND ema.BeginDate <= CAST(ii.EmissionDate AS DATETIME)
      AND (ema.EndDate IS NULL OR ema.EndDate >= CAST(ii.EmissionDate AS DATETIME))
LEFT JOIN Managers m ON m.Id = ema.ManagerId
WHERE ii.Status IN ('Planned','Requested')
GROUP BY ii.Id, NotifyDate, ip.Id, ip.EngagementId, ip.NumInvoices, ip.PaymentTermDays, e.Id,
         e.Description, c.Name, ii.SeqNo, ii.EmissionDate,
         ComputedDueDate, ii.Amount, ip.CustomerFocalPointName, ip.CustomerFocalPointEmail,
         ii.PoNumber, ii.FrsNumber, ii.RitmNumber;

-- Stored Procedure
DROP PROCEDURE IF EXISTS `sp_FillMailOutboxForDate`;
DELIMITER //
CREATE PROCEDURE `sp_FillMailOutboxForDate`(IN pTargetDate DATE)
BEGIN
  INSERT INTO MailOutbox
    (NotificationDate, InvoiceItemId, ToName, ToEmail, CcCsv, Subject, BodyText)
  SELECT
    v.NotifyDate,
    v.InvoiceItemId,
    v.CustomerFocalPointName,
    v.CustomerFocalPointEmail,
    NULLIF(TRIM(BOTH ';' FROM CONCAT(IFNULL(v.ManagerEmails,''),
      CASE WHEN v.ManagerEmails IS NOT NULL AND v.ManagerEmails <> '' AND v.ExtraEmails IS NOT NULL AND v.ExtraEmails <> '' THEN ';' ELSE '' END,
      IFNULL(v.ExtraEmails,''))), '') AS CcCsv,
    CONCAT('[D-7] Emissão planejada em ', DATE_FORMAT(v.EmissionDate,'%d/%m/%Y'),
           ' – ', v.EngagementId, ' / Parcela ', v.SeqNo, ' de ', v.NumInvoices) AS Subject,
    CONCAT('Serviço: Serviços - Assistência e Consultoria','\n',
           'Competência: ', DATE_FORMAT(v.EmissionDate,'%b/%Y'),'\n',
           'PO: ', IFNULL(v.PoNumber,''),'\n',
           'FRS: ', IFNULL(v.FrsNumber,''),'\n',
           'Chamado: ', IFNULL(v.RitmNumber,''),'\n',
           'Parcela ', v.SeqNo, ' de ', v.NumInvoices,'\n',
           'Valor da Parcela: R$ ', FORMAT(v.Amount, 2),'\n',
           'Vencimento: ', DATE_FORMAT(v.ComputedDueDate,'%d/%m/%Y'),'\n',
           'Contato ', IFNULL(v.CustomerName,''), ': ', IFNULL(v.CustomerFocalPointName,''),'\n',
           'E-mails para envio: ', TRIM(BOTH ';' FROM CONCAT(IFNULL(v.CustomerFocalPointEmail,''),';',IFNULL(v.ExtraEmails,''))),'\n',
           'Gestores: ', IFNULL(v.ManagerNames,''),'\n',
           'E-mails Gestores: ', IFNULL(v.ManagerEmails,'')) AS BodyText
  FROM vw_InvoiceNotifyOnDate v
  WHERE v.NotifyDate = pTargetDate
    AND NOT EXISTS (SELECT 1 FROM MailOutbox mo
                     WHERE mo.NotificationDate = v.NotifyDate
                       AND mo.InvoiceItemId = v.InvoiceItemId);
END//
DELIMITER ;

-- Scheduled Event (unchanged as requested)
DROP EVENT IF EXISTS `ev_FillMailOutbox_Daily`;
CREATE EVENT `ev_FillMailOutbox_Daily`
ON SCHEDULE EVERY 1 DAY
STARTS (TIMESTAMP(CURRENT_DATE()) + INTERVAL 7 HOUR + INTERVAL 5 MINUTE)
DO
  CALL sp_FillMailOutboxForDate(CURRENT_DATE());


/* ========
   TRIGGERS
   ======== */

-- Populate Exceptions.Timestamp (DATETIME) with CURRENT_TIMESTAMP(6) on insert
DROP TRIGGER IF EXISTS trg_Exceptions_bi;
DELIMITER //
CREATE TRIGGER trg_Exceptions_bi
BEFORE INSERT ON Exceptions
FOR EACH ROW
BEGIN
  IF NEW.Timestamp IS NULL THEN
    SET NEW.Timestamp = CURRENT_TIMESTAMP(6);
  END IF;
END//
DELIMITER ;

-- Maintain EngagementRankBudgets audit DATETIMEs
DROP TRIGGER IF EXISTS trg_EngagementRankBudgets_bi;
DROP TRIGGER IF EXISTS trg_EngagementRankBudgets_bu;
DELIMITER //
CREATE TRIGGER trg_EngagementRankBudgets_bi
BEFORE INSERT ON EngagementRankBudgets
FOR EACH ROW
BEGIN
  IF NEW.CreatedAtUtc IS NULL THEN
    SET NEW.CreatedAtUtc = CURRENT_TIMESTAMP(6);
  END IF;
  SET NEW.UpdatedAtUtc = CURRENT_TIMESTAMP(6);
END//
CREATE TRIGGER trg_EngagementRankBudgets_bu
BEFORE UPDATE ON EngagementRankBudgets
FOR EACH ROW
BEGIN
  SET NEW.UpdatedAtUtc = CURRENT_TIMESTAMP(6);
END//
DELIMITER ;


/* ========
   SEEDS
   ======== */
INSERT INTO `ClosingPeriods` (`Name`, `FiscalYearId`, `PeriodStart`, `PeriodEnd`) VALUES
    ('2025-10', 2, '2025-10-01 00:00:00', '2025-10-31 23:59:59'),
    ('2025-11', 2, '2025-11-01 00:00:00', '2025-11-30 23:59:59'),
    ('2025-12', 2, '2025-12-01 00:00:00', '2025-12-31 23:59:59'),
    ('2026-01', 2, '2026-01-01 00:00:00', '2026-01-31 23:59:59');

INSERT INTO `FiscalYears` (`Name`, `StartDate`, `EndDate`, `AreaSalesTarget`, `AreaRevenueTarget`) VALUES
    ('FY25', '2024-07-01 00:00:00', '2025-06-30 23:59:59', 0, 0),
    ('FY26', '2025-07-01 00:00:00', '2026-06-30 23:59:59', 0, 0),
    ('FY27', '2026-07-01 00:00:00', '2027-06-30 23:59:59', 0, 0);
    
INSERT INTO `Papds` (`Name`,`Level`,`WindowsLogin`) VALUES
    ('Danilo Passos','AssociatePartner','danilo.passos'),
    ('Fernando São Pedro','Director','fernando.sao-pedro'),
    ('Alexandre Jucá de Paiva','AssociatePartner', NULL);

INSERT INTO `Managers` (`Name`,`Email`,`Position`,`WindowsLogin`) VALUES
    ('Caio Jordão Calisto','caio.calisto@br.ey.com','SeniorManager','caio.calisto'),
    ('Gabriel Cortezia','gabriel.cortezia@br.ey.com','SeniorManager',NULL),
    ('Rafael Gimenis','rafael.gimenis@br.ey.com','SeniorManager',NULL),
    ('Salomão Bruno','salomao.bruno@br.ey.com','SeniorManager',NULL),
    ('Mariana Galegale','mariana.galegale@br.ey.com','Manager',NULL),
    ('Thomas Lima','thomas.lima@br.ey.com','Manager',NULL),
    ('Vinicius Almeida','vinicius.almeida@br.ey.com','SeniorManager',NULL);


SET FOREIGN_KEY_CHECKS = 1;
