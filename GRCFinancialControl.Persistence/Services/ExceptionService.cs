using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Tracks and retrieves application exception entries for diagnostics.
    /// </summary>
    public class ExceptionService : IExceptionService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ExceptionService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);

            _contextFactory = contextFactory;
        }

        public async Task<List<ExceptionEntry>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(context);

            return await context.Exceptions
                .AsNoTracking()
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync().ConfigureAwait(false);
        }
    }
}

