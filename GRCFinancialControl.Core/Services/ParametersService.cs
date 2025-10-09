using System;
using GRCFinancialControl.Data;

namespace GRCFinancialControl.Services
{
    public sealed class ParametersService
    {
        private readonly Func<LocalSqliteContext> _contextFactory;
        private const string SelectedMeasurementPeriodKey = "SelectedMeasurePeriod";

        public ParametersService(Func<LocalSqliteContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public string? GetSelectedMeasurePeriodId()
        {
            using var context = _contextFactory();
            var entry = context.Parameters.Find(SelectedMeasurementPeriodKey);
            return entry?.Value;
        }

        public void SetSelectedMeasurePeriodId(string periodId)
        {
            if (string.IsNullOrWhiteSpace(periodId))
            {
                throw new ArgumentException("Measurement period id is required.", nameof(periodId));
            }

            using var context = _contextFactory();
            var entry = context.Parameters.Find(SelectedMeasurementPeriodKey);
            if (entry == null)
            {
                entry = new ParameterEntry
                {
                    Key = SelectedMeasurementPeriodKey,
                    Value = periodId.Trim(),
                    UpdatedUtc = DateTime.UtcNow
                };
                context.Parameters.Add(entry);
            }
            else
            {
                entry.Value = periodId.Trim();
                entry.UpdatedUtc = DateTime.UtcNow;
                context.Parameters.Update(entry);
            }

            context.SaveChanges();
        }

        public void ClearSelectedMeasurePeriod()
        {
            using var context = _contextFactory();
            var entry = context.Parameters.Find(SelectedMeasurementPeriodKey);
            if (entry == null)
            {
                return;
            }

            context.Parameters.Remove(entry);
            context.SaveChanges();
        }
    }
}
