using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;
using GRCFinancialControl.Parsing;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Uploads
{
    public sealed class MarginDataUploadService
    {
        private readonly MySqlDbContext _db;
        private readonly IdResolver _ids;

        public MarginDataUploadService(MySqlDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ids = new IdResolver(db);
        }

        public OperationSummary Load(long measurementPeriodId, IReadOnlyList<MarginDataRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var summary = new OperationSummary("Load Margin Data");
            if (rows.Count == 0)
            {
                summary.AddInfo("No margin rows parsed; skipping load.");
                return summary;
            }

            if (_db.MeasurementPeriods.Find(measurementPeriodId) == null)
            {
                throw new UploadDataException($"Measurement period '{measurementPeriodId}' was not found in MySQL.");
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

                engagementId = _ids.EnsureEngagement(engagementId, row.EngagementTitle, (entity, isNew) =>
                {
                    if (!isNew)
                    {
                        return false;
                    }

                    var openingMargin = row.OpeningMargin.HasValue
                        ? Math.Round(row.OpeningMargin.Value, 3, MidpointRounding.AwayFromZero)
                        : 0m;
                    entity.OpeningMargin = openingMargin;
                    entity.CurrentMargin = row.CurrentMargin.HasValue
                        ? Math.Round(Convert.ToDouble(row.CurrentMargin.Value), 6, MidpointRounding.AwayFromZero)
                        : Math.Round(Convert.ToDouble(openingMargin), 6, MidpointRounding.AwayFromZero);
                    entity.LastMarginUpdateDate = now;
                    return true;
                });

                var engagement = _db.DimEngagements.Local.FirstOrDefault(e => e.EngagementId == engagementId) ??
                                  _db.DimEngagements.SingleOrDefault(e => e.EngagementId == engagementId);
                if (engagement == null)
                {
                    throw new UploadDataException($"Engagement '{engagementId}' referenced on row {row.ExcelRowNumber} could not be resolved after creation attempt.");
                }

                var engagementEntry = _db.Entry(engagement);
                var isNewEngagement = engagementEntry.State == EntityState.Added;

                var engagementUpdated = false;
                if (isNewEngagement)
                {
                    engagementUpdated = true;
                    summary.AddInfo($"Row {row.ExcelRowNumber}: Created engagement '{engagementId}' from margin file.");
                }
                if (row.OpeningMargin.HasValue && HasDifferentMargin(engagement.OpeningMargin, row.OpeningMargin.Value))
                {
                    engagement.OpeningMargin = Math.Round(row.OpeningMargin.Value, 3, MidpointRounding.AwayFromZero);
                    engagementUpdated = true;
                }

                if (row.CurrentMargin.HasValue && HasDifferentMargin(Convert.ToDecimal(engagement.CurrentMargin), row.CurrentMargin.Value))
                {
                    engagement.CurrentMargin = Math.Round(Convert.ToDouble(row.CurrentMargin.Value), 6, MidpointRounding.AwayFromZero);
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

        private static bool HasDifferentMargin(decimal currentValue, decimal targetValue)
        {
            var roundedTarget = Math.Round(targetValue, 3, MidpointRounding.AwayFromZero);
            return Math.Abs(currentValue - roundedTarget) > 0.0005m;
        }
    }
}
