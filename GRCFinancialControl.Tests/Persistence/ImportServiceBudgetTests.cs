using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GRCFinancialControl.Tests.Persistence;

public sealed class ImportServiceBudgetTests : IAsyncDisposable
{
    private const string EngagementId = "E-TEST-001";
    private const string CustomerName = "Contoso Ltd";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly PooledDbContextFactory<ApplicationDbContext> _factory;
    private readonly ImportService _service;

    public ImportServiceBudgetTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _factory = new PooledDbContextFactory<ApplicationDbContext>(_options);

        using var context = _factory.CreateDbContext();
        context.Database.EnsureCreated();
        EnsureFiscalYear(context);

        _service = new ImportService(
            _factory,
            NullLogger<ImportService>.Instance,
            NullLoggerFactory.Instance,
            new FiscalCalendarConsistencyService(
                _factory,
                NullLogger<FiscalCalendarConsistencyService>.Instance));
    }

    [Fact]
    public async Task ImportBudgetAsync_ResolvesWorksheetWithoutResourcingName()
    {
        var filePath = CreateBudgetWorkbook("Team Plan", includeIncurredRow: false);

        try
        {
            var summary = await _service.ImportBudgetAsync(filePath);

            Assert.Contains("Budget entries inserted: 3", summary);

            await using var verifyContext = _factory.CreateDbContext();
            var engagement = await verifyContext.Engagements
                .Include(e => e.RankBudgets)
                .SingleAsync(e => e.EngagementId == EngagementId);

            Assert.Equal(3, engagement.RankBudgets.Count);
            Assert.All(engagement.RankBudgets, budget =>
                Assert.DoesNotContain("Incurred", budget.RankName, StringComparison.OrdinalIgnoreCase));

            var totalBudget = engagement.RankBudgets.Sum(b => b.BudgetHours);
            Assert.Equal(totalBudget, engagement.InitialHoursBudget);
        }
        finally
        {
            DeleteFile(filePath);
        }
    }

    [Fact]
    public async Task ImportBudgetAsync_IgnoresIncurredHoursRows()
    {
        var filePath = CreateBudgetWorkbook("RESOURCING", includeIncurredRow: true);

        try
        {
            await _service.ImportBudgetAsync(filePath);

            await using var verifyContext = _factory.CreateDbContext();
            var engagement = await verifyContext.Engagements
                .Include(e => e.RankBudgets)
                .SingleAsync(e => e.EngagementId == EngagementId);

            Assert.DoesNotContain(engagement.RankBudgets, budget =>
                budget.RankName.Contains("incurred", StringComparison.OrdinalIgnoreCase));

            var totalBudget = engagement.RankBudgets.Sum(b => b.BudgetHours);
            Assert.Equal(175m, totalBudget);
            Assert.Equal(175m, engagement.InitialHoursBudget);
        }
        finally
        {
            DeleteFile(filePath);
        }
    }

    private static void EnsureFiscalYear(ApplicationDbContext context)
    {
        if (context.FiscalYears.Any())
        {
            return;
        }

        var today = DateTime.UtcNow.Date;
        var start = new DateTime(today.Year, 1, 1);
        var end = start.AddYears(1).AddDays(-1);

        var fiscalYear = new FiscalYear
        {
            Name = $"FY{start.Year}",
            StartDate = start,
            EndDate = end,
            AreaRevenueTarget = 0m,
            AreaSalesTarget = 0m
        };

        fiscalYear.ClosingPeriods.Add(new ClosingPeriod
        {
            Name = "CP-1",
            PeriodStart = start,
            PeriodEnd = start.AddDays(6),
            FiscalYear = fiscalYear
        });

        context.FiscalYears.Add(fiscalYear);
        context.SaveChanges();
    }

    private string CreateBudgetWorkbook(string resourcingSheetName, bool includeIncurredRow)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        using var workbook = new XLWorkbook();
        var planInfo = workbook.AddWorksheet("PLAN INFO");
        planInfo.Cell("A1").Value = "Budget-Test Engagement";
        planInfo.Cell("B2").Value = DateTime.UtcNow;
        planInfo.Cell("B4").Value = CustomerName;
        planInfo.Cell("B5").Value = EngagementId;

        var resourcing = workbook.AddWorksheet(resourcingSheetName);
        resourcing.Cell("A2").Value = DateTime.UtcNow.Date;
        resourcing.Cell("C2").Value = DateTime.UtcNow.Date.AddDays(7);
        resourcing.Cell("A3").Value = "Level";
        resourcing.Cell("B3").Value = "Employee";
        resourcing.Cell("C3").Value = "H";

        resourcing.Cell("A4").Value = "01-PARTNER";
        resourcing.Cell("B4").Value = "Partner Person";
        resourcing.Cell("C4").Value = 100;

        resourcing.Cell("A5").Value = "02-MANAGER";
        resourcing.Cell("B5").Value = "Manager Person";
        resourcing.Cell("C5").Value = 50;

        var currentRow = 6;

        if (includeIncurredRow)
        {
            resourcing.Cell($"A{currentRow}").Value = "Incurred Hours";
            resourcing.Cell($"C{currentRow}").Value = 999;
            currentRow++;
        }

        resourcing.Cell($"A{currentRow}").Value = "03-SENIOR";
        resourcing.Cell($"B{currentRow}").Value = "Senior Person";
        resourcing.Cell($"C{currentRow}").Value = 25;

        workbook.SaveAs(filePath);
        return filePath;
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
