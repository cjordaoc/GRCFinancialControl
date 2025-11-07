using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Infrastructure;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Manages rank mappings for raw-to-normalized rank translations.
    /// </summary>
    public sealed class RankMappingService : ContextFactoryCrudService<RankMapping>, IRankMappingService
    {
        public RankMappingService(IDbContextFactory<ApplicationDbContext> contextFactory)
            : base(contextFactory)
        {
        }

        protected override DbSet<RankMapping> Set(ApplicationDbContext context) => context.RankMappings;

        public async Task<IReadOnlyList<RankMapping>> GetAllAsync()
        {
            return await GetAllInternalAsync(static query => query
                    .AsNoTracking()
                    .OrderBy(mapping => string.IsNullOrWhiteSpace(mapping.NormalizedRank) ? mapping.RawRank : mapping.NormalizedRank)
                    .ThenBy(mapping => mapping.RawRank))
                .ConfigureAwait(false);
        }

        public Task AddAsync(RankMapping rankMapping)
        {
            Normalize(rankMapping);
            return AddEntityAsync(rankMapping);
        }

        public Task UpdateAsync(RankMapping rankMapping)
        {
            Normalize(rankMapping);
            return UpdateEntityAsync(rankMapping);
        }

        public Task DeleteAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Identifier must be positive.");
            }

            return DeleteEntityAsync(id);
        }

        private static void Normalize(RankMapping rankMapping)
        {
            ArgumentNullException.ThrowIfNull(rankMapping);

            rankMapping.RawRank = (rankMapping.RawRank ?? string.Empty).Trim();
            rankMapping.NormalizedRank = (rankMapping.NormalizedRank ?? string.Empty).Trim();
            rankMapping.SpreadsheetRank = (rankMapping.SpreadsheetRank ?? string.Empty).Trim();
        }
    }
}
