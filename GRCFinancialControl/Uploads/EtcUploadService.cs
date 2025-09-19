using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;
using GRCFinancialControl.Parsing;

namespace GRCFinancialControl.Uploads
{
    public sealed class EtcUploadService
    {
        private readonly MySqlDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _sourceId;

        public EtcUploadService(MySqlDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ids = new IdResolver(db);
            _sourceId = _ids.EnsureSourceSystem("ETC", "Engagement ETC Snapshot");
        }

        public OperationSummary Load(ushort measurementPeriodId, string snapshotLabel, IReadOnlyList<EtcRow> rows)
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

                var engagementId = _ids.EnsureEngagement(engagementKey!);

                foreach (var row in group)
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

            if (summary.Inserted == 0)
            {
                summary.AddInfo("No valid ETC rows to insert.");
                return summary;
            }
            return summary;
        }
    }
}
