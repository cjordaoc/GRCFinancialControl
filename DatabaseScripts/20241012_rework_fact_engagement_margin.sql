-- Rework fact_engagement_margin table structure
ALTER TABLE fact_engagement_margin
    DROP PRIMARY KEY;

ALTER TABLE fact_engagement_margin
    DROP COLUMN margin_id,
    DROP COLUMN snapshot_label,
    DROP COLUMN load_utc,
    DROP COLUMN source_system_id,
    DROP COLUMN created_utc,
    CHANGE COLUMN projected_margin_pct margin_value DECIMAL(6,3) NOT NULL;

ALTER TABLE fact_engagement_margin
    ADD PRIMARY KEY (measurement_period_id, engagement_id);
