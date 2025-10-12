using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExcelDataReader;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class ImportService : IImportService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private static readonly Regex MultiWhitespaceRegex = new Regex("\\s+", RegexOptions.Compiled);
        private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");

        public ImportService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<string> ImportBudgetAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Budget workbook could not be found.", filePath);
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });

            var planInfo = ResolveWorksheet(dataSet, "PLAN INFO") ??
                           throw new InvalidDataException("Worksheet 'PLAN INFO' is missing from the budget workbook.");
            var resourcing = ResolveWorksheet(dataSet, "RESOURCING") ??
                             throw new InvalidDataException("Worksheet 'RESOURCING' is missing from the budget workbook.");

            var customerName = NormalizeWhitespace(GetCellString(planInfo, 3, 1));
            var engagementKey = NormalizeWhitespace(GetCellString(planInfo, 4, 1));
            var descriptionRaw = NormalizeWhitespace(GetCellString(planInfo, 0, 0));

            if (string.IsNullOrWhiteSpace(customerName))
            {
                throw new InvalidDataException("PLAN INFO!B4 (Client) must contain a customer name.");
            }

            if (string.IsNullOrWhiteSpace(engagementKey))
            {
                throw new InvalidDataException("PLAN INFO!B5 (Project ID) must contain an engagement identifier.");
            }

            var engagementDescription = ExtractDescription(descriptionRaw);

            var (rankBudgetsFromFile, issues) = ParseResourcing(resourcing);
            var totalBudgetHours = rankBudgetsFromFile.Sum(r => r.hours);

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            var normalizedCustomerName = customerName;
            var normalizedCustomerLookup = normalizedCustomerName.ToLowerInvariant();
            var existingCustomer = await context.Customers
                .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedCustomerLookup);

            bool customerCreated = false;
            Customer customer;
            if (existingCustomer == null)
            {
                customer = new Customer
                {
                    Name = normalizedCustomerName
                };
                await context.Customers.AddAsync(customer);
                customerCreated = true;
            }
            else
            {
                existingCustomer.Name = normalizedCustomerName;
                customer = existingCustomer;
            }

            var engagement = await context.Engagements
                .Include(e => e.RankBudgets)
                .FirstOrDefaultAsync(e => e.EngagementId == engagementKey);

            bool engagementCreated = false;
            if (engagement == null)
            {
                engagement = new Engagement
                {
                    EngagementId = engagementKey,
                    Description = engagementDescription,
                    CustomerKey = normalizedCustomerName,
                    InitialHoursBudget = totalBudgetHours,
                    ActualHours = 0m
                };

                await context.Engagements.AddAsync(engagement);
                engagementCreated = true;
            }
            else
            {
                engagement.Description = engagementDescription;
                engagement.CustomerKey = normalizedCustomerName;
            }

            engagement.InitialHoursBudget = totalBudgetHours;

            if (engagement.RankBudgets == null)
            {
                engagement.RankBudgets = new List<EngagementRankBudget>();
            }

            var now = DateTime.UtcNow;
            var existingBudgets = new Dictionary<string, EngagementRankBudget>(StringComparer.OrdinalIgnoreCase);
            foreach (var budget in engagement.RankBudgets)
            {
                if (!existingBudgets.ContainsKey(budget.RankName))
                {
                    existingBudgets.Add(budget.RankName, budget);
                }
            }

            foreach (var (rankName, hours) in rankBudgetsFromFile)
            {
                if (existingBudgets.TryGetValue(rankName, out var existingBudget))
                {
                    existingBudget.RankName = rankName;
                    existingBudget.Hours = hours;
                    existingBudget.UpdatedAtUtc = now;
                }
                else
                {
                    var budget = new EngagementRankBudget
                    {
                        Engagement = engagement,
                        RankName = rankName,
                        Hours = hours,
                        CreatedAtUtc = now
                    };

                    engagement.RankBudgets.Add(budget);
                }
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            var summary = new StringBuilder();
            summary.Append($"Customer '{customer.Name}' {(customerCreated ? "created" : "upserted")} (Id={customer.Id})");
            summary.Append($", Engagement '{engagement.EngagementId}' {(engagementCreated ? "created" : "upserted")} (Id={engagement.Id})");
            summary.Append($", {rankBudgetsFromFile.Count} rank rows processed, InitialHoursBudget={totalBudgetHours:F2}");

            if (issues.Count > 0)
            {
                summary.Append($". Notes: {string.Join("; ", issues)}");
            }

            return summary.ToString();
        }

        private static (List<(string rank, decimal hours)> rows, List<string> issues) ParseResourcing(DataTable resourcing)
        {
            var rows = new List<(string rank, decimal hours)>();
            var issues = new List<string>();

            var rowIndex = 3; // Row 4 in the worksheet
            var consecutiveBlankRows = 0;

            while (rowIndex < resourcing.Rows.Count || consecutiveBlankRows < 10)
            {
                var rank = NormalizeWhitespace(GetCellString(resourcing, rowIndex, 0));
                var (hours, hasHoursValue) = ParseHours(GetCellValue(resourcing, rowIndex, 8));

                var isRowEmpty = string.IsNullOrEmpty(rank) && !hasHoursValue;

                if (isRowEmpty)
                {
                    consecutiveBlankRows++;

                    if (rowIndex >= resourcing.Rows.Count && consecutiveBlankRows >= 10)
                    {
                        break;
                    }

                    rowIndex++;
                    continue;
                }

                consecutiveBlankRows = 0;

                if (string.IsNullOrEmpty(rank))
                {
                    issues.Add($"Row {rowIndex + 1}: Hours present but rank name missing; skipped.");
                    rowIndex++;
                    continue;
                }

                rows.Add((rank, hours));
                rowIndex++;
            }

            return (rows, issues);
        }

        private static (decimal value, bool hasValue) ParseHours(object? cellValue)
        {
            if (cellValue == null || cellValue == DBNull.Value)
            {
                return (0m, false);
            }

            switch (cellValue)
            {
                case decimal dec:
                    return (dec, true);
                case double dbl:
                    return ((decimal)dbl, true);
                case float flt:
                    return ((decimal)flt, true);
                case int i:
                    return (i, true);
                case long l:
                    return (l, true);
                case short s:
                    return (s, true);
                case string str:
                    var trimmed = str.Trim();
                    if (trimmed.Length == 0)
                    {
                        return (0m, false);
                    }

                    if (decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantParsed))
                    {
                        return (invariantParsed, true);
                    }

                    if (decimal.TryParse(trimmed, NumberStyles.Float, PtBrCulture, out var ptBrParsed))
                    {
                        return (ptBrParsed, true);
                    }

                    throw new InvalidDataException($"Unable to parse hours value '{str}'.");
                default:
                    try
                    {
                        var converted = Convert.ToDecimal(cellValue, CultureInfo.InvariantCulture);
                        return (converted, true);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Unable to parse hours value '{cellValue}'.", ex);
                    }
            }
        }

        private static string ExtractDescription(string rawDescription)
        {
            if (string.IsNullOrEmpty(rawDescription))
            {
                return string.Empty;
            }

            var marker = "Budget-";
            var index = rawDescription.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var value = rawDescription[(index + marker.Length)..];
                return NormalizeWhitespace(value);
            }

            return NormalizeWhitespace(rawDescription);
        }

        private static DataTable? ResolveWorksheet(DataSet dataSet, string worksheetName)
        {
            var target = NormalizeSheetName(worksheetName);

            foreach (DataTable table in dataSet.Tables)
            {
                if (NormalizeSheetName(table.TableName ?? string.Empty) == target)
                {
                    return table;
                }
            }

            return null;
        }

        private static string NormalizeSheetName(string name)
        {
            var normalized = NormalizeWhitespace(name).ToLowerInvariant();
            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

        private static string NormalizeWhitespace(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return MultiWhitespaceRegex.Replace(value.Trim(), " ");
        }

        private static object? GetCellValue(DataTable table, int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || columnIndex < 0)
            {
                return null;
            }

            if (table.Rows.Count <= rowIndex)
            {
                return null;
            }

            if (table.Columns.Count <= columnIndex)
            {
                return null;
            }

            return table.Rows[rowIndex][columnIndex];
        }

        private static string GetCellString(DataTable table, int rowIndex, int columnIndex)
        {
            var value = GetCellValue(table, rowIndex, columnIndex);
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        public async Task<string> ImportActualsAsync(string filePath, int closingPeriodId)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var importBatchId = Guid.NewGuid().ToString();
            int rowsProcessed = 0;
            int engagementsCreated = 0;
            int engagementsUpdated = 0;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var closingPeriod = await context.ClosingPeriods.FindAsync(closingPeriodId);
            if (closingPeriod == null)
            {
                return "Selected closing period could not be found. Please refresh and try again.";
            }

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true
                    }
                });

                var detailTable = dataSet.Tables.Cast<DataTable>().FirstOrDefault(t => t.TableName.ToLowerInvariant().Contains("detail"));

                if (detailTable == null)
                {
                    return "The margin file does not contain a worksheet with 'detail' in its name.";
                }

                var header = detailTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName.Trim()).ToList();
                var engagementIdCol = FindColumn(header, @"(?i)\bengagement\b.*(id|code|#)?");
                var engagementNameCol = FindColumn(header, @"(?i)engagement name");
                var clientIdCol = FindColumn(header, @"(?i)client id");
                var dateCol = FindColumn(header, @"(?i)date|posting date|work date|month|period");
                var hoursCol = FindColumn(header, @"(?i)hours|hrs|qty");

                if (engagementIdCol == -1) return "Could not find Engagement ID column.";
                if (dateCol == -1) return "Could not find Date column.";
                if (hoursCol == -1) return "Could not find Hours column.";

                var engagementHours = new Dictionary<string, double>();

                foreach (DataRow row in detailTable.Rows)
                {
                    var engagementId = row[engagementIdCol]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(engagementId))
                    {
                        continue;
                    }

                    var hours = TryGetDouble(row, detailTable.Columns[hoursCol].ColumnName);
                    if (engagementHours.ContainsKey(engagementId))
                    {
                        engagementHours[engagementId] += hours;
                    }
                    else
                    {
                        engagementHours[engagementId] = hours;
                    }
                }

                foreach (var (engagementId, totalHours) in engagementHours)
                {
                    var engagement = await context.Engagements.FirstOrDefaultAsync(e => e.EngagementId == engagementId);
                    if (engagement == null)
                    {
                        engagement = new Engagement
                        {
                            EngagementId = engagementId,
                            Description = string.Empty,
                            CustomerKey = string.Empty,
                            TotalPlannedHours = 0
                        };

                        await context.Engagements.AddAsync(engagement);
                        await context.SaveChangesAsync();
                        engagementsCreated++;
                    }

                    var actualsEntry = new ActualsEntry
                    {
                        EngagementId = engagement.Id,
                        Date = closingPeriod.PeriodEnd,
                        Hours = totalHours,
                        ImportBatchId = importBatchId,
                        ClosingPeriodId = closingPeriodId
                    };

                    await context.ActualsEntries.AddAsync(actualsEntry);
                    rowsProcessed++;
                }
            }

            await context.SaveChangesAsync();
            return $"Actuals import complete for closing period '{closingPeriod.Name}'. {rowsProcessed} rows processed, {engagementsCreated} engagements created, {engagementsUpdated} updated.";
        }

        private static DateTime? TryGetDate(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName))
            {
                return null;
            }

            var value = row[columnName];
            if (value is DateTime dt)
            {
                return dt;
            }

            if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static double TryGetDouble(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName))
            {
                return 0d;
            }

            var value = row[columnName];
            if (value is double dbl)
            {
                return dbl;
            }

            if (value is float flt)
            {
                return flt;
            }

            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return 0d;
        }

        private int FindColumn(List<string> headers, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return -1;
            }

            var regex = new System.Text.RegularExpressions.Regex(
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

            for (int i = 0; i < headers.Count; i++)
            {
                if (regex.IsMatch(headers[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private List<int> FindWeeklyColumns(List<string> headers)
        {
            var weekCols = new List<int>();
            var regex = new System.Text.RegularExpressions.Regex(@"(?i)\b(week|wk|w\d{2})|(\d{4}-\d{2}-\d{2})|(\d{1,2}\/\d{1,2}\/\d{4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                if (regex.IsMatch(headers[i]))
                {
                    weekCols.Add(i);
                }
            }
            return weekCols;
        }
    }
}