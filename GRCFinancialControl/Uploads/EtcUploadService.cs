using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;
using GRCFinancialControl.Parsing;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Uploads
{
    public sealed class EtcUploadService
    {
        private readonly MySqlDbContext _db;
        private readonly IdResolver _ids;
        private readonly long _sourceId;

        public EtcUploadService(MySqlDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ids = new IdResolver(db);
            _sourceId = _ids.EnsureSourceSystem("ETC", "Engagement ETC Snapshot");
        }

        public OperationSummary Load(long measurementPeriodId, string snapshotLabel, IReadOnlyList<EtcRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var summary = new OperationSummary("Load ETC Snapshot");
            if (rows.Count == 0)
            {
                summary.AddInfo("No ETC rows parsed; skipping load.");
                return summary;
            }

            var trimmedLabel = StringNormalizer.TrimToNull(snapshotLabel) ?? throw new ArgumentException("Snapshot label is required.", nameof(snapshotLabel));
            var loadUtc = DateTime.UtcNow;

            foreach (var group in rows.GroupBy(r => StringNormalizer.TrimToNull(r.EngagementId), StringComparer.OrdinalIgnoreCase))
            {
                var engagementKey = group.Key;
                if (string.IsNullOrWhiteSpace(engagementKey))
                {
                    summary.RegisterSkip("Skipped rows without engagement id.");
                    continue;
                }

                var engagementId = _ids.TryResolveEngagement(engagementKey!);
                if (engagementId == null)
                {
                    summary.RegisterSkip($"Skipped ETC rows for unknown engagement '{engagementKey}'.");
                    continue;
                }

                foreach (var row in group)
                {
                    var employeeName = StringNormalizer.TrimToNull(row.EmployeeName);
                    if (employeeName == null)
                    {
                        summary.RegisterSkip("Skipped row with missing employee name.");
                        continue;
                    }

                    var employeeId = _ids.EnsureEmployee(_sourceId, employeeName);
                    long? levelId = null;
                    var levelSource = row.NormalizedLevel ?? row.RawLevel;
                    if (!string.IsNullOrWhiteSpace(levelSource))
                    {
                        levelId = _ids.EnsureLevel(_sourceId, levelSource);
                    }

                    _db.FactEtcSnapshots.Add(new FactEtcSnapshot
                    {
                        SnapshotLabel = trimmedLabel,
                        LoadUtc = loadUtc,
                        SourceSystemId = _sourceId,
                        MeasurementPeriodId = measurementPeriodId,
                        EngagementId = engagementId,
                        EmployeeId = employeeId,
                        LevelId = levelId,
                        HoursIncurred = row.HoursIncurred,
                        EtcRemaining = row.EtcRemaining,
                        CreatedUtc = DateTime.UtcNow
                    });

                    summary.IncrementInserted();
                }
            }

            SuppressAccidentalEngagementWrites(summary);

            if (summary.Inserted == 0)
            {
                summary.AddInfo("No valid ETC rows to insert.");
                return summary;
            }
            return summary;
        }

        private void SuppressAccidentalEngagementWrites(OperationSummary summary)
        {
            _db.ChangeTracker.DetectChanges();
            var pendingEngagements = _db.ChangeTracker
                .Entries<DimEngagement>()
                .ToList();

            var suppressed = false;
            foreach (var entry in pendingEngagements)
            {
                if (entry.State == EntityState.Added)
                {
                    var id = entry.Entity.EngagementId;
                    entry.State = EntityState.Detached;
                    summary.AddWarning($"Suppressed unintended creation of engagement '{id}' during ETC upload. Seed master data and rerun if required.");
                    suppressed = true;
                }
                else if (entry.State == EntityState.Modified)
                {
                    var id = entry.Entity.EngagementId;
                    entry.State = EntityState.Unchanged;
                    summary.AddWarning($"Reverted unintended update to engagement '{id}' during ETC upload. Master data updates must run outside the ETC pipeline.");
                    suppressed = true;
                }
            }

            if (suppressed)
            {
                summary.AddInfo("ETC upload ran in engagement read-only mode; unintended master-data changes were discarded.");
            }
        }
    }
}
