using System;
using System.Collections.Generic;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;
using GRCFinancialControl.Parsing;

namespace GRCFinancialControl.Uploads
{
    public sealed class PlanUploadService
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _sourceId;

        public PlanUploadService(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ids = new IdResolver(db);
            _sourceId = _ids.EnsureSourceSystem("WEEKLY_PRICING", "Initial Engagement Plan");
        }

        public OperationSummary Load(ushort measurementPeriodId, string engagementId, IReadOnlyList<PlanRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var summary = new OperationSummary("Load Plan");
            if (rows.Count == 0)
            {
                summary.AddInfo("No plan rows parsed; skipping load.");
                return summary;
            }

            _ids.EnsureEngagement(engagementId);

            var loadUtc = DateTime.UtcNow;
            var entities = new List<FactPlanByLevel>(rows.Count);
            foreach (var row in rows)
            {
                var levelName = StringNormalizer.TrimToNull(row.RawLevel);
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
