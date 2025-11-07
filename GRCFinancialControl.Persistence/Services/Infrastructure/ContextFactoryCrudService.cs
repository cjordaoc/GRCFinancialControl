using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services.Infrastructure
{
    /// <summary>
    /// Base service providing CRUD operations with DbContext factory pattern.
    /// </summary>
    public abstract class ContextFactoryCrudService<TEntity>
        where TEntity : class
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        protected ContextFactoryCrudService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);

            _contextFactory = contextFactory;
        }

        protected async Task<ApplicationDbContext> CreateContextAsync()
        {
            var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(context);

            return context;
        }

        protected abstract DbSet<TEntity> Set(ApplicationDbContext context);

        protected virtual IQueryable<TEntity> BuildQuery(ApplicationDbContext context) => Set(context);

        protected async Task<List<TEntity>> GetAllInternalAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>>? configure = null)
        {
            await using var context = await CreateContextAsync();
            IQueryable<TEntity> query = BuildQuery(context);
            if (configure is not null)
            {
                query = configure(query);
            }

            return await query.ToListAsync().ConfigureAwait(false);
        }

        protected async Task<TEntity?> GetSingleInternalAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>> configure)
        {
            await using var context = await CreateContextAsync();
            IQueryable<TEntity> query = BuildQuery(context);
            query = configure(query);
            return await query.FirstOrDefaultAsync().ConfigureAwait(false);
        }

        protected async Task AddEntityAsync(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            await using var context = await CreateContextAsync().ConfigureAwait(false);
            await Set(context).AddAsync(entity).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        protected async Task UpdateEntityAsync(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            await using var context = await CreateContextAsync().ConfigureAwait(false);
            Set(context).Update(entity);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        protected async Task<TEntity?> FindEntityAsync(params object[] keyValues)
        {
            ValidateKeyValues(keyValues);

            await using var context = await CreateContextAsync().ConfigureAwait(false);
            return await Set(context).FindAsync(keyValues).ConfigureAwait(false);
        }

        protected async Task DeleteEntityAsync(params object[] keyValues)
        {
            ValidateKeyValues(keyValues);

            await using var context = await CreateContextAsync().ConfigureAwait(false);
            var entity = await Set(context).FindAsync(keyValues).ConfigureAwait(false);
            if (entity is null)
            {
                return;
            }

            Set(context).Remove(entity);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        private static void ValidateKeyValues(object[] keyValues)
        {
            ArgumentNullException.ThrowIfNull(keyValues);

            if (keyValues.Length == 0)
            {
                throw new ArgumentException("At least one key value must be provided.", nameof(keyValues));
            }

            if (keyValues.Any(value => value is null))
            {
                throw new ArgumentException("Key values must not contain null entries.", nameof(keyValues));
            }
        }
    }
}
