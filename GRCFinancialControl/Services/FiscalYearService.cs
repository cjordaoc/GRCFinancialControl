using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Data;

namespace GRCFinancialControl.Services
{
    public sealed class FiscalYearService
    {
        private readonly MySqlDbContext _db;

        public FiscalYearService(MySqlDbContext db)
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

        public DimFiscalYear Update(long id, string description, DateOnly from, DateOnly to)
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

        public void Delete(long id)
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

        public DimFiscalYear Activate(long id)
        {
            var fiscalYears = _db.DimFiscalYears.ToList();
            var target = fiscalYears.SingleOrDefault(f => f.FiscalYearId == id)
                ?? throw new InvalidOperationException("Fiscal year not found.");

            foreach (var fiscalYear in fiscalYears)
            {
                var shouldBeActive = fiscalYear.FiscalYearId == id;
                if (fiscalYear.IsActive != shouldBeActive)
                {
                    fiscalYear.IsActive = shouldBeActive;
                    fiscalYear.UpdatedUtc = DateTime.UtcNow;
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
