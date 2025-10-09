using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Data;

namespace GRCFinancialControl.Services
{
    public sealed class EngagementDto
    {
        public string EngagementId { get; init; } = string.Empty;
        public string EngagementTitle { get; init; } = string.Empty;
        public string? EngagementPartner { get; init; }
        public string? EngagementManager { get; init; }
        public decimal OpeningMargin { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedUtc { get; init; }
        public DateTime UpdatedUtc { get; init; }
    }

    public sealed class EngagementService
    {
        private readonly MySqlDbContext _db;

        public EngagementService(MySqlDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public List<EngagementDto> LoadAllEngagements()
        {
            return _db.DimEngagements
                .OrderBy(e => e.EngagementId)
                .Select(e => new EngagementDto
                {
                    EngagementId = e.EngagementId,
                    EngagementTitle = e.EngagementTitle,
                    EngagementPartner = e.EngagementPartner,
                    EngagementManager = e.EngagementManager,
                    OpeningMargin = e.OpeningMargin,
                    IsActive = e.IsActive,
                    CreatedUtc = e.CreatedUtc,
                    UpdatedUtc = e.UpdatedUtc
                })
                .ToList();
        }

        public DimEngagement? LoadById(string engagementId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(engagementId);
            return _db.DimEngagements.Find(engagementId.Trim());
        }

        public void Insert(DimEngagement engagement)
        {
            if (engagement == null)
            {
                throw new ArgumentNullException(nameof(engagement));
            }

            if (_db.DimEngagements.Find(engagement.EngagementId) != null)
            {
                throw new InvalidOperationException($"Engagement '{engagement.EngagementId}' already exists.");
            }

            engagement.CreatedUtc = DateTime.UtcNow;
            engagement.UpdatedUtc = DateTime.UtcNow;
            _db.DimEngagements.Add(engagement);
            _db.SaveChanges();
        }

        public void Update(DimEngagement engagement)
        {
            if (engagement == null)
            {
                throw new ArgumentNullException(nameof(engagement));
            }

            var existing = _db.DimEngagements.Find(engagement.EngagementId)
                ?? throw new InvalidOperationException($"Engagement '{engagement.EngagementId}' was not found.");

            existing.EngagementTitle = engagement.EngagementTitle;
            existing.EngagementPartner = engagement.EngagementPartner;
            existing.EngagementManager = engagement.EngagementManager;
            existing.OpeningMargin = engagement.OpeningMargin;
            existing.IsActive = engagement.IsActive;
            existing.UpdatedUtc = DateTime.UtcNow;

            _db.SaveChanges();
        }

        public void Delete(string engagementId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(engagementId);
            var existing = _db.DimEngagements.Find(engagementId.Trim());
            if (existing == null)
            {
                return;
            }

            _db.DimEngagements.Remove(existing);
            _db.SaveChanges();
        }
    }
}
