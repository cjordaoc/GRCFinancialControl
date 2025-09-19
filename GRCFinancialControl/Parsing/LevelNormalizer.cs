using System;
using System.Collections.Generic;
using GRCFinancialControl.Common;

namespace GRCFinancialControl.Parsing
{
    internal static class LevelNormalizer
    {
        private static readonly Dictionary<string, string> LevelCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ANALYST"] = "A",
            ["ANALISTA"] = "A",
            ["JRANALYST"] = "A",
            ["JRANALISTA"] = "A",
            ["CONSULTANT"] = "C",
            ["CONSULTOR"] = "C",
            ["SRCONSULTANT"] = "SC",
            ["SENIORCONSULTANT"] = "SC",
            ["SENIORCONSULTOR"] = "SC",
            ["SRCONSULTOR"] = "SC",
            ["MANAGER"] = "M",
            ["GERENTE"] = "M",
            ["PROJECTMANAGER"] = "M",
            ["SRMANAGER"] = "SM",
            ["SENIORMANAGER"] = "SM",
            ["SENIORGERENTE"] = "SM",
            ["DIRECTOR"] = "D",
            ["DIRETOR"] = "D",
            ["PARTNER"] = "P",
            ["SÓCIO"] = "P",
            ["SOCIO"] = "P"
        };

        public static string? Normalize(string? rawLevel)
        {
            var trimmed = StringNormalizer.TrimToNull(rawLevel);
            if (trimmed == null)
            {
                return null;
            }

            var normalizedKey = ExcelParsingUtilities.NormalizeHeader(trimmed);
            if (LevelCodes.TryGetValue(normalizedKey, out var code))
            {
                return code;
            }

            return trimmed.ToUpperInvariant();
        }
    }
}
