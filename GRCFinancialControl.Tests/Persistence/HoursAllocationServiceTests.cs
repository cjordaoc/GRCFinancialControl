using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Tests.Persistence;

public sealed class HoursAllocationServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly TestDbContextFactory _factory;

    public HoursAllocationServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(_options);
        context.Database.EnsureCreated();

        _factory = new TestDbContextFactory(_options);
    }

    [Fact]
    public async Task GetAllocationAsync_ReturnsSnapshotWithExpectedValues()
    {
        var seed = await SeedSampleDataAsync();
        var service = new HoursAllocationService(_factory);

        var snapshot = await service.GetAllocationAsync(seed.EngagementId);

        Assert.Equal(seed.EngagementId, snapshot.EngagementId);
        Assert.Equal("E-100", snapshot.EngagementCode);
        Assert.Equal("Test Engagement", snapshot.EngagementName);
        Assert.Equal(120m, snapshot.TotalBudgetHours);
        Assert.Equal(80m, snapshot.ActualHours);
        Assert.Equal(70m, snapshot.ToBeConsumedHours);
        Assert.Equal(2, snapshot.FiscalYears.Count);
        Assert.Equal("FY2024", snapshot.FiscalYears[0].Name);
        Assert.False(snapshot.FiscalYears[0].IsLocked);
        Assert.True(snapshot.FiscalYears[1].IsLocked);

        Assert.Single(snapshot.Rows);
        var row = snapshot.Rows[0];
        Assert.Equal("Manager", row.RankName);
        Assert.Equal(2, row.Cells.Count);

        var openCell = row.Cells.Single(c => c.FiscalYearId == seed.OpenFiscalYearId);
        Assert.Equal(seed.OpenBudgetId, openCell.BudgetId);
        Assert.Equal(70m, openCell.BudgetHours);
        Assert.Equal(10m, openCell.ConsumedHours);
        Assert.Equal(60m, openCell.RemainingHours);
        Assert.False(openCell.IsLocked);

        var lockedCell = row.Cells.Single(c => c.FiscalYearId == seed.LockedFiscalYearId);
        Assert.Equal(seed.LockedBudgetId, lockedCell.BudgetId);
        Assert.Equal(50m, lockedCell.BudgetHours);
        Assert.Equal(30m, lockedCell.ConsumedHours);
        Assert.Equal(20m, lockedCell.RemainingHours);
        Assert.True(lockedCell.IsLocked);
    }

    [Fact]
    public async Task SaveAsync_UpdatesConsumedHoursForOpenFiscalYear()
    {
        var seed = await SeedSampleDataAsync();
        var service = new HoursAllocationService(_factory);

        var updates = new List<HoursAllocationCellUpdate>
        {
            new(seed.OpenBudgetId, 25m)
        };

        var snapshot = await service.SaveAsync(seed.EngagementId, updates);

        var managerRow = snapshot.Rows.Single(r => r.RankName == "Manager");
        var updatedCell = managerRow.Cells.Single(c => c.BudgetId == seed.OpenBudgetId);

        Assert.Equal(25m, updatedCell.ConsumedHours);
        Assert.Equal(45m, updatedCell.RemainingHours);
        Assert.Equal(55m, snapshot.ToBeConsumedHours);

        await using var verifyContext = _factory.CreateDbContext();
        var budget = await verifyContext.EngagementRankBudgets.FindAsync(seed.OpenBudgetId);
        Assert.NotNull(budget);
        Assert.Equal(25m, budget!.ConsumedHours);
        Assert.Equal(45m, budget.BudgetHours - budget.ConsumedHours);
    }

    [Fact]
    public async Task SaveAsync_ThrowsWhenFiscalYearLocked()
    {
        var seed = await SeedSampleDataAsync();
        var service = new HoursAllocationService(_factory);

        var updates = new List<HoursAllocationCellUpdate>
        {
            new(seed.LockedBudgetId, 5m)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(seed.EngagementId, updates));
    }

    [Fact]
    public async Task AddRankAsync_CreatesEntriesForOpenFiscalYears()
    {
        var seed = await SeedSampleDataAsync();
        var service = new HoursAllocationService(_factory);

        var snapshot = await service.AddRankAsync(seed.EngagementId, "Senior");

        var newRow = snapshot.Rows.Single(r => r.RankName == "Senior");
        Assert.Equal(snapshot.FiscalYears.Count, newRow.Cells.Count);

        var openCell = newRow.Cells.Single(c => c.FiscalYearId == seed.OpenFiscalYearId);
        Assert.NotNull(openCell.BudgetId);
        Assert.Equal(0m, openCell.BudgetHours);
        Assert.False(openCell.IsLocked);

        var lockedCell = newRow.Cells.Single(c => c.FiscalYearId == seed.LockedFiscalYearId);
        Assert.Null(lockedCell.BudgetId);
        Assert.True(lockedCell.IsLocked);
        Assert.Equal(0m, lockedCell.BudgetHours);
        Assert.Equal(0m, lockedCell.ConsumedHours);
    }

    [Fact]
    public async Task DeleteRankAsync_RemovesRankWhenValuesAreZero()
    {
        var seed = await SeedSampleDataAsync(includeEmptyRank: true);
        var service = new HoursAllocationService(_factory);

        await service.DeleteRankAsync(seed.EngagementId, "Associate");

        await using var verifyContext = _factory.CreateDbContext();
        var remainingRanks = await verifyContext.EngagementRankBudgets
            .Where(b => b.EngagementId == seed.EngagementId && b.RankName == "Associate")
            .ToListAsync();

        Assert.Empty(remainingRanks);
    }

    private async Task<SeedData> SeedSampleDataAsync(bool includeEmptyRank = false)
    {
        await using var context = _factory.CreateDbContext();

        context.FiscalYears.RemoveRange(context.FiscalYears);
        context.EngagementRankBudgets.RemoveRange(context.EngagementRankBudgets);
        context.Engagements.RemoveRange(context.Engagements);
        await context.SaveChangesAsync();

        var openFiscalYear = new FiscalYear
        {
            Name = "FY2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            IsLocked = false
        };
        var lockedFiscalYear = new FiscalYear
        {
            Name = "FY2023",
            StartDate = new DateTime(2023, 1, 1),
            EndDate = new DateTime(2023, 12, 31),
            IsLocked = true
        };

        context.FiscalYears.AddRange(openFiscalYear, lockedFiscalYear);
        await context.SaveChangesAsync();

        var engagement = new Engagement
        {
            EngagementId = "E-100",
            Description = "Test Engagement",
            InitialHoursBudget = 120m,
            EstimatedToCompleteHours = 80m
        };

        context.Engagements.Add(engagement);
        await context.SaveChangesAsync();

        var openBudget = new EngagementRankBudget
        {
            EngagementId = engagement.Id,
            FiscalYearId = openFiscalYear.Id,
            RankName = "Manager",
            BudgetHours = 70m,
            ConsumedHours = 10m,
            RemainingHours = 60m,
            CreatedAtUtc = DateTime.UtcNow
        };

        var lockedBudget = new EngagementRankBudget
        {
            EngagementId = engagement.Id,
            FiscalYearId = lockedFiscalYear.Id,
            RankName = "Manager",
            BudgetHours = 50m,
            ConsumedHours = 30m,
            RemainingHours = 20m,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.EngagementRankBudgets.AddRange(openBudget, lockedBudget);

        if (includeEmptyRank)
        {
            context.EngagementRankBudgets.Add(new EngagementRankBudget
            {
                EngagementId = engagement.Id,
                FiscalYearId = openFiscalYear.Id,
                RankName = "Associate",
                BudgetHours = 0m,
                ConsumedHours = 0m,
                RemainingHours = 0m,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();

        return new SeedData(
            engagement.Id,
            openFiscalYear.Id,
            lockedFiscalYear.Id,
            openBudget.Id,
            lockedBudget.Id);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private sealed record SeedData(
        int EngagementId,
        int OpenFiscalYearId,
        int LockedFiscalYearId,
        long OpenBudgetId,
        long LockedBudgetId);

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public ApplicationDbContext CreateDbContext()
            => new(_options);

        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}
