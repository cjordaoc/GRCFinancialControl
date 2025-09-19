using System;
using System.Collections.Generic;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;
using GRCFinancialControl.Parsing;

namespace GRCFinancialControl.Uploads
{
    public sealed class EtcUploadService
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _sourceId;

        public EtcUploadService(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ids = new IdResolver(db);
            _sourceId = _ids.EnsureSourceSystem("ETC", "Engagement ETC Snapshot");
        }

        public OperationSummary Load(ushort measurementPeriodId, string snapshotLabel, string engagementId, IReadOnlyList<EtcRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var summary = new OperationSummary("Load ETC Snapshot");
            if (rows.Count == 0)
            {
                summary.AddInfo("No ETC rows parsed; skipping load.");
                return summary;
            }

            var trimmedLabel = StringNormalizer.TrimToNull(snapshotLabel) ?? throw new ArgumentException("Snapshot label is required.", nameof(snapshotLabel));
            _ids.EnsureEngagement(engagementId);

            var loadUtc = DateTime.UtcNow;
            var entities = new List<FactEtcSnapshot>(rows.Count);
            foreach (var row in rows)
            {
                var employeeName = StringNormalizer.TrimToNull(row.EmployeeName);
                if (employeeName == null)
                {
                    summary.RegisterSkip("Skipped row with missing employee name.");
                    continue;
                }

                var employeeId = _ids.EnsureEmployee(_sourceId, employeeName);
                uint? levelId = null;
                if (!string.IsNullOrWhiteSpace(row.RawLevel))
                {
                    levelId = _ids.EnsureLevel(_sourceId, row.RawLevel);
                }

                entities.Add(new FactEtcSnapshot
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

            if (entities.Count == 0)
            {
                summary.AddInfo("No valid ETC rows to insert.");
                return summary;
            }

            _db.FactEtcSnapshots.AddRange(entities);
            return summary;
        }
    }
}
