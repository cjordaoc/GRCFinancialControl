using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Tests.Persistence;

public sealed class ImportServiceConsumedHoursTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly TestDbContextFactory _factory;
    private readonly ImportService _service;

    public ImportServiceConsumedHoursTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(_options);
        context.Database.EnsureCreated();

        _factory = new TestDbContextFactory(_options);
        _service = new ImportService(
            _factory,
            NullLogger<ImportService>.Instance,
            NullLoggerFactory.Instance,
            new NoOpFiscalCalendarConsistencyService());
    }

    [Fact]
    public async Task RefreshConsumedHoursAsync_DistributesActualsAcrossRanks()
    {
        await SeedEngagementAsync();

        var updated = await _service.RefreshConsumedHoursAsync(2);

        Assert.True(updated > 0);

        await using var verifyContext = _factory.CreateDbContext();
        var budgets = await verifyContext.EngagementRankBudgets
            .Where(b => b.EngagementId == 1 && b.FiscalYearId == 1)
            .ToListAsync();

        Assert.Equal(2, budgets.Count);
        var budgetsByRank = budgets.ToDictionary(b => b.RankName);
        Assert.Equal(33.33m, budgetsByRank["Manager"].ConsumedHours);
        Assert.Equal(16.67m, budgetsByRank["Senior"].ConsumedHours);

        var lockedBudget = await verifyContext.EngagementRankBudgets
            .SingleAsync(b => b.EngagementId == 1 && b.FiscalYearId == 2);
        Assert.Equal(10m, lockedBudget.ConsumedHours);

        var updatedWithLaterPeriod = await _service.RefreshConsumedHoursAsync(3);
        Assert.True(updatedWithLaterPeriod > 0);

        await using var refreshedContext = _factory.CreateDbContext();
        var refreshedBudgets = await refreshedContext.EngagementRankBudgets
            .Where(b => b.EngagementId == 1 && b.FiscalYearId == 1)
            .ToListAsync();

        Assert.Equal(2, refreshedBudgets.Count);
        var refreshedByRank = refreshedBudgets.ToDictionary(b => b.RankName);
        Assert.Equal(40m, refreshedByRank["Manager"].ConsumedHours);
        Assert.Equal(20m, refreshedByRank["Senior"].ConsumedHours);
    }

    [Fact]
    public async Task RefreshConsumedHoursAsync_UsesExistingDistributionWhenBudgetZero()
    {
        await SeedZeroBudgetEngagementAsync();

        var updated = await _service.RefreshConsumedHoursAsync(6);

        Assert.True(updated > 0);

        await using var verifyContext = _factory.CreateDbContext();
        var budgets = await verifyContext.EngagementRankBudgets
            .Where(b => b.EngagementId == 2 && b.FiscalYearId == 3)
            .ToListAsync();

        Assert.Equal(2, budgets.Count);
        var byRank = budgets.ToDictionary(b => b.RankName);
        Assert.Equal(45m, byRank["Analyst"].ConsumedHours);
        Assert.Equal(15m, byRank["Consultant"].ConsumedHours);
    }

    private async Task SeedEngagementAsync()
    {
        await using var context = _factory.CreateDbContext();

        var openFiscalYear = new FiscalYear
        {
            Id = 1,
            Name = "FY2024",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            IsLocked = false
        };

        var lockedFiscalYear = new FiscalYear
        {
            Id = 2,
            Name = "FY2023",
            StartDate = new DateTime(2023, 1, 1),
            EndDate = new DateTime(2023, 12, 31),
            IsLocked = true
        };

        context.FiscalYears.AddRange(openFiscalYear, lockedFiscalYear);

        var closingPeriods = new[]
        {
            new ClosingPeriod { Id = 1, Name = "2024-Q1", FiscalYearId = 1, PeriodStart = new DateTime(2024, 1, 1), PeriodEnd = new DateTime(2024, 3, 31) },
            new ClosingPeriod { Id = 2, Name = "2024-Q2", FiscalYearId = 1, PeriodStart = new DateTime(2024, 4, 1), PeriodEnd = new DateTime(2024, 6, 30) },
            new ClosingPeriod { Id = 3, Name = "2024-Q3", FiscalYearId = 1, PeriodStart = new DateTime(2024, 7, 1), PeriodEnd = new DateTime(2024, 9, 30) },
            new ClosingPeriod { Id = 4, Name = "2023-Q4", FiscalYearId = 2, PeriodStart = new DateTime(2023, 10, 1), PeriodEnd = new DateTime(2023, 12, 31) }
        };

        context.ClosingPeriods.AddRange(closingPeriods);

        var engagement = new Engagement
        {
            Id = 1,
            EngagementId = "E-001",
            Description = "Sample",
            InitialHoursBudget = 200m,
            EstimatedToCompleteHours = 80m
        };

        context.Engagements.Add(engagement);

        context.EngagementRankBudgets.AddRange(
            new EngagementRankBudget
            {
                Id = 1,
                EngagementId = 1,
                FiscalYearId = 1,
                RankName = "Manager",
                BudgetHours = 100m,
                ConsumedHours = 0m,
                CreatedAtUtc = DateTime.UtcNow
            },
            new EngagementRankBudget
            {
                Id = 2,
                EngagementId = 1,
                FiscalYearId = 1,
                RankName = "Senior",
                BudgetHours = 50m,
                ConsumedHours = 0m,
                CreatedAtUtc = DateTime.UtcNow
            },
            new EngagementRankBudget
            {
                Id = 3,
                EngagementId = 1,
                FiscalYearId = 2,
                RankName = "Manager",
                BudgetHours = 80m,
                ConsumedHours = 10m,
                CreatedAtUtc = DateTime.UtcNow
            });

        context.ActualsEntries.AddRange(
            new ActualsEntry { EngagementId = 1, ClosingPeriodId = 1, Date = new DateTime(2024, 3, 31), Hours = 20m, ImportBatchId = "B1" },
            new ActualsEntry { EngagementId = 1, ClosingPeriodId = 2, Date = new DateTime(2024, 6, 30), Hours = 30m, ImportBatchId = "B2" },
            new ActualsEntry { EngagementId = 1, ClosingPeriodId = 3, Date = new DateTime(2024, 9, 30), Hours = 10m, ImportBatchId = "B3" },
            new ActualsEntry { EngagementId = 1, ClosingPeriodId = 4, Date = new DateTime(2023, 12, 31), Hours = 60m, ImportBatchId = "B4" });

        await context.SaveChangesAsync();
    }

    private async Task SeedZeroBudgetEngagementAsync()
    {
        await using var context = _factory.CreateDbContext();

        var fiscalYear = new FiscalYear
        {
            Id = 3,
            Name = "FY2025",
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 12, 31),
            IsLocked = false
        };

        context.FiscalYears.Add(fiscalYear);

        var closingPeriods = new[]
        {
            new ClosingPeriod { Id = 5, Name = "2025-Q1", FiscalYearId = 3, PeriodStart = new DateTime(2025, 1, 1), PeriodEnd = new DateTime(2025, 3, 31) },
            new ClosingPeriod { Id = 6, Name = "2025-Q2", FiscalYearId = 3, PeriodStart = new DateTime(2025, 4, 1), PeriodEnd = new DateTime(2025, 6, 30) }
        };

        context.ClosingPeriods.AddRange(closingPeriods);

        var engagement = new Engagement
        {
            Id = 2,
            EngagementId = "E-002",
            Description = "Zero Budget",
            InitialHoursBudget = 0m,
            EstimatedToCompleteHours = 0m
        };

        context.Engagements.Add(engagement);

        context.EngagementRankBudgets.AddRange(
            new EngagementRankBudget
            {
                Id = 4,
                EngagementId = 2,
                FiscalYearId = 3,
                RankName = "Consultant",
                BudgetHours = 0m,
                ConsumedHours = 10m,
                CreatedAtUtc = DateTime.UtcNow
            },
            new EngagementRankBudget
            {
                Id = 5,
                EngagementId = 2,
                FiscalYearId = 3,
                RankName = "Analyst",
                BudgetHours = 0m,
                ConsumedHours = 30m,
                CreatedAtUtc = DateTime.UtcNow
            });

        context.ActualsEntries.AddRange(
            new ActualsEntry { EngagementId = 2, ClosingPeriodId = 5, Date = new DateTime(2025, 3, 31), Hours = 20m, ImportBatchId = "C1" },
            new ActualsEntry { EngagementId = 2, ClosingPeriodId = 6, Date = new DateTime(2025, 6, 30), Hours = 40m, ImportBatchId = "C2" });

        await context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public ApplicationDbContext CreateDbContext()
        {
            return new ApplicationDbContext(_options);
        }

        public ValueTask<ApplicationDbContext> CreateDbContextAsync()
        {
            return ValueTask.FromResult(new ApplicationDbContext(_options));
        }
    }

    private sealed class NoOpFiscalCalendarConsistencyService : IFiscalCalendarConsistencyService
    {
        public Task<FiscalCalendarValidationSummary> EnsureConsistencyAsync()
        {
            return Task.FromResult(new FiscalCalendarValidationSummary(
                FiscalYearsProcessed: 0,
                ClosingPeriodsProcessed: 0,
                CorrectionsApplied: 0,
                IssuesBefore: Array.Empty<FiscalYearValidationReport>(),
                IssuesAfter: Array.Empty<FiscalYearValidationReport>(),
                CorrectionsLog: Array.Empty<string>()));
        }
    }
}
