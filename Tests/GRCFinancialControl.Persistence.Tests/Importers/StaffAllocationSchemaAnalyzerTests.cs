using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GRCFinancialControl.Persistence.Tests.Importers;

public sealed class StaffAllocationSchemaAnalyzerTests
{
    [Fact]
    public void Analyze_ReturnsFixedAndWeekColumns()
    {
        var table = BuildWorksheet();

        var analyzer = new StaffAllocationSchemaAnalyzer();
        var analysis = analyzer.Analyze(table);

        Assert.Equal(1, analysis.HeaderRowIndex);
        Assert.Equal(6, analysis.FixedColumns.Count);

        Assert.Equal(0, analysis.FixedColumns[StaffAllocationFixedColumn.Gpn].ColumnIndex);
        Assert.Equal(1, analysis.FixedColumns[StaffAllocationFixedColumn.ResourceName].ColumnIndex);
        Assert.Equal(2, analysis.FixedColumns[StaffAllocationFixedColumn.Rank].ColumnIndex);
        Assert.Equal(3, analysis.FixedColumns[StaffAllocationFixedColumn.UtilizationFytd].ColumnIndex);
        Assert.Equal(4, analysis.FixedColumns[StaffAllocationFixedColumn.Office].ColumnIndex);
        Assert.Equal(5, analysis.FixedColumns[StaffAllocationFixedColumn.Subdomain].ColumnIndex);

        Assert.Equal(2, analysis.WeekColumns.Count);
        Assert.Equal(new DateTime(2024, 5, 6), analysis.WeekColumns[0].WeekStartMon);
        Assert.Equal(new DateTime(2024, 5, 13), analysis.WeekColumns[1].WeekStartMon);
    }

    [Fact]
    public void Analyze_ThrowsWhenWeekHeaderIsNotMonday()
    {
        var table = BuildWorksheet();
        table.Rows[1][6] = new DateTime(2024, 5, 7); // Tuesday

        var analyzer = new StaffAllocationSchemaAnalyzer();

        var exception = Assert.Throws<InvalidDataException>(() => analyzer.Analyze(table));
        Assert.Contains("not Mondays", exception.Message);
    }

    [Fact]
    public void Parse_GeneratesRecordsAndFlagsUnknownAndInactiveEmployees()
    {
        var (table, employees) = BuildWorksheetWithEmployees();

        var parser = new StaffAllocationWorksheetParser(
            new StaffAllocationSchemaAnalyzer(),
            NullLogger<StaffAllocationWorksheetParser>.Instance);

        var result = parser.Parse(table, employees);

        Assert.Equal(3, result.ProcessedRowCount);
        Assert.Equal(2, result.Records.Count);

        var knownRecord = Assert.Single(result.Records, r => r.Gpn == "123456");
        Assert.Equal("E-1001", knownRecord.EngagementCode);
        Assert.False(knownRecord.IsUnknownAffiliation);
        Assert.Equal(40m, knownRecord.Hours);

        var unknownRecord = Assert.Single(result.Records, r => r.Gpn == "000000");
        Assert.True(unknownRecord.IsUnknownAffiliation);
        Assert.Contains("000000", result.UnknownAffiliations);

        var skipped = Assert.Single(result.SkippedInactiveEmployees);
        Assert.Equal("654321", skipped.Gpn);
        Assert.Equal(new DateTime(2024, 5, 6), skipped.WeekStartMon);
    }

    [Fact]
    public void Processor_ComputesSummaryFromRecords()
    {
        var (table, employees) = BuildWorksheetWithEmployees();

        var parser = new StaffAllocationWorksheetParser(
            new StaffAllocationSchemaAnalyzer(),
            NullLogger<StaffAllocationWorksheetParser>.Instance);

        var processor = new StaffAllocationProcessor(parser);
        var result = processor.Process(table, employees);

        Assert.Equal(3, result.Summary.ProcessedRowCount);
        Assert.Equal(2, result.Summary.DistinctEngagementCount);
        Assert.Equal(2, result.Summary.DistinctRankCount);
        Assert.Contains("Consultant", result.Summary.DistinctRanks);
        Assert.Contains("Senior", result.Summary.DistinctRanks);
    }

    private static DataTable BuildWorksheet()
    {
        var table = new DataTable();
        for (var i = 0; i < 8; i++)
        {
            table.Columns.Add($"Col{i}");
        }

        var metadataRow = table.NewRow();
        metadataRow[0] = "Resumo";
        table.Rows.Add(metadataRow);

        var headerRow = table.NewRow();
        headerRow[0] = "GPN";
        headerRow[1] = "Recursos";
        headerRow[2] = "Rank";
        headerRow[3] = "% Utilização FYTD";
        headerRow[4] = "Escritório";
        headerRow[5] = "Subdomínio";
        headerRow[6] = new DateTime(2024, 5, 6);
        headerRow[7] = "13/05/2024";
        table.Rows.Add(headerRow);

        var dataRow = table.NewRow();
        dataRow[0] = "123456";
        dataRow[1] = "Fulano";
        dataRow[2] = "Senior";
        dataRow[3] = 0.85m;
        dataRow[4] = "SP";
        dataRow[5] = "GRC";
        dataRow[6] = "E-1001";
        dataRow[7] = string.Empty;
        table.Rows.Add(dataRow);

        return table;
    }

    private static (DataTable Table, Dictionary<string, Employee> Employees) BuildWorksheetWithEmployees()
    {
        var table = BuildWorksheet();

        var unknownRow = table.NewRow();
        unknownRow[0] = "000000";
        unknownRow[1] = "Desconhecido";
        unknownRow[2] = "Consultant";
        unknownRow[3] = 0.75m;
        unknownRow[4] = "RJ";
        unknownRow[5] = "Cyber";
        unknownRow[6] = "E-2002";
        table.Rows.Add(unknownRow);

        var inactiveRow = table.NewRow();
        inactiveRow[0] = "654321";
        inactiveRow[1] = "Ciclano";
        inactiveRow[2] = "Manager";
        inactiveRow[3] = 0.6m;
        inactiveRow[4] = "SP";
        inactiveRow[5] = "Risk";
        inactiveRow[6] = "E-3003";
        table.Rows.Add(inactiveRow);

        var employees = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase)
        {
            ["123456"] = new Employee
            {
                Gpn = "123456",
                EmployeeName = "Fulano"
            },
            ["654321"] = new Employee
            {
                Gpn = "654321",
                EmployeeName = "Ciclano",
                EndDate = new DateTime(2024, 4, 1)
            }
        };

        return (table, employees);
    }
}
