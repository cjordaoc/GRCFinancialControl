-- Master data baseline for measurement periods and engagement schema normalization

CREATE TABLE IF NOT EXISTS measurement_periods (
    period_id SMALLINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    description VARCHAR(255) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    created_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT chk_measurement_period_dates CHECK (start_date <= end_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET @original_sql_mode = @@SESSION.sql_mode;
SET @patched_sql_mode = IF(
    FIND_IN_SET('NO_AUTO_VALUE_ON_ZERO', @original_sql_mode) = 0,
    CONCAT_WS(',', NULLIF(@original_sql_mode, ''), 'NO_AUTO_VALUE_ON_ZERO'),
    @original_sql_mode
);
SET SESSION sql_mode = @patched_sql_mode;
INSERT INTO measurement_periods (period_id, description, start_date, end_date, created_utc, updated_utc)
VALUES (0, 'Legacy / Pre-migration', '1900-01-01', '1900-01-01', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    description = VALUES(description),
    start_date = VALUES(start_date),
    end_date = VALUES(end_date),
    updated_utc = VALUES(updated_utc);
SET SESSION sql_mode = @original_sql_mode;

ALTER TABLE dim_engagement
    MODIFY COLUMN engagement_title VARCHAR(255) NOT NULL,
    MODIFY COLUMN engagement_partner VARCHAR(255) NULL,
    MODIFY COLUMN engagement_manager VARCHAR(255) NULL,
    MODIFY COLUMN opening_margin DECIMAL(6,3) NOT NULL DEFAULT 0.000;

CREATE INDEX IF NOT EXISTS idx_dim_engagement_is_active ON dim_engagement (is_active);

ALTER TABLE fact_plan_by_level
    ADD COLUMN IF NOT EXISTS measurement_period_id SMALLINT UNSIGNED NOT NULL DEFAULT 0 AFTER source_system_id;

ALTER TABLE fact_etc_snapshot
    ADD COLUMN IF NOT EXISTS measurement_period_id SMALLINT UNSIGNED NOT NULL DEFAULT 0 AFTER source_system_id;

ALTER TABLE fact_declared_erp_week
    ADD COLUMN IF NOT EXISTS measurement_period_id SMALLINT UNSIGNED NOT NULL DEFAULT 0 AFTER source_system_id;

ALTER TABLE fact_declared_retain_week
    ADD COLUMN IF NOT EXISTS measurement_period_id SMALLINT UNSIGNED NOT NULL DEFAULT 0 AFTER source_system_id;

ALTER TABLE fact_timesheet_charge
    ADD COLUMN IF NOT EXISTS measurement_period_id SMALLINT UNSIGNED NOT NULL DEFAULT 0 AFTER source_system_id;

ALTER TABLE audit_etc_vs_charges
    ADD COLUMN IF NOT EXISTS measurement_period_id SMALLINT UNSIGNED NOT NULL DEFAULT 0 AFTER snapshot_label;

SET @rename_margin = IF(
    EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fact_engagement_margin')
        AND NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'fact_engagement_margin'
              AND COLUMN_NAME = 'measurement_period_id')
        AND NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'fact_engagement_margin_legacy'),
    'RENAME TABLE fact_engagement_margin TO fact_engagement_margin_legacy;',
    'SELECT 0;'
);
PREPARE stmt FROM @rename_margin;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

CREATE TABLE IF NOT EXISTS fact_engagement_margin (
    measurement_period_id SMALLINT UNSIGNED NOT NULL,
    engagement_id VARCHAR(64) NOT NULL,
    margin_value DECIMAL(6,3) NOT NULL,
    PRIMARY KEY (measurement_period_id, engagement_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
