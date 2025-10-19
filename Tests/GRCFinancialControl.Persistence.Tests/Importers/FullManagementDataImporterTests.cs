using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Tests;
using GRCFinancialControl.Persistence.Services.Importers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GRCFinancialControl.Persistence.Tests.Importers;

public sealed class FullManagementDataImporterTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly TestDbContextFactory _factory;

    public FullManagementDataImporterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(_options);
        context.Database.EnsureCreated();

        _factory = new TestDbContextFactory(_options);
    }

    public async Task InitializeAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();

        var fiscalYear = new FiscalYear
        {
            Name = "FY24",
            StartDate = new DateTime(2023, 7, 1),
            EndDate = new DateTime(2024, 6, 30)
        };

        context.FiscalYears.Add(fiscalYear);
        await context.SaveChangesAsync();

        context.ClosingPeriods.Add(new ClosingPeriod
        {
            Name = "FY24-P02",
            FiscalYearId = fiscalYear.Id,
            PeriodStart = new DateTime(2023, 8, 1),
            PeriodEnd = new DateTime(2023, 8, 31)
        });

        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task ImportAsync_CreatesEngagementAndFinancialEvolutionEntries()
    {
        var importer = new FullManagementDataImporter(_factory, NullLogger<FullManagementDataImporter>.Instance);

        var workbookPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        CreateWorkbook(workbookPath);

        try
        {
            var result = await importer.ImportAsync(workbookPath);

            Assert.Contains("Distinct engagements touched: 1", result.Summary);

            await using var verificationContext = await _factory.CreateDbContextAsync();
            var engagement = await verificationContext.Engagements
                .Include(e => e.FinancialEvolutions)
                .SingleAsync();

            Assert.Equal("E-001", engagement.EngagementId);
            Assert.Equal("Sample engagement", engagement.Description);
            Assert.Equal(10m, engagement.InitialHoursBudget);
            Assert.Equal(20m, engagement.ValueEtcp);

            Assert.Equal(2, engagement.FinancialEvolutions.Count);
            Assert.Contains(engagement.FinancialEvolutions, fe => fe.ClosingPeriodId == "INITIAL");
            Assert.Contains(engagement.FinancialEvolutions, fe => fe.ClosingPeriodId == "FY24-P02");
        }
        finally
        {
            if (File.Exists(workbookPath))
            {
                File.Delete(workbookPath);
            }
        }
    }

    private static void CreateWorkbook(string workbookPath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Sheet1");

        worksheet.Cell(1, 1).Value = "Engagement ID";
        worksheet.Cell(1, 2).Value = "Engagement Name";
        worksheet.Cell(1, 3).Value = "Closing Period";
        worksheet.Cell(1, 4).Value = "Budget Hours";
        worksheet.Cell(1, 5).Value = "Budget Value";
        worksheet.Cell(1, 6).Value = "Margin % Bud";
        worksheet.Cell(1, 7).Value = "Expenses Bud";
        worksheet.Cell(1, 8).Value = "ETC Hours";
        worksheet.Cell(1, 9).Value = "ETC Value";
        worksheet.Cell(1, 10).Value = "Margin % ETC-P";
        worksheet.Cell(1, 11).Value = "Expenses ETC-P";
        worksheet.Cell(1, 12).Value = "Status";
        worksheet.Cell(1, 13).Value = "Next ETC Date";

        worksheet.Cell(2, 1).Value = "E-001";
        worksheet.Cell(2, 2).Value = "Sample engagement";
        worksheet.Cell(2, 3).Value = "FY24-P02";
        worksheet.Cell(2, 4).Value = 10m;
        worksheet.Cell(2, 5).Value = 1000m;
        worksheet.Cell(2, 6).Value = 0.15m;
        worksheet.Cell(2, 7).Value = 200m;
        worksheet.Cell(2, 8).Value = 12m;
        worksheet.Cell(2, 9).Value = 20m;
        worksheet.Cell(2, 10).Value = 0.25m;
        worksheet.Cell(2, 11).Value = 220m;
        worksheet.Cell(2, 12).Value = "Active";
        worksheet.Cell(2, 13).Value = new DateTime(2023, 9, 30);

        workbook.SaveAs(workbookPath);
    }

}
