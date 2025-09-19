using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Data;

namespace GRCFinancialControl.Services
{
    public sealed class MeasurementPeriodService
    {
        private readonly MySqlDbContext _db;

        public MeasurementPeriodService(MySqlDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public List<MeasurementPeriod> LoadAllPeriods()
        {
            return _db.MeasurementPeriods
                .OrderByDescending(p => p.StartDate)
                .ThenByDescending(p => p.EndDate)
                .ToList();
        }

        public MeasurementPeriod? LoadPeriod(ushort periodId)
        {
            return _db.MeasurementPeriods.Find(periodId);
        }

        public ushort Insert(string description, DateOnly startDate, DateOnly endDate)
        {
            Validate(description, startDate, endDate);
            var entity = new MeasurementPeriod
            {
                Description = description.Trim(),
                StartDate = startDate,
                EndDate = endDate,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            _db.MeasurementPeriods.Add(entity);
            _db.SaveChanges();
            return entity.PeriodId;
        }

        public void Update(MeasurementPeriod period)
        {
            if (period == null)
            {
                throw new ArgumentNullException(nameof(period));
            }

            Validate(period.Description, period.StartDate, period.EndDate);

            var existing = _db.MeasurementPeriods.Find(period.PeriodId)
                ?? throw new InvalidOperationException($"Measurement period {period.PeriodId} was not found.");

            existing.Description = period.Description.Trim();
            existing.StartDate = period.StartDate;
            existing.EndDate = period.EndDate;
            existing.UpdatedUtc = DateTime.UtcNow;

            _db.SaveChanges();
        }

        public void Delete(ushort periodId)
        {
            var entity = _db.MeasurementPeriods.Find(periodId);
            if (entity == null)
            {
                return;
            }

            _db.MeasurementPeriods.Remove(entity);
            _db.SaveChanges();
        }

        private static void Validate(string description, DateOnly startDate, DateOnly endDate)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Description is required.", nameof(description));
            }

            if (endDate < startDate)
            {
                throw new ArgumentException("End date must be greater than or equal to start date.", nameof(endDate));
            }
        }
    }
}
