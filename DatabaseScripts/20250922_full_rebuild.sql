-- ===============================================
-- GRC Financial Control – Full Rebuild (Aligned to current config)
-- Phases: [1] Drop  [2] Create Tables (PK only)  [3] Indexes  [4] FKs  → Views
-- Notes:
-- - Canonicalizes schema to CamelCase table names.
-- - Drops legacy snake_case tables and unused legacy margin table(s).
-- - Matches data types/defaults observed in current configuration.
-- - MySQL 5.7 compatible (no window functions in views).
-- ===============================================

-- -----------------------------------------------
-- [1] Disable FK checks & DROP all views/tables
-- -----------------------------------------------
SET @original_foreign_key_checks = @@FOREIGN_KEY_CHECKS;
SET FOREIGN_KEY_CHECKS = 0;

-- Drop views (legacy & canonical names)
DROP VIEW IF EXISTS vw_plan_vs_actual_by_level;
DROP VIEW IF EXISTS vw_latest_etc_per_employee;
DROP VIEW IF EXISTS vw_charges_sum;
DROP VIEW IF EXISTS VwPlanVsActualByLevel;
DROP VIEW IF EXISTS VwLatestEtcPerEmployee;
DROP VIEW IF EXISTS VwChargesSum;

-- Drop legacy snake_case tables if present
DROP TABLE IF EXISTS audit_etc_vs_charges;
DROP TABLE IF EXISTS dim_employee;
DROP TABLE IF EXISTS dim_level;
DROP TABLE IF EXISTS dim_source_system;
DROP TABLE IF EXISTS fact_declared_erp_week;
DROP TABLE IF EXISTS fact_declared_retain_week;
DROP TABLE IF EXISTS fact_engagement_margin;
DROP TABLE IF EXISTS fact_engagement_margin_legacy;
DROP TABLE IF EXISTS fact_etc_snapshot;
DROP TABLE IF EXISTS fact_plan_by_level;
DROP TABLE IF EXISTS fact_timesheet_charge;
DROP TABLE IF EXISTS map_employee_alias;
DROP TABLE IF EXISTS map_employee_code;
DROP TABLE IF EXISTS map_level_alias;

-- Drop canonical CamelCase tables if present (idempotent)
DROP TABLE IF EXISTS AuditEtcVsCharges;
DROP TABLE IF EXISTS FactTimesheetCharges;
DROP TABLE IF EXISTS FactDeclaredRetainWeeks;
DROP TABLE IF EXISTS FactDeclaredErpWeeks;
DROP TABLE IF EXISTS FactEngagementMargins;
DROP TABLE IF EXISTS FactEtcSnapshots;
DROP TABLE IF EXISTS FactPlanByLevels;
DROP TABLE IF EXISTS MapLevelAliases;
DROP TABLE IF EXISTS MapEmployeeAliases;
DROP TABLE IF EXISTS MapEmployeeCodes;
DROP TABLE IF EXISTS DimEmployees;
DROP TABLE IF EXISTS DimLevels;
DROP TABLE IF EXISTS DimEngagements;
DROP TABLE IF EXISTS DimFiscalYears;
DROP TABLE IF EXISTS MeasurementPeriods;
DROP TABLE IF EXISTS DimSourceSystems;

-- -----------------------------------------------
-- [2] CREATE TABLES (PRIMARY KEYS ONLY; no FKs/Indexes)
-- -----------------------------------------------

CREATE TABLE DimSourceSystems (
    SourceSystemId BIGINT NOT NULL AUTO_INCREMENT,
    SystemCode     VARCHAR(50)  NOT NULL,
    SystemName     VARCHAR(100) NOT NULL,
    PRIMARY KEY (SourceSystemId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE MeasurementPeriods (
    PeriodId     BIGINT NOT NULL AUTO_INCREMENT,
    Description  VARCHAR(255) NOT NULL,
    StartDate    DATE NOT NULL,
    EndDate      DATE NOT NULL,
    CreatedUtc   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UpdatedUtc   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT PK_MeasurementPeriods PRIMARY KEY (PeriodId),
    CONSTRAINT CHK_MeasurementPeriod_Dates CHECK (StartDate <= EndDate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seed legacy PeriodId = 0 (requires NO_AUTO_VALUE_ON_ZERO temporarily)
SET @original_sql_mode = @@SESSION.sql_mode;
SET @patched_sql_mode = IF(
    FIND_IN_SET('NO_AUTO_VALUE_ON_ZERO', @original_sql_mode) = 0,
    CONCAT_WS(',', NULLIF(@original_sql_mode, ''), 'NO_AUTO_VALUE_ON_ZERO'),
    @original_sql_mode
);
SET SESSION sql_mode = @patched_sql_mode;

INSERT INTO MeasurementPeriods (PeriodId, Description, StartDate, EndDate, CreatedUtc, UpdatedUtc)
VALUES (0, 'Legacy / Pre-migration', '1900-01-01', '1900-01-01', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    Description = VALUES(Description),
    StartDate   = VALUES(StartDate),
    EndDate     = VALUES(EndDate),
    UpdatedUtc  = VALUES(UpdatedUtc);

SET SESSION sql_mode = @original_sql_mode;

CREATE TABLE DimFiscalYears (
    FiscalYearId BIGINT NOT NULL AUTO_INCREMENT,
    Description  VARCHAR(100) NOT NULL,
    DateFrom     DATE NOT NULL,
    DateTo       DATE NOT NULL,
    IsActive     TINYINT(1) NOT NULL DEFAULT 0,
    CreatedUtc   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UpdatedUtc   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (FiscalYearId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE DimEngagements (
    EngagementId            VARCHAR(64)  NOT NULL,
    EngagementTitle         VARCHAR(255) NOT NULL,
    IsActive                TINYINT(1)   NOT NULL DEFAULT 1,
    EngagementPartner       VARCHAR(255) NULL,
    EngagementManager       VARCHAR(255) NULL,
    OpeningMargin           DECIMAL(6,3) NOT NULL DEFAULT 0.000,
    CurrentMargin           DOUBLE       NOT NULL DEFAULT 0,
    LastMarginUpdateDate    DATETIME(6)  NULL,
    CreatedUtc              DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UpdatedUtc              DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (EngagementId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE DimLevels (
    LevelId     BIGINT NOT NULL AUTO_INCREMENT,
    LevelCode   VARCHAR(64)  NOT NULL,
    LevelName   VARCHAR(128) NOT NULL,
    LevelOrder  SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    CreatedUtc  DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UpdatedUtc  DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (LevelId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE DimEmployees (
    EmployeeId     BIGINT NOT NULL AUTO_INCREMENT,
    EmployeeCode   VARCHAR(64)  NULL,
    FullName       VARCHAR(255) NOT NULL,
    NormalizedName VARCHAR(255) NOT NULL,
    CreatedUtc     DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UpdatedUtc     DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (EmployeeId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE MapEmployeeAliases (
    EmployeeAliasId BIGINT NOT NULL AUTO_INCREMENT,
    SourceSystemId  BIGINT NOT NULL,
    RawName         VARCHAR(255) NOT NULL,
    NormalizedRaw   VARCHAR(255) NOT NULL,
    EmployeeId      BIGINT NOT NULL,
    CreatedUtc      DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (EmployeeAliasId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE MapEmployeeCodes (
    EmployeeCodeId BIGINT NOT NULL AUTO_INCREMENT,
    SourceSystemId BIGINT NOT NULL,
    EmployeeCode   VARCHAR(64) NOT NULL,
    EmployeeId     BIGINT NOT NULL,
    CreatedUtc     DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (EmployeeCodeId),
    UNIQUE KEY UX_MapEmployeeCodes_SourceCode (SourceSystemId, EmployeeCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE MapLevelAliases (
    LevelAliasId   BIGINT NOT NULL AUTO_INCREMENT,
    SourceSystemId BIGINT NOT NULL,
    RawLevel       VARCHAR(128) NOT NULL,
    NormalizedRaw  VARCHAR(128) NOT NULL,
    LevelId        BIGINT NOT NULL,
    CreatedUtc     DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (LevelAliasId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE FactPlanByLevels (
    PlanId              BIGINT NOT NULL AUTO_INCREMENT,
    LoadUtc             DATETIME(6) NOT NULL,
    SourceSystemId      BIGINT NOT NULL,
    MeasurementPeriodId BIGINT NOT NULL,
    EngagementId        VARCHAR(64) NOT NULL,
    LevelId             BIGINT NOT NULL,
    PlannedHours        DECIMAL(12,2) NOT NULL,
    PlannedRate         DECIMAL(14,4) NULL,
    CreatedUtc          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (PlanId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE FactEtcSnapshots (
    EtcId               BIGINT NOT NULL AUTO_INCREMENT,
    SnapshotLabel       VARCHAR(100) NOT NULL,
    LoadUtc             DATETIME(6) NOT NULL,
    SourceSystemId      BIGINT NOT NULL,
    MeasurementPeriodId BIGINT NOT NULL,
    EngagementId        VARCHAR(64) NOT NULL,
    EmployeeId          BIGINT NOT NULL,
    LevelId             BIGINT NULL,
    HoursIncurred       DECIMAL(12,2) NOT NULL,
    EtcRemaining        DECIMAL(12,2) NOT NULL,
    CreatedUtc          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (EtcId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE FactEngagementMargins (
    MeasurementPeriodId BIGINT NOT NULL,
    EngagementId        VARCHAR(64) NOT NULL,
    MarginValue         DECIMAL(6,3) NOT NULL,
    PRIMARY KEY (MeasurementPeriodId, EngagementId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE FactDeclaredErpWeeks (
    ErpId               BIGINT NOT NULL AUTO_INCREMENT,
    SourceSystemId      BIGINT NOT NULL,
    MeasurementPeriodId BIGINT NOT NULL,
    WeekStartDate       DATE NOT NULL,
    EngagementId        VARCHAR(64) NOT NULL,
    EmployeeId          BIGINT NOT NULL,
    DeclaredHours       DECIMAL(12,2) NOT NULL,
    LoadUtc             DATETIME(6) NOT NULL,
    CreatedUtc          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (ErpId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE FactDeclaredRetainWeeks (
    RetainId            BIGINT NOT NULL AUTO_INCREMENT,
    SourceSystemId      BIGINT NOT NULL,
    MeasurementPeriodId BIGINT NOT NULL,
    WeekStartDate       DATE NOT NULL,
    EngagementId        VARCHAR(64) NOT NULL,
    EmployeeId          BIGINT NOT NULL,
    DeclaredHours       DECIMAL(12,2) NOT NULL,
    LoadUtc             DATETIME(6) NOT NULL,
    CreatedUtc          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (RetainId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE FactTimesheetCharges (
    ChargeId            BIGINT NOT NULL AUTO_INCREMENT,
    SourceSystemId      BIGINT NOT NULL,
    MeasurementPeriodId BIGINT NOT NULL,
    ChargeDate          DATE NOT NULL,
    EngagementId        VARCHAR(64) NOT NULL,
    EmployeeId          BIGINT NOT NULL,
    HoursCharged        DECIMAL(12,2) NOT NULL,
    CostAmount          DECIMAL(14,4) NULL,
    LoadUtc             DATETIME(6) NOT NULL,
    CreatedUtc          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (ChargeId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE AuditEtcVsCharges (
    AuditId             BIGINT NOT NULL AUTO_INCREMENT,
    SnapshotLabel       VARCHAR(100) NOT NULL,
    MeasurementPeriodId BIGINT NOT NULL,
    EngagementId        VARCHAR(64) NOT NULL,
    EmployeeId          BIGINT NOT NULL,
    LastWeekEndDate     DATE NOT NULL,
    EtcHoursIncurred    DECIMAL(12,2) NOT NULL,
    ChargesSumHours     DECIMAL(12,2) NOT NULL,
    DiffHours           DECIMAL(12,2) NOT NULL,
    CreatedUtc          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (AuditId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------
-- [3] INDEXES (unique + regular). Still no FKs.
-- -----------------------------------------------

-- DimSourceSystems
CREATE UNIQUE INDEX UX_DimSourceSystems_SystemCode ON DimSourceSystems (SystemCode);

-- MeasurementPeriods
CREATE INDEX IX_MeasurementPeriods_Description ON MeasurementPeriods (Description);

-- DimEngagements
CREATE INDEX IX_DimEngagements_IsActive ON DimEngagements (IsActive);

-- DimLevels
CREATE UNIQUE INDEX UX_DimLevels_LevelCode ON DimLevels (LevelCode);

-- DimEmployees
CREATE UNIQUE INDEX UX_DimEmployees_NormalizedName ON DimEmployees (NormalizedName);

-- Maps
CREATE INDEX IX_MapEmployeeAliases_SourceSystem_NormalizedRaw ON MapEmployeeAliases (SourceSystemId, NormalizedRaw);
CREATE INDEX IX_MapEmployeeAliases_Employee ON MapEmployeeAliases (EmployeeId);

CREATE INDEX IX_MapEmployeeCodes_Employee ON MapEmployeeCodes (EmployeeId);

CREATE INDEX IX_MapLevelAliases_SourceSystem_NormalizedRaw ON MapLevelAliases (SourceSystemId, NormalizedRaw);
CREATE INDEX IX_MapLevelAliases_Level ON MapLevelAliases (LevelId);

-- Facts
CREATE INDEX IX_FactPlanByLevels_Period_Engagement_Level
    ON FactPlanByLevels (MeasurementPeriodId, EngagementId, LevelId);

CREATE INDEX IX_FactEtcSnapshots_Period_Engagement_Employee
    ON FactEtcSnapshots (MeasurementPeriodId, EngagementId, EmployeeId);

-- Speed up "latest per employee" view
CREATE INDEX IX_FactEtcSnapshots_EngEmpLoadUtcEtcId
    ON FactEtcSnapshots (EngagementId, EmployeeId, LoadUtc, EtcId);

CREATE UNIQUE INDEX UX_FactDeclaredErpWeeks_Period_Engagement_Employee_Week
    ON FactDeclaredErpWeeks (MeasurementPeriodId, EngagementId, EmployeeId, WeekStartDate);

CREATE UNIQUE INDEX UX_FactDeclaredRetainWeeks_Period_Engagement_Employee_Week
    ON FactDeclaredRetainWeeks (MeasurementPeriodId, EngagementId, EmployeeId, WeekStartDate);

CREATE INDEX IX_FactTimesheetCharges_Period_Engagement_Employee_Date
    ON FactTimesheetCharges (MeasurementPeriodId, EngagementId, EmployeeId, ChargeDate);

-- -----------------------------------------------
-- [4] FOREIGN KEYS
-- -----------------------------------------------

-- MapEmployeeAliases
ALTER TABLE MapEmployeeAliases
  ADD CONSTRAINT FK_MapEmployeeAliases_SourceSystem
    FOREIGN KEY (SourceSystemId) REFERENCES DimSourceSystems (SourceSystemId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_MapEmployeeAliases_Employee
    FOREIGN KEY (EmployeeId) REFERENCES DimEmployees (EmployeeId)
    ON DELETE RESTRICT ON UPDATE CASCADE;

-- MapEmployeeCodes
ALTER TABLE MapEmployeeCodes
  ADD CONSTRAINT FK_MapEmployeeCodes_SourceSystem
    FOREIGN KEY (SourceSystemId) REFERENCES DimSourceSystems (SourceSystemId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_MapEmployeeCodes_Employee
    FOREIGN KEY (EmployeeId) REFERENCES DimEmployees (EmployeeId)
    ON DELETE RESTRICT ON UPDATE CASCADE;

-- MapLevelAliases
ALTER TABLE MapLevelAliases
  ADD CONSTRAINT FK_MapLevelAliases_SourceSystem
    FOREIGN KEY (SourceSystemId) REFERENCES DimSourceSystems (SourceSystemId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_MapLevelAliases_Level
    FOREIGN KEY (LevelId) REFERENCES DimLevels (LevelId)
    ON DELETE RESTRICT ON UPDATE CASCADE;

-- FactPlanByLevels
ALTER TABLE FactPlanByLevels
  ADD CONSTRAINT FK_FactPlanByLevels_SourceSystem
    FOREIGN KEY (SourceSystemId) REFERENCES DimSourceSystems (SourceSystemId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactPlanByLevels_MeasurementPeriod
    FOREIGN KEY (MeasurementPeriodId) REFERENCES MeasurementPeriods (PeriodId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactPlanByLevels_Engagement
    FOREIGN KEY (EngagementId) REFERENCES DimEngagements (EngagementId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactPlanByLevels_Level
    FOREIGN KEY (LevelId) REFERENCES DimLevels (LevelId)
    ON DELETE RESTRICT ON UPDATE CASCADE;

-- FactEtcSnapshots
ALTER TABLE FactEtcSnapshots
  ADD CONSTRAINT FK_FactEtcSnapshots_SourceSystem
    FOREIGN KEY (SourceSystemId) REFERENCES DimSourceSystems (SourceSystemId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactEtcSnapshots_MeasurementPeriod
    FOREIGN KEY (MeasurementPeriodId) REFERENCES MeasurementPeriods (PeriodId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactEtcSnapshots_Engagement
    FOREIGN KEY (EngagementId) REFERENCES DimEngagements (EngagementId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactEtcSnapshots_Employee
    FOREIGN KEY (EmployeeId) REFERENCES DimEmployees (EmployeeId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactEtcSnapshots_Level
    FOREIGN KEY (LevelId) REFERENCES DimLevels (LevelId)
    ON DELETE SET NULL ON UPDATE CASCADE;

-- FactEngagementMargins
ALTER TABLE FactEngagementMargins
  ADD CONSTRAINT FK_FactEngagementMargins_MeasurementPeriod
    FOREIGN KEY (MeasurementPeriodId) REFERENCES MeasurementPeriods (PeriodId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactEngagementMargins_Engagement
    FOREIGN KEY (EngagementId) REFERENCES DimEngagements (EngagementId)
    ON DELETE RESTRICT ON UPDATE CASCADE;

-- FactDeclaredErpWeeks
ALTER TABLE FactDeclaredErpWeeks
  ADD CONSTRAINT FK_FactDeclaredErpWeeks_SourceSystem
    FOREIGN KEY (SourceSystemId) REFERENCES DimSourceSystems (SourceSystemId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactDeclaredErpWeeks_MeasurementPeriod
    FOREIGN KEY (MeasurementPeriodId) REFERENCES MeasurementPeriods (PeriodId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactDeclaredErpWeeks_Engagement
    FOREIGN KEY (EngagementId) REFERENCES DimEngagements (EngagementId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactDeclaredErpWeeks_Employee
    FOREIGN KEY (EmployeeId) REFERENCES DimEmployees (EmployeeId)
    ON DELETE RESTRICT ON UPDATE CASCADE;

-- FactDeclaredRetainWeeks
ALTER TABLE FactDeclaredRetainWeeks
  ADD CONSTRAINT FK_FactDeclaredRetainWeeks_SourceSystem
    FOREIGN KEY (SourceSystemId) REFERENCES DimSourceSystems (SourceSystemId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactDeclaredRetainWeeks_MeasurementPeriod
    FOREIGN KEY (MeasurementPeriodId) REFERENCES MeasurementPeriods (PeriodId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactDeclaredRetainWeeks_Engagement
    FOREIGN KEY (EngagementId) REFERENCES DimEngagements (EngagementId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactDeclaredRetainWeeks_Employee
    FOREIGN KEY (EmployeeId) REFERENCES DimEmployees (EmployeeId)
    ON DELETE RESTRICT ON UPDATE CASCADE;

-- FactTimesheetCharges
ALTER TABLE FactTimesheetCharges
  ADD CONSTRAINT FK_FactTimesheetCharges_SourceSystem
    FOREIGN KEY (SourceSystemId) REFERENCES DimSourceSystems (SourceSystemId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactTimesheetCharges_MeasurementPeriod
    FOREIGN KEY (MeasurementPeriodId) REFERENCES MeasurementPeriods (PeriodId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactTimesheetCharges_Engagement
    FOREIGN KEY (EngagementId) REFERENCES DimEngagements (EngagementId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_FactTimesheetCharges_Employee
    FOREIGN KEY (EmployeeId) REFERENCES DimEmployees (EmployeeId)
    ON DELETE RESTRICT ON UPDATE CASCADE;

-- AuditEtcVsCharges
ALTER TABLE AuditEtcVsCharges
  ADD CONSTRAINT FK_AuditEtcVsCharges_MeasurementPeriod
    FOREIGN KEY (MeasurementPeriodId) REFERENCES MeasurementPeriods (PeriodId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_AuditEtcVsCharges_Engagement
    FOREIGN KEY (EngagementId) REFERENCES DimEngagements (EngagementId)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  ADD CONSTRAINT FK_AuditEtcVsCharges_Employee
    FOREIGN KEY (EmployeeId) REFERENCES DimEmployees (EmployeeId)
    ON DELETE RESTRICT ON UPDATE CASCADE;

-- -----------------------------------------------
-- [Views] (CamelCase names; MySQL 5.7 compatible)
-- -----------------------------------------------

-- Latest ETC per (EngagementId, EmployeeId) w/o window functions
CREATE OR REPLACE VIEW VwLatestEtcPerEmployee AS
SELECT
    s.EtcId,
    s.SnapshotLabel,
    s.LoadUtc,
    s.SourceSystemId,
    s.EngagementId,
    s.EmployeeId,
    s.LevelId,
    s.HoursIncurred,
    s.EtcRemaining,
    s.CreatedUtc
FROM FactEtcSnapshots AS s
WHERE NOT EXISTS (
    SELECT 1
    FROM FactEtcSnapshots AS s2
    WHERE s2.EngagementId = s.EngagementId
      AND s2.EmployeeId   = s.EmployeeId
      AND (
           s2.LoadUtc > s.LoadUtc
        OR (s2.LoadUtc = s.LoadUtc AND s2.EtcId > s.EtcId)
      )
);

CREATE OR REPLACE VIEW VwChargesSum AS
SELECT
    ft.EngagementId,
    ft.EmployeeId,
    ft.ChargeDate,
    CAST(SUM(ft.HoursCharged) AS DECIMAL(34,2)) AS HoursCharged
FROM FactTimesheetCharges AS ft
GROUP BY
    ft.EngagementId,
    ft.EmployeeId,
    ft.ChargeDate;

CREATE OR REPLACE VIEW VwPlanVsActualByLevel AS
SELECT
    p.EngagementId,
    p.LevelId,
    CAST(SUM(p.PlannedHours) AS DECIMAL(34,2)) AS PlannedHours,
    CAST(SUM(e.HoursIncurred) AS DECIMAL(56,2)) AS ActualHours
FROM FactPlanByLevels AS p
LEFT JOIN FactEtcSnapshots AS e
    ON e.EngagementId        = p.EngagementId
   AND e.LevelId             = p.LevelId
   AND e.MeasurementPeriodId = p.MeasurementPeriodId
GROUP BY
    p.EngagementId,
    p.LevelId;

-- Restore FK checks
SET FOREIGN_KEY_CHECKS = @original_foreign_key_checks;
