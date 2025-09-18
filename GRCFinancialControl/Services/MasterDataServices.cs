using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Data;

namespace GRCFinancialControl.Services
{
    public sealed class MeasurementPeriodService
    {
        private readonly AppDbContext _db;

        public MeasurementPeriodService(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public List<DimMeasurementPeriod> GetAll()
        {
            return _db.DimMeasurementPeriods
                .OrderByDescending(p => p.StartDate)
                .ThenByDescending(p => p.EndDate)
                .ToList();
        }

        public DimMeasurementPeriod Create(string description, DateOnly startDate, DateOnly endDate)
        {
            Validate(description, startDate, endDate);
            var entity = new DimMeasurementPeriod
            {
                Description = description.Trim(),
                StartDate = startDate,
                EndDate = endDate,
                IsActive = !_db.DimMeasurementPeriods.Any(),
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            _db.DimMeasurementPeriods.Add(entity);
            _db.SaveChanges();
            return entity;
        }

        public DimMeasurementPeriod Update(ushort id, string description, DateOnly startDate, DateOnly endDate)
        {
            Validate(description, startDate, endDate);
            var entity = _db.DimMeasurementPeriods.SingleOrDefault(p => p.MeasurementPeriodId == id)
                ?? throw new InvalidOperationException("Measurement period not found.");

            entity.Description = description.Trim();
            entity.StartDate = startDate;
            entity.EndDate = endDate;
            entity.UpdatedUtc = DateTime.UtcNow;
            _db.SaveChanges();
            return entity;
        }

        public void Delete(ushort id)
        {
            var entity = _db.DimMeasurementPeriods.SingleOrDefault(p => p.MeasurementPeriodId == id)
                ?? throw new InvalidOperationException("Measurement period not found.");

            if (entity.IsActive)
            {
                throw new InvalidOperationException("Cannot delete the active measurement period.");
            }

            _db.DimMeasurementPeriods.Remove(entity);
            _db.SaveChanges();
        }

        public bool TryGetSingleActive(out DimMeasurementPeriod? active, out string? error)
        {
            var actives = _db.DimMeasurementPeriods.Where(p => p.IsActive).ToList();
            if (actives.Count == 0)
            {
                active = null;
                error = "No measurement period is marked as active.";
                return false;
            }

            if (actives.Count > 1)
            {
                active = null;
                error = "Multiple measurement periods are marked as active. Please activate only one.";
                return false;
            }

            active = actives[0];
            error = null;
            return true;
        }

        public DimMeasurementPeriod Activate(ushort id)
        {
            var periods = _db.DimMeasurementPeriods.ToList();
            var target = periods.SingleOrDefault(p => p.MeasurementPeriodId == id)
                ?? throw new InvalidOperationException("Measurement period not found.");

            foreach (var period in periods)
            {
                var shouldBeActive = period.MeasurementPeriodId == id;
                if (period.IsActive != shouldBeActive)
                {
                    period.IsActive = shouldBeActive;
                    period.UpdatedUtc = DateTime.UtcNow;
                }
            }

            _db.SaveChanges();
            return target;
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

    public sealed class FiscalYearService
    {
        private readonly AppDbContext _db;

        public FiscalYearService(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public List<DimFiscalYear> GetAll()
        {
            return _db.DimFiscalYears
                .OrderByDescending(f => f.DateFrom)
                .ThenByDescending(f => f.DateTo)
                .ToList();
        }

        public DimFiscalYear Create(string description, DateOnly from, DateOnly to)
        {
            Validate(description, from, to);
            var entity = new DimFiscalYear
            {
                Description = description.Trim(),
                DateFrom = from,
                DateTo = to,
                IsActive = !_db.DimFiscalYears.Any(),
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            _db.DimFiscalYears.Add(entity);
            _db.SaveChanges();
            return entity;
        }

        public DimFiscalYear Update(ushort id, string description, DateOnly from, DateOnly to)
        {
            Validate(description, from, to);
            var entity = _db.DimFiscalYears.SingleOrDefault(f => f.FiscalYearId == id)
                ?? throw new InvalidOperationException("Fiscal year not found.");

            entity.Description = description.Trim();
            entity.DateFrom = from;
            entity.DateTo = to;
            entity.UpdatedUtc = DateTime.UtcNow;
            _db.SaveChanges();
            return entity;
        }

        public void Delete(ushort id)
        {
            var entity = _db.DimFiscalYears.SingleOrDefault(f => f.FiscalYearId == id)
                ?? throw new InvalidOperationException("Fiscal year not found.");

            if (entity.IsActive)
            {
                throw new InvalidOperationException("Cannot delete the active fiscal year.");
            }

            _db.DimFiscalYears.Remove(entity);
            _db.SaveChanges();
        }

        public bool TryGetSingleActive(out DimFiscalYear? active, out string? error)
        {
            var actives = _db.DimFiscalYears.Where(f => f.IsActive).ToList();
            if (actives.Count == 0)
            {
                active = null;
                error = "No fiscal year is marked as active.";
                return false;
            }

            if (actives.Count > 1)
            {
                active = null;
                error = "Multiple fiscal years are marked as active. Please activate only one.";
                return false;
            }

            active = actives[0];
            error = null;
            return true;
        }

        public DimFiscalYear Activate(ushort id)
        {
            var years = _db.DimFiscalYears.ToList();
            var target = years.SingleOrDefault(f => f.FiscalYearId == id)
                ?? throw new InvalidOperationException("Fiscal year not found.");

            foreach (var year in years)
            {
                var shouldBeActive = year.FiscalYearId == id;
                if (year.IsActive != shouldBeActive)
                {
                    year.IsActive = shouldBeActive;
                    year.UpdatedUtc = DateTime.UtcNow;
                }
            }

            _db.SaveChanges();
            return target;
        }

        private static void Validate(string description, DateOnly from, DateOnly to)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Description is required.", nameof(description));
            }

            if (to < from)
            {
                throw new ArgumentException("End date must be greater than or equal to start date.", nameof(to));
            }
        }
    }
}
