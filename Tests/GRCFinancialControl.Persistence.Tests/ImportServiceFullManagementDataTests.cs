using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GRCFinancialControl.Persistence.Tests;

public sealed class ImportServiceFullManagementDataTests : IAsyncLifetime
{
    private const string EngagementCode = "E-67004338";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly TestDbContextFactory _factory;
    private readonly ImportService _importService;

    public ImportServiceFullManagementDataTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(_options);
        context.Database.EnsureCreated();

        _factory = new TestDbContextFactory(_options);
        _importService = new ImportService(_factory, NullLogger<ImportService>.Instance, NullLoggerFactory.Instance);
    }

    public async Task InitializeAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();

        var fiscalYears = new[]
        {
            new FiscalYear
            {
                Name = "FY26",
                StartDate = new DateTime(2025, 7, 1),
                EndDate = new DateTime(2026, 6, 30)
            },
            new FiscalYear
            {
                Name = "FY27",
                StartDate = new DateTime(2026, 7, 1),
                EndDate = new DateTime(2027, 6, 30)
            }
        };

        context.FiscalYears.AddRange(fiscalYears);

        context.Engagements.Add(new Engagement
        {
            EngagementId = EngagementCode,
            Description = "Sample engagement",
            Source = EngagementSource.GrcProject
        });

        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task ImportFullManagementDataAsync_UpsertsRevenueAllocations()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        CreateWorkbook(workbookPath);

        try
        {
            var summary = await _importService.ImportFullManagementDataAsync(workbookPath);

            Assert.Equal(
                "FYc=FY26, FYn=FY27, LastUpdateDate=2025-09-29\nUpserts=2\nSkippedMissingEngagement=0\nSkippedLockedFY=0\nSkippedInvalidNumbers=0",
                summary);

            await using var verificationContext = await _factory.CreateDbContextAsync();
            var allocations = await verificationContext.EngagementFiscalYearRevenueAllocations
                .Include(a => a.FiscalYear)
                .Where(a => a.Engagement.EngagementId == EngagementCode)
                .ToListAsync();

            Assert.Equal(2, allocations.Count);

            var currentAllocation = allocations.Single(a => a.FiscalYear.Name == "FY26");
            Assert.Equal(1_758_865.98m, currentAllocation.ToGoValue);
            Assert.Equal(1_530_458.77m, currentAllocation.ToDateValue);
            Assert.Equal(new DateTime(2025, 9, 29), currentAllocation.LastUpdateDate);
            Assert.True((DateTime.UtcNow - currentAllocation.UpdatedAt).TotalMinutes < 5);

            var nextAllocation = allocations.Single(a => a.FiscalYear.Name == "FY27");
            Assert.Equal(2_908_679.66m, nextAllocation.ToGoValue);
            Assert.Equal(0m, nextAllocation.ToDateValue);
            Assert.Equal(new DateTime(2025, 9, 29), nextAllocation.LastUpdateDate);
            Assert.True((DateTime.UtcNow - nextAllocation.UpdatedAt).TotalMinutes < 5);
        }
        finally
        {
            if (File.Exists(workbookPath))
            {
                File.Delete(workbookPath);
            }
        }
    }

    private static void CreateWorkbook(string path)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Engagement Detail");

        worksheet.Cell(4, 1).Value = "FY26 Period 3 week 4 | Last Update : 29 Sep 2025";

        const int headerRow = 11;
        worksheet.Cell(headerRow, 1).Value = "Engagement";
        worksheet.Cell(headerRow, ColumnNameToNumber("IN")).Value = "FYTG Backlog";
        worksheet.Cell(headerRow, ColumnNameToNumber("IO")).Value = "Future FY Backlog";
        worksheet.Cell(headerRow, ColumnNameToNumber("JN")).Value = "Original Budget";

        var dataRow = headerRow + 1;
        worksheet.Cell(dataRow, 1).Value = EngagementCode;
        worksheet.Cell(dataRow, ColumnNameToNumber("IN")).Value = 1_758_865.98m;
        worksheet.Cell(dataRow, ColumnNameToNumber("IO")).Value = 2_908_679.66m;
        worksheet.Cell(dataRow, ColumnNameToNumber("JN")).Value = 6_198_004.41m;

        workbook.SaveAs(path);
    }

    private static int ColumnNameToNumber(string columnName)
    {
        var normalized = columnName.Trim().ToUpperInvariant();
        var index = 0;

        foreach (var ch in normalized)
        {
            index = (index * 26) + (ch - 'A' + 1);
        }

        return index;
    }
}
