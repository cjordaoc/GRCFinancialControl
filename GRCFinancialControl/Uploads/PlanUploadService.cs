using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;
using GRCFinancialControl.Parsing;

namespace GRCFinancialControl.Uploads
{
    public sealed class PlanUploadService
    {
        private readonly MySqlDbContext _db;
        private readonly IdResolver _ids;
        private readonly long _sourceId;

        public PlanUploadService(MySqlDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ids = new IdResolver(db);
            _sourceId = _ids.EnsureSourceSystem("WEEKLY_PRICING", "Initial Engagement Plan");
        }

        public OperationSummary Load(long measurementPeriodId, IReadOnlyList<PlanRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var summary = new OperationSummary("Load Plan");
            if (rows.Count == 0)
            {
                summary.AddInfo("No plan rows parsed; skipping load.");
                return summary;
            }

            var loadUtc = DateTime.UtcNow;
            var entities = new List<FactPlanByLevel>(rows.Count);
            foreach (var group in rows.GroupBy(r => StringNormalizer.TrimToNull(r.EngagementId), StringComparer.OrdinalIgnoreCase))
            {
                var engagementKey = group.Key;
                if (string.IsNullOrWhiteSpace(engagementKey))
                {
                    summary.RegisterSkip("Skipped plan rows without engagement id.");
                    continue;
                }

                var engagementId = _ids.EnsureEngagement(engagementKey!);

                foreach (var row in group)
                {
                    var levelName = StringNormalizer.TrimToNull(row.NormalizedLevel ?? row.RawLevel);
                    if (levelName == null)
                    {
                        summary.RegisterSkip("Skipped row with missing level.");
                        continue;
                    }

                    var levelId = _ids.EnsureLevel(_sourceId, levelName);
                    entities.Add(new FactPlanByLevel
                    {
                        LoadUtc = loadUtc,
                        SourceSystemId = _sourceId,
                        MeasurementPeriodId = measurementPeriodId,
                        EngagementId = engagementId,
                        LevelId = levelId,
                        PlannedHours = row.PlannedHours,
                        PlannedRate = row.PlannedRate,
                        CreatedUtc = DateTime.UtcNow
                    });
                    summary.IncrementInserted();
                }
            }

            if (entities.Count == 0)
            {
                summary.AddInfo("No valid plan rows to insert.");
                return summary;
            }

            _db.FactPlanByLevels.AddRange(entities);
            return summary;
        }
    }
}
