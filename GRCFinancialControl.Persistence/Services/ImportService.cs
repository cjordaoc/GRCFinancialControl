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
using GRCFinancialControl.Core.Enums;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services
{
    public class ImportService : IImportService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<ImportService> _logger;
        private const string FinancialEvolutionInitialPeriodId = "INITIAL";
        private static readonly Regex MultiWhitespaceRegex = new Regex("\\s+", RegexOptions.Compiled);
        private static readonly Regex DigitsRegex = new Regex("\\d+", RegexOptions.Compiled);
        private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");

        public ImportService(IDbContextFactory<ApplicationDbContext> contextFactory, ILogger<ImportService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
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
                    EtcpHours = 0m
                };

                await context.Engagements.AddAsync(engagement);
                engagementCreated = true;
            }
            else
            {
                engagement.Description = engagementDescription;
                engagement.CustomerKey = normalizedCustomerName;
                engagement.InitialHoursBudget = totalBudgetHours;
            }

            engagement.Customer = customer;
            if (customer.Id > 0)
            {
                engagement.CustomerId = customer.Id;
            }
            engagement.TotalPlannedHours = (double)totalBudgetHours;

            if (engagement.RankBudgets == null)
            {
                engagement.RankBudgets = new List<EngagementRankBudget>();
            }
            else
            {
                // Full replace behavior
                engagement.RankBudgets.Clear();
            }

            var now = DateTime.UtcNow;
            foreach (var (rankName, hours) in rankBudgetsFromFile)
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

            while (rowIndex < resourcing.Rows.Count && consecutiveBlankRows < 10)
            {
                var rank = NormalizeWhitespace(GetCellString(resourcing, rowIndex, 0)); // Column A
                var (hours, hasHoursValue) = ParseHours(GetCellValue(resourcing, rowIndex, 8)); // Column I

                var isRowEmpty = string.IsNullOrEmpty(rank) && !hasHoursValue;

                if (isRowEmpty)
                {
                    consecutiveBlankRows++;
                    rowIndex++;
                    continue;
                }

                consecutiveBlankRows = 0;

                if (string.IsNullOrEmpty(rank))
                {
                    if (hours > 0)
                    {
                        issues.Add($"Row {rowIndex + 1}: Hours present but rank name missing; skipped.");
                    }
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
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("ETC-P workbook could not be found.", filePath);
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var closingPeriod = await context.ClosingPeriods.FindAsync(closingPeriodId);
            if (closingPeriod == null)
            {
                return "Selected closing period could not be found. Please refresh and try again.";
            }

            var customerCache = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
            var engagementCache = new Dictionary<string, Engagement>(StringComparer.OrdinalIgnoreCase);
            var processedEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rowErrors = new List<string>();

            var customersCreated = 0;
            var engagementsCreated = 0;
            var engagementsUpdated = 0;
            var rowsProcessed = 0;

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });

            var etcpTable = ResolveEtcpWorksheet(dataSet);
            if (etcpTable == null)
            {
                return "The ETC-P workbook does not contain the expected worksheet.";
            }

            const int headerRowIndex = 4; // Row 5 in Excel (1-based)
            if (etcpTable.Rows.Count <= headerRowIndex)
            {
                return $"The ETC-P worksheet does not contain any data rows for closing period '{closingPeriod.Name}'.";
            }

            EnsureColumnExists(etcpTable, 2, "Client Name (ID)");
            EnsureColumnExists(etcpTable, 3, "Engagement Name (ID) Currency");
            EnsureColumnExists(etcpTable, 4, "Engagement Status");
            EnsureColumnExists(etcpTable, 8, "Charged Hours Bud");
            EnsureColumnExists(etcpTable, 9, "Charged Hours ETC-P");
            EnsureColumnExists(etcpTable, 11, "TER Bud");
            EnsureColumnExists(etcpTable, 12, "TER ETC-P");
            EnsureColumnExists(etcpTable, 14, "Margin % Bud");
            EnsureColumnExists(etcpTable, 15, "Margin % ETC-P");
            EnsureColumnExists(etcpTable, 17, "Expenses Bud");
            EnsureColumnExists(etcpTable, 18, "Expenses ETC-P");
            EnsureColumnExists(etcpTable, 20, "ETC Age Days");

            for (var rowIndex = headerRowIndex + 1; rowIndex < etcpTable.Rows.Count; rowIndex++)
            {
                var row = etcpTable.Rows[rowIndex];
                var rowNumber = rowIndex + 1; // Excel is 1-based

                try
                {
                    if (IsRowEmpty(row))
                    {
                        continue;
                    }

                    var parsedRow = ParseEtcpRow(row, rowNumber);
                    if (parsedRow == null)
                    {
                        continue;
                    }

                    var (customer, customerCreated) = await GetOrCreateCustomerAsync(context, customerCache, parsedRow);
                    if (customerCreated)
                    {
                        customersCreated++;
                    }

                    UpdateCustomer(customer, parsedRow);

                    var (engagement, engagementCreated) = await GetOrCreateEngagementAsync(context, engagementCache, parsedRow);
                    if (engagementCreated)
                    {
                        engagementsCreated++;
                        processedEngagements.Add(engagement.EngagementId);
                    }
                    else if (processedEngagements.Add(engagement.EngagementId))
                    {
                        engagementsUpdated++;
                    }

                    UpdateEngagement(engagement, customer, parsedRow, closingPeriod);

                    UpsertFinancialEvolution(context, engagement, FinancialEvolutionInitialPeriodId, parsedRow.BudgetHours, parsedRow.BudgetValue, parsedRow.MarginBudget, parsedRow.BudgetExpenses);
                    UpsertFinancialEvolution(context, engagement, closingPeriod.Name, parsedRow.EtcpHours, parsedRow.EtcpValue, parsedRow.MarginEtcp, parsedRow.EtcpExpenses);

                    rowsProcessed++;
                }
                catch (Exception ex)
                {
                    rowErrors.Add($"Row {rowNumber}: {ex.Message}");
                    _logger.LogError(ex, "Failed to import ETC-P row {RowNumber} from file {FilePath}", rowNumber, filePath);
                }
            }

            await context.SaveChangesAsync();

            if (rowsProcessed == 0)
            {
                var emptySummary = new StringBuilder();
                emptySummary.Append($"No ETC-P rows were imported for closing period '{closingPeriod.Name}'.");
                if (rowErrors.Count > 0)
                {
                    emptySummary.Append($" {rowErrors.Count} rows reported issues; review logs for details.");
                }

                return emptySummary.ToString();
            }

            var summaryBuilder = new StringBuilder();
            summaryBuilder.Append($"ETC-P import complete for closing period '{closingPeriod.Name}'.");
            summaryBuilder.Append($" Rows processed: {rowsProcessed}.");
            summaryBuilder.Append($" Customers created: {customersCreated}.");
            summaryBuilder.Append($" Engagements created: {engagementsCreated}.");
            summaryBuilder.Append($" Engagements updated: {engagementsUpdated}.");

            if (rowErrors.Count > 0)
            {
                summaryBuilder.Append($" {rowErrors.Count} rows reported issues; review logs for details.");
            }

            return summaryBuilder.ToString();
        }

        private static DataTable? ResolveEtcpWorksheet(DataSet dataSet)
        {
            foreach (DataTable table in dataSet.Tables)
            {
                if (table.Rows.Count <= 4 || table.Columns.Count <= 3)
                {
                    continue;
                }

                var headerRow = table.Rows[4];
                var clientHeader = NormalizeWhitespace(Convert.ToString(headerRow[2], CultureInfo.InvariantCulture));
                var engagementHeader = NormalizeWhitespace(Convert.ToString(headerRow[3], CultureInfo.InvariantCulture));

                if (clientHeader.Contains("client", StringComparison.OrdinalIgnoreCase) &&
                    engagementHeader.Contains("engagement", StringComparison.OrdinalIgnoreCase))
                {
                    return table;
                }
            }

            return dataSet.Tables.Count > 0 ? dataSet.Tables[0] : null;
        }

        private static void EnsureColumnExists(DataTable table, int columnIndex, string friendlyName)
        {
            if (columnIndex < table.Columns.Count)
            {
                return;
            }

            var columnName = ColumnIndexToName(columnIndex);
            throw new InvalidDataException($"The ETC-P worksheet is missing expected column '{friendlyName}' at position {columnName}.");
        }

        private static string ColumnIndexToName(int columnIndex)
        {
            var dividend = columnIndex + 1;
            var columnName = new StringBuilder();

            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName.Insert(0, Convert.ToChar('A' + modulo));
                dividend = (dividend - modulo) / 26;
            }

            return columnName.ToString();
        }

        private static bool IsRowEmpty(DataRow row)
        {
            foreach (var item in row.ItemArray)
            {
                if (item == null || item == DBNull.Value)
                {
                    continue;
                }

                var text = NormalizeWhitespace(Convert.ToString(item, CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(text))
                {
                    return false;
                }
            }

            return true;
        }

        private EtcpImportRow? ParseEtcpRow(DataRow row, int rowNumber)
        {
            var customerCell = NormalizeWhitespace(Convert.ToString(row[2], CultureInfo.InvariantCulture));
            var engagementCell = NormalizeWhitespace(Convert.ToString(row[3], CultureInfo.InvariantCulture));

            if (string.IsNullOrWhiteSpace(customerCell) && string.IsNullOrWhiteSpace(engagementCell))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(customerCell))
            {
                throw new InvalidDataException("Client Name (ID) is required.");
            }

            if (string.IsNullOrWhiteSpace(engagementCell))
            {
                throw new InvalidDataException("Engagement Name (ID) Currency is required.");
            }

            var (customerName, customerId) = ParseEtcpCustomerCell(customerCell);
            var (engagementDescription, engagementId, currency) = ParseEngagementCell(engagementCell);

            var statusText = NormalizeWhitespace(Convert.ToString(row[4], CultureInfo.InvariantCulture));

            var budgetHours = ParsePtBrNumber(row[8]);
            var etcpHours = ParsePtBrNumber(row[9]);
            var budgetValue = ParsePtBrMoney(row[11]);
            var etcpValue = ParsePtBrMoney(row[12]);
            var marginBudget = ParsePtBrPercent(row[14]);
            var marginEtcp = ParsePtBrPercent(row[15]);
            var budgetExpenses = ParsePtBrMoney(row[17]);
            var etcpExpenses = ParsePtBrMoney(row[18]);
            var ageDays = ParseInt(row[20]);

            return new EtcpImportRow
            {
                RowNumber = rowNumber,
                CustomerName = customerName,
                CustomerId = customerId,
                EngagementDescription = engagementDescription,
                EngagementId = engagementId,
                Currency = currency,
                StatusText = statusText,
                BudgetHours = budgetHours,
                EtcpHours = etcpHours,
                BudgetValue = budgetValue,
                EtcpValue = etcpValue,
                MarginBudget = marginBudget,
                MarginEtcp = marginEtcp,
                BudgetExpenses = budgetExpenses,
                EtcpExpenses = etcpExpenses,
                EtcpAgeDays = ageDays
            };
        }

        private static EngagementStatus ParseStatus(string? statusText)
        {
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return EngagementStatus.Active;
            }

            return statusText.Trim().ToLowerInvariant() switch
            {
                "closing" => EngagementStatus.Closed,
                "closed" => EngagementStatus.Closed,
                "inactive" => EngagementStatus.Inactive,
                _ => EngagementStatus.Active
            };
        }

        private static decimal? ParsePtBrNumber(object? value) => ParseDecimal(value, null);

        private static decimal? ParsePtBrMoney(object? value) => ParseDecimal(value, 2);

        private static decimal? ParsePtBrPercent(object? value)
        {
            var parsed = ParseDecimal(value, 4);
            if (!parsed.HasValue)
            {
                return null;
            }

            var normalized = parsed.Value;
            if (Math.Abs(normalized) <= 1m)
            {
                normalized *= 100m;
            }

            return Math.Round(normalized, 4, MidpointRounding.AwayFromZero);
        }

        private static decimal? ParseDecimal(object? value, int? decimals)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            decimal parsed;
            switch (value)
            {
                case decimal dec:
                    parsed = dec;
                    break;
                case double dbl:
                    parsed = Convert.ToDecimal(dbl);
                    break;
                case float flt:
                    parsed = Convert.ToDecimal(flt);
                    break;
                case int i:
                    parsed = i;
                    break;
                case long l:
                    parsed = l;
                    break;
                case string str:
                    var sanitized = SanitizeNumericString(str);
                    if (sanitized.Length == 0)
                    {
                        return null;
                    }

                    if (!decimal.TryParse(sanitized, NumberStyles.Number | NumberStyles.AllowLeadingSign, PtBrCulture, out parsed) &&
                        !decimal.TryParse(sanitized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsed))
                    {
                        throw new InvalidDataException($"Unable to parse decimal value '{str}'.");
                    }
                    break;
                default:
                    try
                    {
                        parsed = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Unable to parse decimal value '{value}'.", ex);
                    }
                    break;
            }

            if (decimals.HasValue)
            {
                parsed = Math.Round(parsed, decimals.Value, MidpointRounding.AwayFromZero);
            }

            return parsed;
        }

        private static string SanitizeNumericString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sanitized = value
                .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("%", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("\u00A0", string.Empty, StringComparison.Ordinal);

            sanitized = NormalizeWhitespace(sanitized);

            var isNegative = sanitized.StartsWith("(") && sanitized.EndsWith(")");
            if (isNegative)
            {
                sanitized = sanitized[1..^1];
            }

            sanitized = sanitized.Replace(" ", string.Empty);
            sanitized = sanitized.Trim();

            if (isNegative && sanitized.Length > 0)
            {
                sanitized = "-" + sanitized;
            }

            return sanitized;
        }

        private static int? ParseInt(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            switch (value)
            {
                case int i:
                    return i;
                case long l:
                    return (int)l;
                case short s:
                    return s;
                case decimal dec:
                    return (int)Math.Round(dec, MidpointRounding.AwayFromZero);
                case double dbl:
                    return (int)Math.Round(dbl, MidpointRounding.AwayFromZero);
                case float flt:
                    return (int)Math.Round(flt, MidpointRounding.AwayFromZero);
                case string str:
                    var trimmed = NormalizeWhitespace(str);
                    if (trimmed.Length == 0)
                    {
                        return null;
                    }

                    if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var invariantParsed))
                    {
                        return invariantParsed;
                    }

                    if (int.TryParse(trimmed, NumberStyles.Integer, PtBrCulture, out var ptBrParsed))
                    {
                        return ptBrParsed;
                    }

                    throw new InvalidDataException($"Unable to parse integer value '{str}'.");
                default:
                    try
                    {
                        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Unable to parse integer value '{value}'.", ex);
                    }
            }
        }

        private static (string EngagementDisplayName, string EngagementCode, string Currency) ParseEngagementCell(string value)
        {
            var normalized = NormalizeWhitespace(value);
            var match = Regex.Match(normalized, @"\((E-[^)]+)\)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                throw new InvalidDataException($"Engagement code could not be found in '{value}'.");
            }

            var engagementCode = NormalizeWhitespace(match.Groups[1].Value).ToUpperInvariant();
            var displayName = NormalizeWhitespace(normalized[..match.Index]);

            var remainder = NormalizeWhitespace(normalized[(match.Index + match.Length)..]);
            var currency = string.Empty;
            if (!string.IsNullOrEmpty(remainder))
            {
                var tokens = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0)
                {
                    currency = tokens[^1];
                }
            }

            if (string.IsNullOrEmpty(currency))
            {
                throw new InvalidDataException($"Currency could not be determined from '{value}'.");
            }

            return (displayName, engagementCode, currency);
        }

        private static (string CustomerName, string CustomerId) ParseEtcpCustomerCell(string value)
        {
            var normalized = NormalizeWhitespace(value);
            var match = Regex.Match(normalized, @"\(([^)]+)\)\s*$");
            if (!match.Success)
            {
                throw new InvalidDataException($"Client identifier could not be parsed from '{value}'.");
            }

            var digits = DigitsRegex.Match(match.Groups[1].Value);
            if (!digits.Success)
            {
                throw new InvalidDataException($"Client identifier must contain digits in '{value}'.");
            }

            var name = NormalizeWhitespace(normalized[..match.Index]);
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException($"Client name is missing in '{value}'.");
            }

            return (name, digits.Value);
        }

        private static async Task<(Customer customer, bool created)> GetOrCreateCustomerAsync(
            ApplicationDbContext context,
            IDictionary<string, Customer> cache,
            EtcpImportRow row)
        {
            if (cache.TryGetValue(row.CustomerId, out var cachedCustomer))
            {
                return (cachedCustomer, false);
            }

            var customer = await context.Customers
                .FirstOrDefaultAsync(c => c.CustomerID == row.CustomerId);

            var created = false;
            if (customer == null)
            {
                customer = new Customer
                {
                    CustomerID = row.CustomerId,
                    Name = row.CustomerName
                };

                await context.Customers.AddAsync(customer);
                created = true;
            }

            cache[row.CustomerId] = customer;
            return (customer, created);
        }

        private static void UpdateCustomer(Customer customer, EtcpImportRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.CustomerName) &&
                !string.Equals(customer.Name, row.CustomerName, StringComparison.Ordinal))
            {
                customer.Name = row.CustomerName;
            }
        }

        private static async Task<(Engagement engagement, bool created)> GetOrCreateEngagementAsync(
            ApplicationDbContext context,
            IDictionary<string, Engagement> cache,
            EtcpImportRow row)
        {
            if (cache.TryGetValue(row.EngagementId, out var cachedEngagement))
            {
                return (cachedEngagement, false);
            }

            var engagement = await context.Engagements
                .Include(e => e.FinancialEvolutions)
                .FirstOrDefaultAsync(e => e.EngagementId == row.EngagementId);

            var created = false;
            if (engagement == null)
            {
                engagement = new Engagement
                {
                    EngagementId = row.EngagementId
                };

                await context.Engagements.AddAsync(engagement);
                created = true;
            }

            cache[row.EngagementId] = engagement;
            return (engagement, created);
        }

        private static void UpdateEngagement(Engagement engagement, Customer customer, EtcpImportRow row, ClosingPeriod closingPeriod)
        {
            if (!string.IsNullOrWhiteSpace(row.EngagementDescription))
            {
                engagement.Description = row.EngagementDescription;
            }

            engagement.Currency = row.Currency;
            engagement.Customer = customer;
            if (customer.Id > 0)
            {
                engagement.CustomerId = customer.Id;
            }

            engagement.CustomerKey = customer.Name;

            if (!string.IsNullOrWhiteSpace(row.StatusText))
            {
                engagement.StatusText = row.StatusText;
            }

            engagement.Status = ParseStatus(row.StatusText);

            if (row.MarginBudget.HasValue)
            {
                engagement.MarginPctBudget = row.MarginBudget;
                engagement.OpeningMargin = row.MarginBudget.Value;
            }

            if (row.BudgetValue.HasValue)
            {
                engagement.OpeningValue = row.BudgetValue.Value;
            }

            if (row.BudgetExpenses.HasValue)
            {
                engagement.OpeningExpenses = row.BudgetExpenses.Value;
            }

            if (row.BudgetHours.HasValue)
            {
                engagement.InitialHoursBudget = row.BudgetHours.Value;
                engagement.TotalPlannedHours = (double)row.BudgetHours.Value;
            }

            engagement.MarginPctEtcp = row.MarginEtcp;
            engagement.EtcpHours = row.EtcpHours ?? 0m;
            engagement.ValueEtcp = row.EtcpValue ?? 0m;
            engagement.ExpensesEtcp = row.EtcpExpenses ?? 0m;
            engagement.EtcpAgeDays = row.EtcpAgeDays;
            engagement.LatestEtcDate = closingPeriod.PeriodEnd;
            engagement.LastClosingPeriodId = closingPeriod.Name;
            engagement.NextEtcDate = null;
        }

        private static void UpsertFinancialEvolution(
            ApplicationDbContext context,
            Engagement engagement,
            string closingPeriodId,
            decimal? hours,
            decimal? value,
            decimal? margin,
            decimal? expenses)
        {
            var evolution = engagement.FinancialEvolutions
                .FirstOrDefault(fe => string.Equals(fe.ClosingPeriodId, closingPeriodId, StringComparison.OrdinalIgnoreCase));

            if (evolution == null)
            {
                evolution = new FinancialEvolution
                {
                    ClosingPeriodId = closingPeriodId,
                    EngagementId = engagement.EngagementId
                };

                engagement.FinancialEvolutions.Add(evolution);
                context.FinancialEvolutions.Add(evolution);
            }

            evolution.HoursData = hours;
            evolution.ValueData = value;
            evolution.MarginData = margin;
            evolution.ExpenseData = expenses;
        }

        private sealed class EtcpImportRow
        {
            public int RowNumber { get; init; }
            public string CustomerName { get; init; } = string.Empty;
            public string CustomerId { get; init; } = string.Empty;
            public string EngagementDescription { get; init; } = string.Empty;
            public string EngagementId { get; init; } = string.Empty;
            public string Currency { get; init; } = string.Empty;
            public string StatusText { get; init; } = string.Empty;
            public decimal? BudgetHours { get; init; }
            public decimal? EtcpHours { get; init; }
            public decimal? BudgetValue { get; init; }
            public decimal? EtcpValue { get; init; }
            public decimal? MarginBudget { get; init; }
            public decimal? MarginEtcp { get; init; }
            public decimal? BudgetExpenses { get; init; }
            public decimal? EtcpExpenses { get; init; }
            public int? EtcpAgeDays { get; init; }
        }
    }
}