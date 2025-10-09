using System;
using System.Linq;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;

namespace GRCFinancialControl.Uploads
{
    public sealed class ReconciliationService
    {
        private readonly MySqlDbContext _db;

        public ReconciliationService(MySqlDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public OperationSummary Reconcile(long measurementPeriodId, string snapshotLabel, DateOnly lastWeekEnd)
        {
            var summary = new OperationSummary("ETC vs Charges Reconciliation");
            var trimmedLabel = StringNormalizer.TrimToNull(snapshotLabel) ?? throw new ArgumentException("Snapshot label is required.", nameof(snapshotLabel));
            var normalizedWeekEnd = WeekHelper.ToWeekEnd(lastWeekEnd);

            var latestLoads = _db.FactEtcSnapshots
                .Where(e => e.SnapshotLabel == trimmedLabel && e.MeasurementPeriodId == measurementPeriodId)
                .GroupBy(e => new { e.EngagementId, e.EmployeeId })
                .Select(g => new { g.Key.EngagementId, g.Key.EmployeeId, LatestLoadUtc = g.Max(x => x.LoadUtc) })
                .ToList();

            if (latestLoads.Count == 0)
            {
                summary.AddInfo($"No ETC snapshots found for label '{trimmedLabel}'.");
                return summary;
            }

            var etcLatest = (from latest in latestLoads
                             join snapshot in _db.FactEtcSnapshots on new { latest.EngagementId, latest.EmployeeId, latest.LatestLoadUtc }
                                 equals new { snapshot.EngagementId, snapshot.EmployeeId, LatestLoadUtc = snapshot.LoadUtc }
                             select new
                             {
                                 snapshot.EngagementId,
                                 snapshot.EmployeeId,
                                 snapshot.HoursIncurred
                             }).ToList();

            if (etcLatest.Count == 0)
            {
                summary.AddInfo("No ETC detail rows found for the latest loads.");
                return summary;
            }

            var engagementIds = etcLatest.Select(x => x.EngagementId).Distinct().ToList();
            var employeeIds = etcLatest.Select(x => x.EmployeeId).Distinct().ToList();

            var chargesLookup = _db.FactTimesheetCharges
                .Where(c => c.ChargeDate <= normalizedWeekEnd && c.MeasurementPeriodId == measurementPeriodId && engagementIds.Contains(c.EngagementId) && employeeIds.Contains(c.EmployeeId))
                .GroupBy(c => new { c.EngagementId, c.EmployeeId })
                .Select(g => new { g.Key.EngagementId, g.Key.EmployeeId, Hours = g.Sum(x => x.HoursCharged) })
                .ToDictionary(x => (x.EngagementId, x.EmployeeId), x => x.Hours);

            var existingAudits = _db.AuditEtcVsCharges
                .Where(a => a.SnapshotLabel == trimmedLabel && a.LastWeekEndDate == normalizedWeekEnd && a.MeasurementPeriodId == measurementPeriodId)
                .ToList();

            if (existingAudits.Count > 0)
            {
                _db.AuditEtcVsCharges.RemoveRange(existingAudits);
                summary.RegisterRemoval(existingAudits.Count, $"Removed {existingAudits.Count} prior audit row(s) for snapshot '{trimmedLabel}'.");
            }

            foreach (var entry in etcLatest)
            {
                var chargeHours = chargesLookup.TryGetValue((entry.EngagementId, entry.EmployeeId), out var hours)
                    ? hours
                    : 0m;

                var diff = entry.HoursIncurred - chargeHours;
                if (diff == 0m)
                {
                    continue;
                }

                _db.AuditEtcVsCharges.Add(new AuditEtcVsCharges
                {
                    SnapshotLabel = trimmedLabel,
                    MeasurementPeriodId = measurementPeriodId,
                    EngagementId = entry.EngagementId,
                    EmployeeId = entry.EmployeeId,
                    LastWeekEndDate = normalizedWeekEnd,
                    EtcHoursIncurred = entry.HoursIncurred,
                    ChargesSumHours = chargeHours,
                    DiffHours = diff,
                    CreatedUtc = DateTime.UtcNow
                });

                summary.IncrementInserted();
            }

            if (summary.Inserted == 0)
            {
                summary.AddInfo("No variances detected between ETC and charges up to the selected week end.");
            }

            _db.SaveChanges();
            return summary;
        }
    }
}
