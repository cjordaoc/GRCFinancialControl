-- Composite indexes to accelerate OLTP uploads
ALTER TABLE FactEtcSnapshots
    ADD INDEX IX_FactEtcSnapshots_PeriodEngagementEmployee (MeasurementPeriodId, EngagementId, EmployeeId);

ALTER TABLE FactDeclaredErpWeek
    ADD UNIQUE INDEX UX_FactDeclaredErpWeek_PeriodEngagementEmployeeWeek (MeasurementPeriodId, EngagementId, EmployeeId, WeekStartDate);

ALTER TABLE FactDeclaredRetainWeek
    ADD UNIQUE INDEX UX_FactDeclaredRetainWeek_PeriodEngagementEmployeeWeek (MeasurementPeriodId, EngagementId, EmployeeId, WeekStartDate);

ALTER TABLE FactTimesheetCharge
    ADD INDEX IX_FactTimesheetCharge_PeriodEngagementEmployeeDate (MeasurementPeriodId, EngagementId, EmployeeId, ChargeDate);

ALTER TABLE FactPlanByLevel
    ADD INDEX IX_FactPlanByLevel_PeriodEngagementLevel (MeasurementPeriodId, EngagementId, LevelId);
