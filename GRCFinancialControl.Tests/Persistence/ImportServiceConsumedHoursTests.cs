using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services;

namespace GRCFinancialControl.Tests.Persistence;

public sealed class ImportServiceConsumedHoursTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly PooledDbContextFactory<ApplicationDbContext> _factory;
    private readonly ImportService _service;

    public ImportServiceConsumedHoursTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _factory = new PooledDbContextFactory<ApplicationDbContext>(_options);

        using var context = _factory.CreateDbContext();
        context.Database.EnsureCreated();

        _service = new ImportService(
            _factory,
            NullLogger<ImportService>.Instance,
            NullLoggerFactory.Instance,
            new FiscalCalendarConsistencyService(
                _factory,
                NullLogger<FiscalCalendarConsistencyService>.Instance));
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

    [Fact]
    public async Task UpdateStaffAllocationsAsync_UpdatesConsumedHoursForActivePeriod()
    {
        await SeedStaffAllocationScenarioAsync();

        var monday = GetReferenceMonday();
        var filePath = CreateStaffAllocationWorkbook(monday, new StaffAllocationRow("12345", "John Doe", "Manager", "E-001"));

        try
        {
            var summary = await _service.UpdateStaffAllocationsAsync(filePath, closingPeriodId: 1);

            Assert.Contains("Imported CP Active (1)", summary);
            Assert.Contains("1 engagement-rank combinations", summary);

            await using var verifyContext = _factory.CreateDbContext();
            var budget = await verifyContext.EngagementRankBudgets
                .SingleAsync(b => b.EngagementId == 1 && b.FiscalYearId == 1 && b.RankName == "Manager");

            Assert.Equal(40m, budget.IncurredHours);
            Assert.Equal(60m, budget.RemainingHours);

            var history = await verifyContext.EngagementRankBudgetHistory.SingleAsync();
            Assert.Equal(40m, history.Hours);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task UpdateStaffAllocationsAsync_ClearsBudgetsNotInWorkbook()
    {
        await SeedStaffAllocationScenarioAsync(includeSecondaryEngagement: true);

        var monday = GetReferenceMonday();
        var initialFilePath = CreateStaffAllocationWorkbook(
            monday,
            new StaffAllocationRow("12345", "John Doe", "Manager", "E-001"),
            new StaffAllocationRow("54321", "Jane Doe", "Senior", "E-999"));

        var updateFilePath = CreateStaffAllocationWorkbook(
            monday,
            new StaffAllocationRow("12345", "John Doe", "Manager", "E-001"));

        try
        {
            await _service.UpdateStaffAllocationsAsync(initialFilePath, closingPeriodId: 1);

            var summary = await _service.UpdateStaffAllocationsAsync(updateFilePath, closingPeriodId: 1);

            Assert.Contains("Imported CP Active (1)", summary);

            await using var verifyContext = _factory.CreateDbContext();
            var clearedBudget = await verifyContext.EngagementRankBudgets
                .Include(b => b.Engagement)
                .SingleAsync(b => b.Engagement != null && b.Engagement.EngagementId == "E-999");

            Assert.Equal(0m, clearedBudget.IncurredHours);
            Assert.Equal(clearedBudget.BudgetHours, clearedBudget.RemainingHours);

            var histories = await verifyContext.EngagementRankBudgetHistory
                .Where(h => h.EngagementCode == "E-999")
                .ToListAsync();
            Assert.Empty(histories);
        }
        finally
        {
            File.Delete(initialFilePath);
            File.Delete(updateFilePath);
        }
    }

    [Fact]
    public async Task UpdateStaffAllocationsAsync_MapsSpreadsheetRankNames()
    {
        await SeedStaffAllocationScenarioAsync(includeRankMapping: false);

        await using (var context = _factory.CreateDbContext())
        {
            var budget = await context.EngagementRankBudgets
                .SingleAsync(b => b.EngagementId == 1 && b.FiscalYearId == 1);

            budget.RankName = "11-SENIOR 3";

            context.RankMappings.Add(new RankMapping
            {
                RawRank = "11-SENIOR 3",
                NormalizedRank = "Senior",
                SpreadsheetRank = "Senior 3",
                IsActive = true
            });

            await context.SaveChangesAsync();
        }

        var monday = GetReferenceMonday();
        var filePath = CreateStaffAllocationWorkbook(monday, new StaffAllocationRow("12345", "John Doe", "Senior 3", "E-001"));

        try
        {
            var summary = await _service.UpdateStaffAllocationsAsync(filePath, closingPeriodId: 1);

            Assert.Contains("Imported CP Active (1)", summary);

            await using var verifyContext = _factory.CreateDbContext();
            var budget = await verifyContext.EngagementRankBudgets
                .SingleAsync(b => b.EngagementId == 1 && b.FiscalYearId == 1 && b.RankName == "11-SENIOR 3");

            Assert.Equal(40m, budget.IncurredHours);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task UpdateStaffAllocationsAsync_SkipsWhenRankMappingMissing()
    {
        await SeedStaffAllocationScenarioAsync(includeRankMapping: false);

        var monday = GetReferenceMonday();
        var filePath = CreateStaffAllocationWorkbook(monday, new StaffAllocationRow("12345", "John Doe", "Unknown", "E-001"));

        try
        {
            var summary = await _service.UpdateStaffAllocationsAsync(filePath, closingPeriodId: 1);

            Assert.Contains("0 engagement-rank combinations", summary);

            await using var verifyContext = _factory.CreateDbContext();
            var budget = await verifyContext.EngagementRankBudgets
                .SingleAsync(b => b.EngagementId == 1 && b.FiscalYearId == 1 && b.RankName == "Manager");

            Assert.Equal(0m, budget.IncurredHours);

            var histories = await verifyContext.EngagementRankBudgetHistory.ToListAsync();
            Assert.Empty(histories);
        }
        finally
        {
            File.Delete(filePath);
        }
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

    private static DateTime GetReferenceMonday()
    {
        var today = DateTime.UtcNow.Date;
        var offset = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return today.AddDays(-offset);
    }

    private static string CreateStaffAllocationWorkbook(DateTime weekDate, params StaffAllocationRow[] rows)
    {
        var path = Path.Combine(Path.GetTempPath(), $"staff_alloc_{Guid.NewGuid():N}.xlsx");

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Alocações_Staff");
        worksheet.Cell(1, 1).Value = "GPN";
        worksheet.Cell(1, 2).Value = "% UTILIZACAO FYTD";
        worksheet.Cell(1, 3).Value = "Rank";
        worksheet.Cell(1, 4).Value = "Recursos";
        worksheet.Cell(1, 5).Value = "Escritorio";
        worksheet.Cell(1, 6).Value = "Subdominio";
        worksheet.Cell(1, 7).Value = weekDate;

        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var rowIndex = index + 2;
            worksheet.Cell(rowIndex, 1).Value = row.Gpn;
            worksheet.Cell(rowIndex, 2).Value = 0;
            worksheet.Cell(rowIndex, 3).Value = row.Rank;
            worksheet.Cell(rowIndex, 4).Value = row.EmployeeName;
            worksheet.Cell(rowIndex, 5).Value = row.Office;
            worksheet.Cell(rowIndex, 6).Value = row.Subdomain;
            worksheet.Cell(rowIndex, 7).Value = row.EngagementCode;
        }

        workbook.SaveAs(path);
        return path;
    }

    private async Task SeedStaffAllocationScenarioAsync(bool includeRankMapping = true, bool includeSecondaryEngagement = false)
    {
        await using var context = _factory.CreateDbContext();

        context.EngagementRankBudgets.RemoveRange(context.EngagementRankBudgets);
        context.Engagements.RemoveRange(context.Engagements);
        context.RankMappings.RemoveRange(context.RankMappings);
        context.Employees.RemoveRange(context.Employees);
        context.ClosingPeriods.RemoveRange(context.ClosingPeriods);
        context.FiscalYears.RemoveRange(context.FiscalYears);
        context.Exceptions.RemoveRange(context.Exceptions);
        await context.SaveChangesAsync();

        var monday = GetReferenceMonday();
        var fiscalYear = new FiscalYear
        {
            Id = 1,
            Name = $"FY{monday.Year}",
            StartDate = new DateTime(monday.Year, 1, 1),
            EndDate = new DateTime(monday.Year, 12, 31),
            IsLocked = false
        };

        context.FiscalYears.Add(fiscalYear);
        await context.SaveChangesAsync();

        var closingPeriod = new ClosingPeriod
        {
            Id = 1,
            Name = "Active",
            FiscalYearId = fiscalYear.Id,
            PeriodStart = monday,
            PeriodEnd = monday.AddDays(7).AddMinutes(-1)
        };

        context.ClosingPeriods.Add(closingPeriod);

        context.Employees.Add(new Employee
        {
            Gpn = "12345",
            EmployeeName = "John Doe",
            StartDate = monday.AddYears(-1)
        });

        var engagement = new Engagement
        {
            Id = 1,
            EngagementId = "E-001",
            Description = "Test Engagement",
            InitialHoursBudget = 100m,
            EstimatedToCompleteHours = 80m
        };

        context.Engagements.Add(engagement);
        await context.SaveChangesAsync();

        if (includeSecondaryEngagement)
        {
            var secondaryEngagement = new Engagement
            {
                EngagementId = "E-999",
                Description = "Secondary Engagement",
                InitialHoursBudget = 80m,
                EstimatedToCompleteHours = 40m
            };

            context.Engagements.Add(secondaryEngagement);
            await context.SaveChangesAsync();

            context.EngagementRankBudgets.Add(new EngagementRankBudget
            {
                EngagementId = secondaryEngagement.Id,
                FiscalYearId = fiscalYear.Id,
                RankName = "Senior",
                BudgetHours = 80m,
                ConsumedHours = 40m,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        if (includeRankMapping)
        {
            context.RankMappings.Add(new RankMapping
            {
                RawRank = "Manager",
                NormalizedRank = "Manager",
                SpreadsheetRank = "Manager",
                IsActive = true
            });

            context.RankMappings.Add(new RankMapping
            {
                RawRank = "Senior",
                NormalizedRank = "Senior",
                SpreadsheetRank = "Senior",
                IsActive = true
            });
        }

        context.EngagementRankBudgets.Add(new EngagementRankBudget
        {
            EngagementId = engagement.Id,
            FiscalYearId = fiscalYear.Id,
            RankName = "Manager",
            BudgetHours = 100m,
            ConsumedHours = 0m,
            CreatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private readonly record struct StaffAllocationRow(
        string Gpn,
        string EmployeeName,
        string Rank,
        string EngagementCode,
        string Office = "Office",
        string Subdomain = "Domain");

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
