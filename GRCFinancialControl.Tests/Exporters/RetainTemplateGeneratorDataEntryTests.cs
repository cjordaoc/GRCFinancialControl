using System;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
using GRCFinancialControl.Persistence.Services.Exporters;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GRCFinancialControl.Tests.Exporters;

public sealed class RetainTemplateGeneratorDataEntryTests
{
    [Fact]
    public async Task GenerateRetainTemplateAsync_PopulatesDataEntrySheet()
    {
        var today = DateTime.Today;
        var firstMonday = StartOfWeek(today);
        var secondMonday = firstMonday.AddDays(7);

        var allocationPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");

        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Alocações_Staff");
            sheet.Cell(1, 1).Value = "GPN";
            sheet.Cell(1, 2).Value = "Recursos";
            sheet.Cell(1, 3).Value = firstMonday;
            sheet.Cell(1, 4).Value = secondMonday;

            sheet.Cell(2, 1).Value = "BR123";
            sheet.Cell(2, 2).Value = "Jane Smith";
            sheet.Cell(2, 3).Value = "Alpha (E-0001)";
            sheet.Cell(2, 4).Value = "Alpha (E-0001)";

            sheet.Cell(3, 1).Value = "BR456";
            sheet.Cell(3, 2).Value = "John Doe";
            sheet.Cell(3, 4).Value = "Bravo (E-0002)";

            workbook.SaveAs(allocationPath);
        }

        var generator = new RetainTemplateGenerator(NullLogger<RetainTemplateGenerator>.Instance);

        string? outputPath = null;

        try
        {
            outputPath = await generator.GenerateRetainTemplateAsync(allocationPath);

            Assert.True(File.Exists(outputPath));

            using var outputWorkbook = new XLWorkbook(outputPath);
            var dataEntry = outputWorkbook.Worksheet("Data Entry");

            var firstSaturday = NextOrSameSaturday(today);
            var secondSaturday = firstSaturday.AddDays(7);

            Assert.Equal(firstSaturday, dataEntry.Cell(1, 5).GetDateTime().Date);
            Assert.Equal(secondSaturday, dataEntry.Cell(1, 6).GetDateTime().Date);

            Assert.True(dataEntry.Row(2).Cell(1).IsEmpty());
            Assert.True(dataEntry.Row(2).Cell(5).IsEmpty());

            Assert.Equal(1, dataEntry.Cell(3, 1).GetValue<int>());
            Assert.Equal("Alpha (E-0001)", dataEntry.Cell(3, 2).GetString());
            Assert.Equal("Jane Smith", dataEntry.Cell(3, 3).GetString());
            Assert.Equal("BR123", dataEntry.Cell(3, 4).GetString());
            Assert.Equal(40m, dataEntry.Cell(3, 5).GetValue<decimal>());
            Assert.Equal(40m, dataEntry.Cell(3, 6).GetValue<decimal>());

            Assert.Equal(2, dataEntry.Cell(4, 1).GetValue<int>());
            Assert.Equal("Bravo (E-0002)", dataEntry.Cell(4, 2).GetString());
            Assert.Equal("John Doe", dataEntry.Cell(4, 3).GetString());
            Assert.Equal("BR456", dataEntry.Cell(4, 4).GetString());
            Assert.True(dataEntry.Cell(4, 5).IsEmpty());
            Assert.Equal(40m, dataEntry.Cell(4, 6).GetValue<decimal>());
        }
        finally
        {
            if (File.Exists(allocationPath))
            {
                File.Delete(allocationPath);
            }

            if (outputPath is not null && File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var monday = date.Date;
        var offset = (int)monday.DayOfWeek - (int)DayOfWeek.Monday;
        if (offset < 0)
        {
            offset += 7;
        }

        return monday.AddDays(-offset);
    }

    private static DateTime NextOrSameSaturday(DateTime date)
    {
        var current = date.Date;
        var daysToSaturday = ((int)DayOfWeek.Saturday - (int)current.DayOfWeek + 7) % 7;
        return current.AddDays(daysToSaturday);
    }
}
