using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ExcelDataReader;
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Importers;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GRCFinancialControl.Persistence.Tests;

public sealed class ImportServiceFcsBacklogTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly TestDbContextFactory _factory;
    private readonly ImportService _importService;

    public ImportServiceFcsBacklogTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(_options);
        context.Database.EnsureCreated();

        _factory = new TestDbContextFactory(_options);
        _importService = new ImportService(_factory, NullLogger<ImportService>.Instance, new StubFullManagementDataImporter());
    }

    public async Task InitializeAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();

        var currentFiscalYear = new FiscalYear
        {
            Name = "FY26",
            StartDate = new DateTime(2025, 7, 1),
            EndDate = new DateTime(2026, 6, 30)
        };

        var nextFiscalYear = new FiscalYear
        {
            Name = "FY27",
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2027, 6, 30)
        };

        context.FiscalYears.AddRange(currentFiscalYear, nextFiscalYear);

        context.Engagements.Add(new Engagement
        {
            EngagementId = "E-1",
            Description = "Test engagement",
            OpeningValue = 1000m,
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
    public async Task ImportFcsRevenueBacklogAsync_UsesHeaderMatchingForAggregatedFutureColumn()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        CreateAggregatedWorkbook(workbookPath);
        var metadata = ReadMetadataCell(workbookPath);
        Assert.Equal("FY26", metadata);

        try
        {
            var summary = await _importService.ImportFcsRevenueBacklogAsync(workbookPath);

            Assert.Contains("Engagements affected: 1", summary);

            await using var verificationContext = await _factory.CreateDbContextAsync();
            var allocations = await verificationContext.EngagementFiscalYearRevenueAllocations
                .Include(a => a.FiscalYear)
                .Include(a => a.Engagement)
                .ToListAsync();

            Assert.Equal(2, allocations.Count);

            var currentAllocation = allocations.Single(a => a.FiscalYear.Name == "FY26");
            Assert.Equal(300m, currentAllocation.ToGoValue);
            Assert.Equal(500m, currentAllocation.ToDateValue);

            var futureAllocation = allocations.Single(a => a.FiscalYear.Name == "FY27");
            Assert.Equal(200m, futureAllocation.ToGoValue);
            Assert.Equal(0m, futureAllocation.ToDateValue);
        }
        finally
        {
            if (File.Exists(workbookPath))
            {
                File.Delete(workbookPath);
            }
        }
    }

    [Fact]
    public async Task ImportFcsRevenueBacklogAsync_SumsQuarterColumnsWhenAggregatedMissing()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        CreateQuarterWorkbook(workbookPath);
        var metadata = ReadMetadataCell(workbookPath);
        Assert.Equal("FY26", metadata);

        try
        {
            var summary = await _importService.ImportFcsRevenueBacklogAsync(workbookPath);

            Assert.Contains("Engagements affected: 1", summary);

            await using var verificationContext = await _factory.CreateDbContextAsync();
            var allocations = await verificationContext.EngagementFiscalYearRevenueAllocations
                .Include(a => a.FiscalYear)
                .ToListAsync();

            var futureAllocation = allocations.Single(a => a.FiscalYear.Name == "FY27");
            Assert.Equal(450m, futureAllocation.ToGoValue);
        }
        finally
        {
            if (File.Exists(workbookPath))
            {
                File.Delete(workbookPath);
            }
        }
    }

    private static void CreateAggregatedWorkbook(string path)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Backlog");

        worksheet.Cell(1, 1).Value = "Header";
        worksheet.Cell(2, 1).Value = "Header";
        worksheet.Cell(3, 1).Value = "Header";
        worksheet.Cell(4, 1).Value = "FY26";

        var headerRow = 11;
        worksheet.Cell(headerRow, 1).Value = "Engagement ID";
        worksheet.Cell(headerRow, 5).Value = "FYTG Backlog";
        worksheet.Cell(headerRow, 6).Value = "Future FY Backlog";
        worksheet.Cell(headerRow, 7).Value = "Future FY Backlog (Lead)";
        worksheet.Cell(headerRow, 8).Value = "Future FY Backlog Opp Currency";

        var dataRow = headerRow + 1;
        worksheet.Cell(dataRow, 1).Value = "E-1";
        worksheet.Cell(dataRow, 5).Value = 300m;
        worksheet.Cell(dataRow, 6).Value = 200m;
        worksheet.Cell(dataRow, 7).Value = 999m; // ignored
        worksheet.Cell(dataRow, 8).Value = 888m; // ignored

        workbook.SaveAs(path);
    }

    private static void CreateQuarterWorkbook(string path)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Backlog");

        worksheet.Cell(1, 1).Value = "Header";
        worksheet.Cell(2, 1).Value = "Header";
        worksheet.Cell(3, 1).Value = "Header";
        worksheet.Cell(4, 1).Value = "FY26";

        var headerRow = 11;
        worksheet.Cell(headerRow, 1).Value = "Engagement ID";
        worksheet.Cell(headerRow, 5).Value = "FYTG Backlog";
        worksheet.Cell(headerRow, 6).Value = "Future FY27 Q1 Backlog Jan - Mar";
        worksheet.Cell(headerRow, 7).Value = "Future FY27 Q2 Backlog Apr - Jun";
        worksheet.Cell(headerRow, 8).Value = "Future FY28 Q1 Backlog"; // ignored because not next FY

        var dataRow = headerRow + 1;
        worksheet.Cell(dataRow, 1).Value = "E-1";
        worksheet.Cell(dataRow, 5).Value = 300m;
        worksheet.Cell(dataRow, 6).Value = 200m;
        worksheet.Cell(dataRow, 7).Value = 250m;
        worksheet.Cell(dataRow, 8).Value = 400m;

        workbook.SaveAs(path);
    }

    private static string ReadMetadataCell(string path)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false
            }
        });

        if (dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count <= 3)
        {
            return string.Empty;
        }

        return Convert.ToString(dataSet.Tables[0].Rows[3][0], CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed class StubFullManagementDataImporter : IFullManagementDataImporter
    {
        public Task<FullManagementDataImportResult> ImportAsync(string filePath)
        {
            return Task.FromResult(new FullManagementDataImportResult("", 0, 0, 0, 0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>, IAsyncDisposable
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

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
