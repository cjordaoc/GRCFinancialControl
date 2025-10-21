using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GRCFinancialControl.Tests.Persistence
{
    public sealed class StaffAllocationForecastServiceTests
    {
        [Fact]
        public async Task SaveEngagementForecastAsync_aggregatesEntriesAndUpdatesBudgets()
        {
            await using var fixture = await TestDatabaseFixture.CreateAsync();

            var now = DateTime.UtcNow;
            int engagementId;

            using (var context = fixture.CreateContext())
            {
                context.FiscalYears.AddRange(
                    new FiscalYear
                    {
                        Id = 1,
                        Name = "FY24",
                        StartDate = new DateTime(2023, 7, 1),
                        EndDate = new DateTime(2024, 6, 30)
                    },
                    new FiscalYear
                    {
                        Id = 2,
                        Name = "FY25",
                        StartDate = new DateTime(2024, 7, 1),
                        EndDate = new DateTime(2025, 6, 30)
                    });

                var engagement = new Engagement
                {
                    EngagementId = "ENG-001",
                    Description = "Test engagement",
                    InitialHoursBudget = 200m,
                    RankBudgets =
                    {
                        new EngagementRankBudget { RankName = "SENIOR", Hours = 80m, ForecastHours = 12m, CreatedAtUtc = now },
                        new EngagementRankBudget { RankName = "Manager", Hours = 60m, ForecastHours = 5m, CreatedAtUtc = now },
                        new EngagementRankBudget { RankName = "Analyst", Hours = 40m, ForecastHours = 20m, CreatedAtUtc = now }
                    }
                };

                context.Engagements.Add(engagement);
                await context.SaveChangesAsync();

                engagementId = engagement.Id;

                context.StaffAllocationForecasts.AddRange(
                    new StaffAllocationForecast
                    {
                        EngagementId = engagementId,
                        FiscalYearId = 1,
                        RankName = "SENIOR",
                        ForecastHours = 3m,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    },
                    new StaffAllocationForecast
                    {
                        EngagementId = engagementId,
                        FiscalYearId = 2,
                        RankName = "Analyst",
                        ForecastHours = 10m,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });

                await context.SaveChangesAsync();
            }

            var entries = new List<EngagementForecastUpdateEntry>
            {
                new(1, "Senior", 10m),
                new(1, " SENIOR ", 5.12m),
                new(2, "manager", 8.5m)
            };

            await fixture.Service.SaveEngagementForecastAsync(engagementId, entries);

            using (var verificationContext = fixture.CreateContext())
            {
                var forecasts = await verificationContext.StaffAllocationForecasts
                    .Where(f => f.EngagementId == engagementId)
                    .OrderBy(f => f.FiscalYearId)
                    .ThenBy(f => f.RankName)
                    .ToListAsync();

                Assert.Collection(
                    forecasts,
                    forecast =>
                    {
                        Assert.Equal(1, forecast.FiscalYearId);
                        Assert.Equal("SENIOR", forecast.RankName);
                        Assert.Equal(15.12m, forecast.ForecastHours);
                    },
                    forecast =>
                    {
                        Assert.Equal(2, forecast.FiscalYearId);
                        Assert.Equal("Manager", forecast.RankName);
                        Assert.Equal(8.5m, forecast.ForecastHours);
                    });

                var budgets = await verificationContext.EngagementRankBudgets
                    .Where(b => b.EngagementId == engagementId)
                    .OrderBy(b => b.RankName)
                    .ToListAsync();

                var seniorBudget = Assert.Single(budgets.Where(b => string.Equals(b.RankName, "SENIOR", StringComparison.Ordinal)));
                Assert.Equal(15.12m, seniorBudget.ForecastHours);

                var managerBudget = Assert.Single(budgets.Where(b => string.Equals(b.RankName, "Manager", StringComparison.Ordinal)));
                Assert.Equal(8.5m, managerBudget.ForecastHours);

                var analystBudget = Assert.Single(budgets.Where(b => string.Equals(b.RankName, "Analyst", StringComparison.Ordinal)));
                Assert.Equal(0m, analystBudget.ForecastHours);
            }
        }

        private sealed class TestDatabaseFixture : IAsyncDisposable
        {
            private readonly SqliteConnection _connection;
            private readonly TestDbContextFactory _factory;

            private TestDatabaseFixture(SqliteConnection connection, TestDbContextFactory factory)
            {
                _connection = connection;
                _factory = factory;
                Service = new StaffAllocationForecastService(_factory, NullLogger<StaffAllocationForecastService>.Instance);
            }

            public StaffAllocationForecastService Service { get; }

            public static async Task<TestDatabaseFixture> CreateAsync()
            {
                var connection = new SqliteConnection("DataSource=:memory:;Mode=Memory;Cache=Shared");
                await connection.OpenAsync();

                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connection)
                    .Options;

                var factory = new TestDbContextFactory(options);

                using (var context = factory.CreateDbContext())
                {
                    await context.Database.EnsureCreatedAsync();
                }

                return new TestDatabaseFixture(connection, factory);
            }

            public ApplicationDbContext CreateContext() => _factory.CreateDbContext();

            public ValueTask DisposeAsync() => _connection.DisposeAsync();
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

            public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            {
                return ValueTask.FromResult(new ApplicationDbContext(_options));
            }
        }
    }
}
