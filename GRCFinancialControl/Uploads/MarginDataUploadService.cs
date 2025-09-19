using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;
using GRCFinancialControl.Parsing;

namespace GRCFinancialControl.Uploads
{
    public sealed class MarginDataUploadService
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;

        public MarginDataUploadService(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ids = new IdResolver(db);
        }

        public OperationSummary Load(ushort measurementPeriodId, IReadOnlyList<MarginDataRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var summary = new OperationSummary("Load Margin Data");
            if (rows.Count == 0)
            {
                summary.AddInfo("No margin rows parsed; skipping load.");
                return summary;
            }

            if (_db.DimMeasurementPeriods.Find(measurementPeriodId) == null)
            {
                throw new InvalidOperationException($"Measurement period {measurementPeriodId} was not found.");
            }

            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            foreach (var row in rows)
            {
                var engagementId = StringNormalizer.TrimToNull(row.EngagementId);
                if (engagementId == null)
                {
                    summary.RegisterSkip($"Row {row.ExcelRowNumber}: Missing engagement identifier.");
                    continue;
                }

                if (!processed.Add(engagementId))
                {
                    summary.RegisterDuplicate($"Row {row.ExcelRowNumber}: Duplicate engagement '{engagementId}'.");
                    continue;
                }

                engagementId = _ids.EnsureEngagement(engagementId, row.EngagementTitle);
                var engagement = _db.DimEngagements.Single(e => e.EngagementId == engagementId);

                var engagementUpdated = false;
                if (row.OpeningMargin.HasValue && HasDifferentMargin(engagement.OpeningMargin, row.OpeningMargin.Value))
                {
                    engagement.OpeningMargin = Convert.ToDouble(Math.Round(row.OpeningMargin.Value, 6, MidpointRounding.AwayFromZero));
                    engagementUpdated = true;
                }

                if (row.CurrentMargin.HasValue && HasDifferentMargin(engagement.CurrentMargin, row.CurrentMargin.Value))
                {
                    engagement.CurrentMargin = Convert.ToDouble(Math.Round(row.CurrentMargin.Value, 6, MidpointRounding.AwayFromZero));
                    engagementUpdated = true;
                }

                var factChanged = false;
                var factUpdated = false;
                if (row.MarginValue.HasValue)
                {
                    var marginValue = Math.Round(row.MarginValue.Value, 6, MidpointRounding.AwayFromZero);
                    var existing = _db.FactEngagementMargins.SingleOrDefault(m => m.MeasurementPeriodId == measurementPeriodId && m.EngagementId == engagementId);
                    if (existing == null)
                    {
                        _db.FactEngagementMargins.Add(new FactEngagementMargin
                        {
                            MeasurementPeriodId = measurementPeriodId,
                            EngagementId = engagementId,
                            MarginValue = marginValue
                        });
                        summary.IncrementInserted();
                        factChanged = true;
                    }
                    else if (existing.MarginValue != marginValue)
                    {
                        existing.MarginValue = marginValue;
                        factChanged = true;
                        factUpdated = true;
                    }
                }
                else
                {
                    summary.AddWarning($"Row {row.ExcelRowNumber}: Margin value missing; fact table was not updated.");
                }

                if (engagementUpdated)
                {
                    summary.IncrementUpdated();
                }

                if (factUpdated)
                {
                    summary.IncrementUpdated();
                }

                if (engagementUpdated || factChanged)
                {
                    engagement.LastMarginUpdateDate = now;
                    engagement.UpdatedUtc = now;
                }
            }

            return summary;
        }

        private static bool HasDifferentMargin(double currentValue, decimal targetValue)
        {
            var targetDouble = Convert.ToDouble(Math.Round(targetValue, 6, MidpointRounding.AwayFromZero));
            return Math.Abs(currentValue - targetDouble) > 0.0005d;
        }
    }
}
