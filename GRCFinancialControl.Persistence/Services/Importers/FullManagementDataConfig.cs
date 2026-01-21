using System;
using System.Collections.Generic;
using System.Linq;

namespace GRCFinancialControl.Persistence.Services.Importers
{
    /// <summary>
    /// Configuration and field mapping for Full Management Data Importer.
    /// Centralizes all header alias definitions and column mappings to enable
    /// flexible field resolution without code recompilation.
    /// </summary>
    public static class FullManagementDataConfig
    {
        /// <summary>
        /// Header alias groups for flexible column resolution.
        /// Each group contains acceptable variations of a header name.
        /// </summary>
        public static class Headers
        {
            public static readonly string[] EngagementId = new[]
            {
                "engagement id",
                "project id",
                "eng id"
            };

            public static readonly string[] EngagementName = new[]
            {
                "engagement description",
                "engagement name",
                "project name",
                "engagement",
                "description"
            };

            public static readonly string[] CustomerName = new[]
            {
                "customer name",
                "customername",
                "customer-name",
                "client name",
                "clientname",
                "client-name",
                "client name (id)",
                "clientname(id)",
                "client-name(id)",
                "client description",
                "clientdescription",
                "client-description",
                "customer description",
                "customerdescription",
                "customer-description",
                "customer",
                "client"
            };

            public static readonly string[] CustomerId = new[]
            {
                "customer id",
                "customerid",
                "customer-id",
                "client id",
                "clientid",
                "client-id",
                "customer code",
                "customercode",
                "customer-code",
                "client code",
                "clientcode",
                "client-code"
            };

            public static readonly string[] OpportunityCurrency = new[]
            {
                "opportunity currency",
                "engagement currency"
            };

            public static readonly string[] OriginalBudgetHours = new[]
            {
                "original budget hours",
                "budget hours",
                "hours budget",
                "bud hours"
            };

            public static readonly string[] OriginalBudgetTer = new[]
            {
                "original budget ter",
                "budget value",
                "value bud",
                "revenue bud"
            };

            public static readonly string[] OriginalBudgetMarginPercent = new[]
            {
                "original budget margin %",
                "margin % bud",
                "budget margin",
                "margin budget"
            };

            public static readonly string[] OriginalBudgetExpenses = new[]
            {
                "original budget expenses",
                "expenses bud",
                "budget expenses"
            };

            public static readonly string[] ChargedHoursEtd = new[]
            {
                "charged hours etd",
                "etd hours",
                "hours etd"
            };

            public static readonly string[] ChargedHoursFytd = new[]
            {
                "charged hours fytd",
                "fytd hours",
                "hours fytd"
            };

            public static readonly string[] TermMercuryProjected = new[]
            {
                "ter mercury projected opp currency",
                "etcp value",
                "value etc-p",
                "etp value",
                "etc value"
            };

            public static readonly string[] ValueData = new[]
            {
                "ter etd",
                "value data",
                "valuedata",
                "etd value"
            };

            public static readonly string[] TerFiscalYearToDate = new[]
            {
                "ter fytd",
                "ter fytd (r$)",
                "ter fytd (brl)",
                "ter fytd value",
                "ter fytd (local currency)",
                "ter fytd local currency",
                "ter fytd (lc)",
                "ter fytd (opp currency)",
                "ter fytd opp currency",
                "ter fytd (opportunity currency)",
                "ter fytd opportunity currency",
                "ter fytd (moeda da oportunidade)",
                "ter fytd moeda da oportunidade",
                "ter fytd (moeda oportunidade)",
                "ter fytd (moeda local)",
                "ter fytd moeda local",
                "ter fiscal year to date",
                "fytd ter"
            };

            public static readonly string[] MarginPercentEtd = new[]
            {
                "margin % etd",
                "etd margin %",
                "margin etd"
            };

            public static readonly string[] MarginPercentFytd = new[]
            {
                "margin % fytd",
                "fytd margin %",
                "margin fytd"
            };

            public static readonly string[] ExpensesEtd = new[]
            {
                "expenses etd",
                "etd expenses"
            };

            public static readonly string[] ExpensesFytd = new[]
            {
                "expenses fytd",
                "fytd expenses"
            };

            public static readonly string[] Status = new[]
            {
                "engagement status",
                "status"
            };

            public static readonly string[] EngagementPartnerGui = new[]
            {
                "engagement partner gui"
            };

            public static readonly string[] EngagementManagerGui = new[]
            {
                "engagement manager gui"
            };

            public static readonly string[] EtcAgeDays = new[]
            {
                "etc age days",
                "age days"
            };

            public static readonly string[] UnbilledRevenueDays = new[]
            {
                "unbilled revenue days"
            };

            public static readonly string[] LastActiveEtcPDate = new[]
            {
                "last active etc-p date",
                "last etc date",
                "last etc-p",
                "last etc"
            };

            public static readonly string[] NextEtcDate = new[]
            {
                "next etc date",
                "proposed next etc",
                "next etc-p"
            };

            public static readonly string[] CurrentFiscalYearBacklog = new[]
            {
                "fytg backlog",
                "fiscal year to go backlog",
                "current backlog"
            };

            public static readonly string[] FutureFiscalYearBacklog = new[]
            {
                "future fy backlog",
                "future fiscal year backlog",
                "future backlog"
            };
        }

        /// <summary>
        /// All header groups for worksheet format detection.
        /// </summary>
        public static readonly IReadOnlyList<string[]> AllHeaderGroups = new[]
        {
            Headers.EngagementId,
            Headers.EngagementName,
            Headers.CustomerName,
            Headers.CustomerId,
            Headers.OpportunityCurrency,
            Headers.OriginalBudgetHours,
            Headers.OriginalBudgetTer,
            Headers.OriginalBudgetMarginPercent,
            Headers.OriginalBudgetExpenses,
            Headers.ChargedHoursEtd,
            Headers.ChargedHoursFytd,
            Headers.TermMercuryProjected,
            Headers.ValueData,
            Headers.TerFiscalYearToDate,
            Headers.MarginPercentEtd,
            Headers.MarginPercentFytd,
            Headers.ExpensesEtd,
            Headers.ExpensesFytd,
            Headers.Status,
            Headers.EtcAgeDays,
            Headers.UnbilledRevenueDays,
            Headers.LastActiveEtcPDate,
            Headers.NextEtcDate,
            Headers.CurrentFiscalYearBacklog,
            Headers.FutureFiscalYearBacklog
        };

        /// <summary>
        /// Column specifications with field names, expected locations, and header aliases.
        /// </summary>
        public static class Columns
        {
            public class ColumnDef
            {
                public required string FieldName { get; set; }
                public required string Label { get; set; }
                public required string PreferredLetter { get; set; }
                public int PreferredIndex { get; set; }
                public required string[] Aliases { get; set; }
                public bool IsRequired { get; set; }
            }

            private static int LetterToIndex(string? letter)
            {
                if (string.IsNullOrEmpty(letter))
                {
                    return -1;
                }

                int index = 0;
                for (int i = 0; i < letter.Length; i++)
                {
                    index = index * 26 + (char.ToUpperInvariant(letter[i]) - 'A' + 1);
                }
                return index - 1;
            }

            public static readonly ColumnDef[] All = new[]
            {
                new ColumnDef
                {
                    FieldName = "EngagementId",
                    Label = "Engagement ID",
                    PreferredLetter = "A",
                    PreferredIndex = LetterToIndex("A"),
                    Aliases = Headers.EngagementId,
                    IsRequired = true
                },
                new ColumnDef
                {
                    FieldName = "EngagementName",
                    Label = "Engagement",
                    PreferredLetter = "B",
                    PreferredIndex = LetterToIndex("B"),
                    Aliases = Headers.EngagementName,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "CustomerName",
                    Label = "Client",
                    PreferredLetter = "BK",
                    PreferredIndex = LetterToIndex("BK"),
                    Aliases = Headers.CustomerName,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "CustomerCode",
                    Label = "Client ID",
                    PreferredLetter = "BI",
                    PreferredIndex = LetterToIndex("BI"),
                    Aliases = Headers.CustomerId,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "OpportunityCurrency",
                    Label = "Opportunity Currency",
                    PreferredLetter = "FX",
                    PreferredIndex = LetterToIndex("FX"),
                    Aliases = Headers.OpportunityCurrency,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "OriginalBudgetHours",
                    Label = "Original Budget Hours",
                    PreferredLetter = "HL",
                    PreferredIndex = LetterToIndex("HL"),
                    Aliases = Headers.OriginalBudgetHours,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "OriginalBudgetTer",
                    Label = "Original Budget TER",
                    PreferredLetter = "JN",
                    PreferredIndex = LetterToIndex("JN"),
                    Aliases = Headers.OriginalBudgetTer,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "OriginalBudgetMarginPercent",
                    Label = "Original Budget Margin %",
                    PreferredLetter = "HW",
                    PreferredIndex = LetterToIndex("HW"),
                    Aliases = Headers.OriginalBudgetMarginPercent,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "OriginalBudgetExpenses",
                    Label = "Original Budget Expenses",
                    PreferredLetter = "HQ",
                    PreferredIndex = LetterToIndex("HQ"),
                    Aliases = Headers.OriginalBudgetExpenses,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "ChargedHours",
                    Label = "Charged Hours ETD",
                    PreferredLetter = "CI",
                    PreferredIndex = LetterToIndex("CI"),
                    Aliases = Headers.ChargedHoursEtd,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "ChargedHoursFytd",
                    Label = "Charged Hours FYTD",
                    PreferredLetter = "CJ",
                    PreferredIndex = LetterToIndex("CJ"),
                    Aliases = Headers.ChargedHoursFytd,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "TermMercuryProjectedOppCurrency",
                    Label = "TER Mercury Projected Opp Currency",
                    PreferredLetter = "FP",
                    PreferredIndex = LetterToIndex("FP"),
                    Aliases = Headers.TermMercuryProjected,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "ValueData",
                    Label = "TER ETD",
                    PreferredLetter = "CU",
                    PreferredIndex = LetterToIndex("CU"),
                    Aliases = Headers.ValueData,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "TerFiscalYearToDate",
                    Label = "TER FYTD",
                    PreferredLetter = "CV",
                    PreferredIndex = LetterToIndex("CV"),
                    Aliases = Headers.TerFiscalYearToDate,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "MarginPercentEtd",
                    Label = "Margin % ETD",
                    PreferredLetter = "CG",
                    PreferredIndex = LetterToIndex("CG"),
                    Aliases = Headers.MarginPercentEtd,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "MarginPercentFytd",
                    Label = "Margin % FYTD",
                    PreferredLetter = "CH",
                    PreferredIndex = LetterToIndex("CH"),
                    Aliases = Headers.MarginPercentFytd,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "ExpensesEtd",
                    Label = "Expenses ETD",
                    PreferredLetter = "DH",
                    PreferredIndex = LetterToIndex("DH"),
                    Aliases = Headers.ExpensesEtd,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "ExpensesFytd",
                    Label = "Expenses FYTD",
                    PreferredLetter = "DI",
                    PreferredIndex = LetterToIndex("DI"),
                    Aliases = Headers.ExpensesFytd,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "Status",
                    Label = "Engagement Status",
                    PreferredLetter = "AH",
                    PreferredIndex = LetterToIndex("AH"),
                    Aliases = Headers.Status,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "EngagementPartnerGui",
                    Label = "Engagement Partner GUI",
                    PreferredLetter = "AO",
                    PreferredIndex = LetterToIndex("AO"),
                    Aliases = Headers.EngagementPartnerGui,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "EngagementManagerGui",
                    Label = "Engagement Manager GUI",
                    PreferredLetter = "AZ",
                    PreferredIndex = LetterToIndex("AZ"),
                    Aliases = Headers.EngagementManagerGui,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "EtcAgeDays",
                    Label = "ETC-P Age",
                    PreferredLetter = "FB",
                    PreferredIndex = LetterToIndex("FB"),
                    Aliases = Headers.EtcAgeDays,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "UnbilledRevenueDays",
                    Label = "Unbilled Revenue Days",
                    PreferredLetter = "GA",
                    PreferredIndex = LetterToIndex("GA"),
                    Aliases = Headers.UnbilledRevenueDays,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "LastActiveEtcPDate",
                    Label = "Last Active ETC-P Date",
                    PreferredLetter = "EZ",
                    PreferredIndex = LetterToIndex("EZ"),
                    Aliases = Headers.LastActiveEtcPDate,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "NextEtcDate",
                    Label = "Next ETC Date",
                    PreferredLetter = "FD",
                    PreferredIndex = LetterToIndex("FD"),
                    Aliases = Headers.NextEtcDate,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "CurrentFiscalYearBacklog",
                    Label = "FYTG Backlog",
                    PreferredLetter = "GR",
                    PreferredIndex = LetterToIndex("GR"),
                    Aliases = Headers.CurrentFiscalYearBacklog,
                    IsRequired = false
                },
                new ColumnDef
                {
                    FieldName = "FutureFiscalYearBacklog",
                    Label = "Future FY Backlog",
                    PreferredLetter = "GS",
                    PreferredIndex = LetterToIndex("GS"),
                    Aliases = Headers.FutureFiscalYearBacklog,
                    IsRequired = false
                }
            };

            public static ColumnDef? GetByFieldName(string fieldName)
                => All.FirstOrDefault(c => c.FieldName == fieldName);
        }
    }
}
