using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services;
using GRCFinancialControl.Persistence.Services.Importers.StaffAllocations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GRCFinancialControl.Tests.Persistence;

public sealed class SimplifiedStaffAllocationParserTests
{
    [Fact]
    public void Parse_UsesOnlyWeeksWithinClosingPeriod()
    {
        var worksheet = new TestWorksheet(new[]
        {
            new object?[]
            {
                "GPN",
                "Utilization",
                "Rank",
                "Name",
                "Office",
                "Subdomain",
                new DateTime(2024, 1, 1),
                new DateTime(2024, 1, 8),
                new DateTime(2024, 1, 15)
            },
            new object?[]
            {
                "12345",
                "97.95",
                "Manager",
                "John Doe",
                "São Paulo",
                "Acessos",
                "E-0001",
                "E-0001",
                "E-0001"
            },
            new object?[] { null, null, null, null, null, null, null, null, null },
            new object?[] { null, null, null, null, null, null, null, null, null },
            new object?[] { null, null, null, null, null, null, null, null, null }
        });

        var closingPeriod = new ClosingPeriod
        {
            Id = 1,
            Name = "2024-Q1",
            FiscalYearId = 1,
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 14)
        };

        var rankMappings = new List<RankMapping>
        {
            new()
            {
                RawRank = "Manager",
                SpreadsheetRank = "Manager",
                NormalizedRank = "Manager",
                IsActive = true
            }
        };

        var parser = new SimplifiedStaffAllocationParser(NullLogger<SimplifiedStaffAllocationParser>.Instance);

        var allocations = parser.Parse(worksheet, closingPeriod, rankMappings);

        var allocation = Assert.Single(allocations);
        Assert.Equal("E-0001", allocation.EngagementCode);
        Assert.Equal("MANAGER", allocation.RankCode);
        Assert.Equal(1, allocation.FiscalYearId);
        Assert.Equal(80m, allocation.Hours);
    }

    [Fact]
    public void Parse_AssignsFortyHoursPerEngagementWeekCell()
    {
        var worksheet = new TestWorksheet(new[]
        {
            new object?[]
            {
                "GPN",
                "Utilization",
                "Rank",
                "Name",
                "Office",
                "Subdomain",
                new DateTime(2024, 1, 1),
                new DateTime(2024, 1, 8),
                new DateTime(2024, 1, 15)
            },
            new object?[]
            {
                "12345",
                "97.95",
                "Manager",
                "John Doe",
                "São Paulo",
                "Acessos",
                "E-0001",
                "E-0001",
                null
            },
            new object?[]
            {
                "67890",
                "88.10",
                "Senior 1",
                "Jane Roe",
                "Rio de Janeiro",
                "Acessos",
                "E-0002",
                null,
                null
            },
            new object?[] { null, null, null, null, null, null, null, null, null },
            new object?[] { null, null, null, null, null, null, null, null, null },
            new object?[] { null, null, null, null, null, null, null, null, null }
        });

        var closingPeriod = new ClosingPeriod
        {
            Id = 1,
            Name = "2024-Q1",
            FiscalYearId = 1,
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 14)
        };

        var rankMappings = new List<RankMapping>
        {
            new()
            {
                RawRank = "Manager",
                SpreadsheetRank = "Manager",
                NormalizedRank = "Manager",
                IsActive = true
            },
            new()
            {
                RawRank = "Senior 1",
                SpreadsheetRank = "Senior 1",
                NormalizedRank = "Senior 1",
                IsActive = true
            }
        };

        var parser = new SimplifiedStaffAllocationParser(NullLogger<SimplifiedStaffAllocationParser>.Instance);

        var allocations = parser
            .Parse(worksheet, closingPeriod, rankMappings)
            .OrderBy(a => a.EngagementCode, StringComparer.Ordinal)
            .ThenBy(a => a.RankCode, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(2, allocations.Count);

        var first = allocations[0];
        Assert.Equal("E-0001", first.EngagementCode);
        Assert.Equal("MANAGER", first.RankCode);
        Assert.Equal(80m, first.Hours);

        var second = allocations[1];
        Assert.Equal("E-0002", second.EngagementCode);
        Assert.Equal("SENIOR 1", second.RankCode);
        Assert.Equal(40m, second.Hours);

        Assert.Equal(120m, allocations.Sum(a => a.Hours));
    }

    [Fact]
    public void Parse_MapsSpreadsheetRankToRankCode()
    {
        var worksheet = new TestWorksheet(new[]
        {
            new object?[]
            {
                "GPN",
                "Utilization",
                "Rank",
                "Name",
                "Office",
                "Subdomain",
                new DateTime(2024, 2, 5),
                new DateTime(2024, 2, 12)
            },
            new object?[]
            {
                "24680",
                "92.50",
                "Senior 3",
                "Alex Doe",
                "São Paulo",
                "Consulting",
                "E-0100",
                "E-0100"
            },
            new object?[] { null, null, null, null, null, null, null, null }
        });

        var closingPeriod = new ClosingPeriod
        {
            Id = 2,
            Name = "2024-Q2",
            FiscalYearId = 2,
            PeriodStart = new DateTime(2024, 2, 1),
            PeriodEnd = new DateTime(2024, 2, 28)
        };

        var rankMappings = new List<RankMapping>
        {
            new()
            {
                RawRank = "RANK-S3",
                SpreadsheetRank = "Senior 3",
                NormalizedRank = "Senior 3",
                IsActive = true
            }
        };

        var parser = new SimplifiedStaffAllocationParser(NullLogger<SimplifiedStaffAllocationParser>.Instance);

        var allocations = parser.Parse(worksheet, closingPeriod, rankMappings);

        var allocation = Assert.Single(allocations);
        Assert.Equal("E-0100", allocation.EngagementCode);
        Assert.Equal("RANK-S3", allocation.RankCode);
        Assert.Equal(2, allocation.FiscalYearId);
        Assert.Equal(80m, allocation.Hours);
    }

    private sealed class TestWorksheet : ImportService.IWorksheet
    {
        private readonly object?[][] _cells;
        private readonly int _columnCount;

        public TestWorksheet(IReadOnlyList<object?[]> rows)
        {
            if (rows is null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            _cells = rows.Select(row => row.ToArray()).ToArray();
            _columnCount = _cells.Length == 0 ? 0 : _cells.Max(row => row.Length);
        }

        public int RowCount => _cells.Length;

        public int ColumnCount => _columnCount;

        public object? GetValue(int rowIndex, int columnIndex)
        {
            if ((uint)rowIndex >= (uint)_cells.Length)
            {
                return null;
            }

            var row = _cells[rowIndex];
            if ((uint)columnIndex >= (uint)row.Length)
            {
                return null;
            }

            return row[columnIndex];
        }
    }
}
