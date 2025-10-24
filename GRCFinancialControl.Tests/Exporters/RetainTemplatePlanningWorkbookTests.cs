using System;
using System.IO;
using ClosedXML.Excel;
using GRCFinancialControl.Persistence.Services.Exporters;
using Xunit;

namespace GRCFinancialControl.Tests.Exporters;

public sealed class RetainTemplatePlanningWorkbookTests
{
    [Fact]
    public void Load_FiltersPastWeeksAndExtractsEntries()
    {
        var referenceDate = new DateTime(2024, 1, 3);
        var workbookPath = CreatePlanningWorkbook(static sheet =>
        {
            sheet.Cell(1, 1).Value = "GPN";
            sheet.Cell(1, 2).Value = "Rank";
            sheet.Cell(1, 3).Value = "Recursos";
            sheet.Cell(1, 4).Value = "Office";
            sheet.Cell(1, 5).Value = new DateTime(2023, 12, 25);
            sheet.Cell(1, 6).Value = new DateTime(2024, 1, 1);
            sheet.Cell(1, 7).Value = new DateTime(2024, 1, 8);

            sheet.Cell(2, 1).Value = "BR123";
            sheet.Cell(2, 3).Value = "John Doe";
            sheet.Cell(2, 5).Value = "Past (E-0009)";
            sheet.Cell(2, 6).Value = "Alpha (E-0010)";
            sheet.Cell(2, 7).Value = "Bravo (E-0011)";
        });

        try
        {
            var snapshot = RetainTemplatePlanningWorkbook.Load(workbookPath, referenceDate);

            Assert.Equal(new DateTime(2024, 1, 1), snapshot.ReferenceWeekStart);
            Assert.Equal(new DateTime(2024, 1, 8), snapshot.LastWeekStart);
            Assert.Collection(
                snapshot.Entries,
                entry =>
                {
                    Assert.Equal("BR123", entry.ResourceId);
                    Assert.Equal("John Doe", entry.ResourceName);
                    Assert.Equal("E-0010", entry.EngagementCode);
                    Assert.Equal("Alpha", entry.EngagementName);
                    Assert.Equal(new DateTime(2024, 1, 1), entry.WeekStartDate);
                    Assert.Equal(40m, entry.Hours);
                },
                entry =>
                {
                    Assert.Equal("BR123", entry.ResourceId);
                    Assert.Equal("John Doe", entry.ResourceName);
                    Assert.Equal("E-0011", entry.EngagementCode);
                    Assert.Equal("Bravo", entry.EngagementName);
                    Assert.Equal(new DateTime(2024, 1, 8), entry.WeekStartDate);
                    Assert.Equal(40m, entry.Hours);
                });
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public void Load_ReturnsEmptySnapshotWhenNoFutureEngagements()
    {
        var referenceDate = new DateTime(2024, 5, 1);
        var workbookPath = CreatePlanningWorkbook(static sheet =>
        {
            sheet.Cell(1, 1).Value = "GPN";
            sheet.Cell(1, 2).Value = "Recursos";
            sheet.Cell(1, 3).Value = new DateTime(2024, 1, 1);
            sheet.Cell(2, 1).Value = "BR123";
            sheet.Cell(2, 2).Value = "John Doe";
            sheet.Cell(2, 3).Value = "Alfa";
        });

        try
        {
            var snapshot = RetainTemplatePlanningWorkbook.Load(workbookPath, referenceDate);

            Assert.Equal(new DateTime(2024, 4, 29), snapshot.ReferenceWeekStart);
            Assert.Null(snapshot.LastWeekStart);
            Assert.Empty(snapshot.Entries);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public void BuildSaturdayHeaders_CoversEveryWeekUntilLastEntry()
    {
        var referenceDate = new DateTime(2024, 1, 3);
        var workbookPath = CreatePlanningWorkbook(static sheet =>
        {
            sheet.Cell(1, 1).Value = "GPN";
            sheet.Cell(1, 2).Value = "Recursos";
            sheet.Cell(1, 3).Value = new DateTime(2024, 1, 1);
            sheet.Cell(1, 4).Value = new DateTime(2024, 1, 8);
            sheet.Cell(1, 5).Value = new DateTime(2024, 1, 15);
            sheet.Cell(1, 6).Value = new DateTime(2024, 1, 22);
            sheet.Cell(1, 7).Value = new DateTime(2024, 1, 29);

            sheet.Cell(2, 1).Value = "BR123";
            sheet.Cell(2, 2).Value = "John Doe";
            sheet.Cell(2, 3).Value = "Alpha (E-1001)";
            sheet.Cell(2, 7).Value = "Bravo (E-1002)";
        });

        try
        {
            var snapshot = RetainTemplatePlanningWorkbook.Load(workbookPath, referenceDate);
            var saturdays = snapshot.BuildSaturdayHeaders(referenceDate);

            Assert.Equal(
                new[]
                {
                    new DateTime(2024, 1, 6),
                    new DateTime(2024, 1, 13),
                    new DateTime(2024, 1, 20),
                    new DateTime(2024, 1, 27),
                    new DateTime(2024, 2, 3)
                },
                saturdays);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public void BuildSaturdayHeaders_WhenNoEntries_ReturnsReferenceSaturday()
    {
        var referenceDate = new DateTime(2024, 5, 1);
        var workbookPath = CreatePlanningWorkbook(static sheet =>
        {
            sheet.Cell(1, 1).Value = "GPN";
            sheet.Cell(1, 2).Value = "Recursos";
            sheet.Cell(1, 3).Value = new DateTime(2024, 1, 1);
        });

        try
        {
            var snapshot = RetainTemplatePlanningWorkbook.Load(workbookPath, referenceDate);
            var saturdays = snapshot.BuildSaturdayHeaders(referenceDate);

            Assert.Single(saturdays);
            Assert.Equal(new DateTime(2024, 5, 4), saturdays[0]);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    private static string CreatePlanningWorkbook(Action<IXLWorksheet> builder)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");

        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Alocações_Staff");
            builder(sheet);
            workbook.SaveAs(tempPath);
        }

        return tempPath;
    }
}
