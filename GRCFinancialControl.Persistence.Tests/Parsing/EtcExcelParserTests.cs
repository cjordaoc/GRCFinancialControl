#if NET8_0_WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRCFinancialControl.Parsing;
using Xunit;

namespace GRCFinancialControl.Persistence.Tests.Parsing
{
    public sealed class EtcExcelParserTests
    {
        [Fact]
        public void Parse_EyTemplate_ReadsHoursAndEtcWithoutWarnings()
        {
            var parser = new EtcExcelParser();
            var filePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "GRCFinancialControl",
                "DataTemplate",
                "EY_PERSON_ETC_LAST_TRANSFERRED_v1.xlsx"));

            var result = parser.Parse(filePath);

            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
            Assert.Equal(0, result.Skipped);
            Assert.Equal(5, result.Rows.Count);

            var expected = new Dictionary<string, (decimal Hours, decimal EtcRemaining)>
            {
                ["Danilo Luiz Passos"] = (18m, 14m),
                ["Caio Jordao Calisto"] = (8m, 24m),
                ["Mariana Galegale Ferreira Mathias"] = (82m, 72m),
                ["Thais Frederico Silva"] = (116m, 200m),
                ["Ruthe De Sousa Ferreira"] = (40m, 200m)
            };

            foreach (var row in result.Rows)
            {
                Assert.Equal("E-69288339", row.EngagementId);
                Assert.True(expected.ContainsKey(row.EmployeeName), $"Unexpected employee '{row.EmployeeName}'.");

                var expectedMetrics = expected[row.EmployeeName];
                Assert.Equal(expectedMetrics.Hours, row.HoursIncurred);
                Assert.Equal(expectedMetrics.EtcRemaining, row.EtcRemaining);
            }

            Assert.Equal(264m, result.Rows.Sum(r => r.HoursIncurred));
            Assert.Equal(510m, result.Rows.Sum(r => r.EtcRemaining));
        }
    }
}
#endif
