using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Xunit;

namespace GRCFinancialControl.Tests.Exporters;

public sealed class RetainTemplateGeneratorTemplateTests
{
    [Fact]
    public void RetainTemplateContainsRequiredSheetsAndHeaders()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var templatePath = Path.Combine(repositoryRoot, "Templates", "RetainTemplate.xlsx.b64");
        Assert.True(File.Exists(templatePath), $"Retain template asset was not found at {templatePath}.");

        var base64 = File.ReadAllText(templatePath);
        using var templateStream = new MemoryStream(Convert.FromBase64String(base64));
        using var workbook = new XLWorkbook(templateStream);
        var worksheetNames = workbook.Worksheets.Select(sheet => sheet.Name).ToList();

        Assert.Contains("Macro1", worksheetNames);
        Assert.Contains("Data Entry", worksheetNames);
        Assert.Contains("Instructions", worksheetNames);
        Assert.Contains("Control", worksheetNames);

        var dataEntry = workbook.Worksheet("Data Entry");
        Assert.Equal("#", dataEntry.Cell(1, 1).GetString());
        Assert.Equal("Job Name", dataEntry.Cell(1, 2).GetString());
        Assert.Equal("Resource Name", dataEntry.Cell(1, 3).GetString());
        Assert.Equal("Resource ID", dataEntry.Cell(1, 4).GetString());

        var firstDateCell = dataEntry.Cell(1, 5);
        Assert.False(firstDateCell.IsEmpty(), "The first weekly header cell should contain a sample date value.");
        var firstDate = firstDateCell.GetDateTime().Date;
        Assert.Equal(DayOfWeek.Saturday, firstDate.DayOfWeek);

        var lastColumn = dataEntry.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var column = 1; column <= lastColumn; column++)
        {
            Assert.True(dataEntry.Cell(2, column).IsEmpty(), "Row 2 must remain blank in the template.");
        }
    }
}
