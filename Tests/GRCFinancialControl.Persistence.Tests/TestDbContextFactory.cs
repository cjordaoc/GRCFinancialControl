using System;
using System.Threading;
using System.Threading.Tasks;
using GRCFinancialControl.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Tests;

public sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>, IAsyncDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(_options);
    }

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
