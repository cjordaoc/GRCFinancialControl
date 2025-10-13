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
        private static readonly Regex MultiWhitespaceRegex = new Regex("\\s+", RegexOptions.Compiled);
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
                    ActualHours = 0m
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
                throw new FileNotFoundException("Margin workbook could not be found.", filePath);
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var closingPeriod = await context.ClosingPeriods.FindAsync(closingPeriodId);
            if (closingPeriod == null)
            {
                return "Selected closing period could not be found. Please refresh and try again.";
            }

            var uploadTimestampUtc = DateTime.UtcNow;
            var customerCache = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
            var engagementCache = new Dictionary<string, Engagement>(StringComparer.OrdinalIgnoreCase);
            var processedEngagements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rowErrors = new List<string>();

            var engagementsCreated = 0;
            var engagementsUpdated = 0;
            var customersCreated = 0;
            var initialMarginEntriesCreated = 0;
            var etcEntriesCreated = 0;

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            var marginTable = ResolveMarginWorksheet(dataSet);
            if (marginTable == null)
            {
                return "The margin file does not contain a worksheet with the expected headers.";
            }

            EnsureColumnExists(marginTable, 0, "Engagement Name (ID) Currency");
            EnsureColumnExists(marginTable, 1, "Client Name (ID)");
            EnsureColumnExists(marginTable, 3, "Margin % Bud");
            EnsureColumnExists(marginTable, 4, "Margin % ETC-P");
            EnsureColumnExists(marginTable, 15, "ETC-P Age (Days)");
            EnsureColumnExists(marginTable, 17, "Status");

            for (var rowIndex = 0; rowIndex < marginTable.Rows.Count; rowIndex++)
            {
                var row = marginTable.Rows[rowIndex];
                var rowNumber = rowIndex + 2; // account for header row

                try
                {
                    if (IsRowEmpty(row))
                    {
                        continue;
                    }

                    var parsedRow = ParseMarginRow(row, rowNumber, uploadTimestampUtc);
                    if (parsedRow == null)
                    {
                        continue;
                    }

                    var (customer, customerCreated) = await GetOrCreateCustomerAsync(context, customerCache, parsedRow);
                    if (customerCreated)
                    {
                        customersCreated++;
                    }
                    else if (string.IsNullOrWhiteSpace(customer.Name) && !string.IsNullOrWhiteSpace(parsedRow.ClientName))
                    {
                        customer.Name = parsedRow.ClientName;
                    }

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

                    if (!string.IsNullOrWhiteSpace(parsedRow.EngagementDisplayName) &&
                        string.IsNullOrWhiteSpace(engagement.Description))
                    {
                        engagement.Description = parsedRow.EngagementDisplayName;
                    }

                    if (!string.IsNullOrWhiteSpace(parsedRow.Currency))
                    {
                        engagement.Currency = parsedRow.Currency;
                    }

                    if (!string.IsNullOrWhiteSpace(parsedRow.StatusText))
                    {
                        engagement.StatusText = parsedRow.StatusText;
                        engagement.Status = ParseStatus(parsedRow.StatusText);
                    }

                    if (parsedRow.MarginPctBudget.HasValue)
                    {
                        engagement.MarginPctBudget = parsedRow.MarginPctBudget;
                        engagement.OpeningMargin = parsedRow.MarginPctBudget.Value;
                    }

                    if (parsedRow.MarginPctEtcp.HasValue)
                    {
                        engagement.MarginPctEtcp = parsedRow.MarginPctEtcp;
                    }

                    if (parsedRow.EtcpAgeDays.HasValue)
                    {
                        engagement.EtcpAgeDays = parsedRow.EtcpAgeDays;
                    }

                    if (parsedRow.LatestEtcDate.HasValue)
                    {
                        engagement.LatestEtcDate = parsedRow.LatestEtcDate;
                    }

                    engagement.NextEtcDate = null;
                    engagement.Customer = customer;
                    if (customer.Id > 0)
                    {
                        engagement.CustomerId = customer.Id;
                    }
                    engagement.CustomerKey = customer.Name;

                    if (parsedRow.MarginPctBudget.HasValue &&
                        !engagement.MarginEvolutions.Any(me => me.EntryType == MarginEvolutionType.InitialBudget))
                    {
                        engagement.MarginEvolutions.Add(new MarginEvolution
                        {
                            EntryType = MarginEvolutionType.InitialBudget,
                            MarginPercentage = parsedRow.MarginPctBudget.Value,
                            CreatedAtUtc = uploadTimestampUtc,
                            EffectiveDate = closingPeriod.PeriodEnd,
                            ClosingPeriodId = closingPeriod.Id
                        });
                        initialMarginEntriesCreated++;
                    }

                    if (parsedRow.MarginPctEtcp.HasValue)
                    {
                        engagement.MarginEvolutions.Add(new MarginEvolution
                        {
                            EntryType = MarginEvolutionType.Etc,
                            MarginPercentage = parsedRow.MarginPctEtcp.Value,
                            CreatedAtUtc = uploadTimestampUtc,
                            EffectiveDate = parsedRow.LatestEtcDate,
                            ClosingPeriodId = closingPeriod.Id
                        });
                        etcEntriesCreated++;
                    }
                }
                catch (Exception ex)
                {
                    rowErrors.Add($"Row {rowNumber}: {ex.Message}");
                    _logger.LogError(ex, "Failed to import row {RowNumber} from file {FilePath}", rowNumber, filePath);
                }
            }

            await context.SaveChangesAsync();

            var summaryBuilder = new StringBuilder();
            summaryBuilder.Append($"Margin import complete for closing period '{closingPeriod.Name}'.");
            summaryBuilder.Append($" {engagementsCreated} engagements created.");
            summaryBuilder.Append($" {engagementsUpdated} engagements updated.");
            summaryBuilder.Append($" {customersCreated} customers created.");
            summaryBuilder.Append($" {initialMarginEntriesCreated + etcEntriesCreated} margin history records added ({initialMarginEntriesCreated} initial, {etcEntriesCreated} ETC).");

            if (rowErrors.Count > 0)
            {
                summaryBuilder.Append($" {rowErrors.Count} rows reported issues; review logs for details.");
            }

            return summaryBuilder.ToString();
        }

        private static DataTable? ResolveMarginWorksheet(DataSet dataSet)
        {
            foreach (DataTable table in dataSet.Tables)
            {
                if (table.Columns.Count == 0)
                {
                    continue;
                }

                var firstHeader = NormalizeWhitespace(table.Columns[0].ColumnName);
                if (firstHeader.Contains("engagement", StringComparison.OrdinalIgnoreCase))
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
            throw new InvalidDataException($"The margin worksheet is missing expected column '{friendlyName}' at position {columnName}.");
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

        private MarginImportRow? ParseMarginRow(DataRow row, int rowNumber, DateTime uploadTimestampUtc)
        {
            var engagementCell = NormalizeWhitespace(Convert.ToString(row[0], CultureInfo.InvariantCulture));
            if (string.IsNullOrWhiteSpace(engagementCell))
            {
                return null;
            }

            var clientCell = NormalizeWhitespace(Convert.ToString(row[1], CultureInfo.InvariantCulture));
            if (string.IsNullOrWhiteSpace(clientCell))
            {
                _logger.LogWarning("Row {RowNumber}: Client information is missing. Skipping row.", rowNumber);
                return null;
            }

            var (engagementDisplayName, engagementCode, currency) = ParseEngagementCell(engagementCell);
            var (clientName, clientIdText) = ParseCustomerCell(clientCell);

            var marginBudget = ParsePercentage(row[3]);
            var marginEtcp = ParsePercentage(row[4]);
            var etcAgeDays = ParseInt(row[15]);
            var statusText = NormalizeWhitespace(Convert.ToString(row[17], CultureInfo.InvariantCulture));

            DateTime? latestEtcDate = null;
            if (etcAgeDays.HasValue)
            {
                latestEtcDate = uploadTimestampUtc.Date.AddDays(-etcAgeDays.Value);
            }

            return new MarginImportRow
            {
                RowNumber = rowNumber,
                EngagementCode = engagementCode,
                EngagementDisplayName = engagementDisplayName,
                Currency = currency,
                ClientName = clientName,
                ClientIdText = clientIdText,
                MarginPctBudget = marginBudget,
                MarginPctEtcp = marginEtcp,
                EtcpAgeDays = etcAgeDays,
                LatestEtcDate = latestEtcDate,
                StatusText = statusText
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

        private static decimal? ParsePercentage(object? value)
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
                    parsed = (decimal)dbl;
                    break;
                case float flt:
                    parsed = (decimal)flt;
                    break;
                case int i:
                    parsed = i;
                    break;
                case long l:
                    parsed = l;
                    break;
                case string str:
                    var trimmed = NormalizeWhitespace(str);
                    if (trimmed.Length == 0)
                    {
                        return null;
                    }

                    if (!decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
                        !decimal.TryParse(trimmed, NumberStyles.Float, PtBrCulture, out parsed))
                    {
                        throw new InvalidDataException($"Unable to parse percentage value '{str}'.");
                    }
                    break;
                default:
                    try
                    {
                        parsed = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Unable to parse percentage value '{value}'.", ex);
                    }
                    break;
            }

            return NormalizePercentage(parsed);
        }

        private static decimal NormalizePercentage(decimal value)
        {
            if (value <= 1m && value >= -1m)
            {
                value *= 100m;
            }

            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
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

        private static (string ClientName, string ClientIdText) ParseCustomerCell(string value)
        {
            var normalized = NormalizeWhitespace(value);
            var match = Regex.Match(normalized, @"\(([^)]+)\)\s*$");
            if (!match.Success)
            {
                throw new InvalidDataException($"Client identifier could not be found in '{value}'.");
            }

            var idText = NormalizeWhitespace(match.Groups[1].Value);
            if (string.IsNullOrEmpty(idText))
            {
                throw new InvalidDataException($"Client identifier is empty in '{value}'.");
            }

            var name = NormalizeWhitespace(normalized[..match.Index]);
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException($"Client name is empty in '{value}'.");
            }

            return (name, idText);
        }

        private static void CacheCustomer(IDictionary<string, Customer> cache, MarginImportRow row, Customer customer)
        {
            static void TryCache(IDictionary<string, Customer> cache, string? key, Customer customer)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    cache[key.Trim()] = customer;
                }
            }

            TryCache(cache, row.ClientIdText, customer);
            TryCache(cache, row.ClientName, customer);
            TryCache(cache, customer.ClientIdText, customer);
            TryCache(cache, customer.Name, customer);
        }

        private static async Task<(Customer customer, bool created)> GetOrCreateCustomerAsync(
            ApplicationDbContext context,
            IDictionary<string, Customer> cache,
            MarginImportRow row)
        {
            var normalizedName = string.IsNullOrWhiteSpace(row.ClientName)
                ? null
                : row.ClientName.Trim();

            if (!string.IsNullOrWhiteSpace(row.ClientIdText) && cache.TryGetValue(row.ClientIdText, out var cachedById))
            {
                return (cachedById, false);
            }

            if (!string.IsNullOrWhiteSpace(normalizedName) && cache.TryGetValue(normalizedName, out var cachedByName))
            {
                return (cachedByName, false);
            }

            Customer? existingCustomer = null;

            if (!string.IsNullOrWhiteSpace(row.ClientIdText))
            {
                existingCustomer = await context.Customers
                    .FirstOrDefaultAsync(c => c.ClientIdText == row.ClientIdText);
            }

            if (existingCustomer == null && !string.IsNullOrWhiteSpace(normalizedName))
            {
                existingCustomer = await context.Customers
                    .FirstOrDefaultAsync(c => c.Name == normalizedName);
            }

            if (existingCustomer != null)
            {
                if (string.IsNullOrWhiteSpace(existingCustomer.ClientIdText) &&
                    !string.IsNullOrWhiteSpace(row.ClientIdText))
                {
                    existingCustomer.ClientIdText = row.ClientIdText;
                }

                if (string.IsNullOrWhiteSpace(existingCustomer.Name) && !string.IsNullOrWhiteSpace(normalizedName))
                {
                    existingCustomer.Name = normalizedName;
                }

                CacheCustomer(cache, row, existingCustomer);
                return (existingCustomer, false);
            }

            var customer = new Customer
            {
                Name = normalizedName ?? string.Empty,
                ClientIdText = row.ClientIdText
            };

            await context.Customers.AddAsync(customer);
            CacheCustomer(cache, row, customer);
            return (customer, true);
        }

        private static async Task<(Engagement engagement, bool created)> GetOrCreateEngagementAsync(
            ApplicationDbContext context,
            IDictionary<string, Engagement> cache,
            MarginImportRow row)
        {
            if (cache.TryGetValue(row.EngagementCode, out var cachedEngagement))
            {
                return (cachedEngagement, false);
            }

            var engagement = await context.Engagements
                .Include(e => e.MarginEvolutions)
                .FirstOrDefaultAsync(e => e.EngagementId == row.EngagementCode);

            if (engagement != null)
            {
                cache[row.EngagementCode] = engagement;
                return (engagement, false);
            }

            engagement = new Engagement
            {
                EngagementId = row.EngagementCode,
                Description = row.EngagementDisplayName,
                Currency = row.Currency,
                CustomerKey = row.ClientName,
                MarginPctBudget = row.MarginPctBudget,
                MarginPctEtcp = row.MarginPctEtcp,
                EtcpAgeDays = row.EtcpAgeDays,
                LatestEtcDate = row.LatestEtcDate,
                StatusText = string.IsNullOrWhiteSpace(row.StatusText) ? null : row.StatusText,
                NextEtcDate = null,
                OpeningMargin = row.MarginPctBudget ?? 0,
                Status = ParseStatus(row.StatusText)
            };

            await context.Engagements.AddAsync(engagement);
            cache[row.EngagementCode] = engagement;
            return (engagement, true);
        }

        private sealed class MarginImportRow
        {
            public int RowNumber { get; init; }
            public string EngagementCode { get; init; } = string.Empty;
            public string EngagementDisplayName { get; init; } = string.Empty;
            public string Currency { get; init; } = string.Empty;
            public string ClientName { get; init; } = string.Empty;
            public string ClientIdText { get; init; } = string.Empty;
            public decimal? MarginPctBudget { get; init; }
            public decimal? MarginPctEtcp { get; init; }
            public int? EtcpAgeDays { get; init; }
            public DateTime? LatestEtcDate { get; init; }
            public string StatusText { get; init; } = string.Empty;
        }

        private static List<int> FindWeeklyColumns(DataTable resourcing)
        {
            var headerRowIndex = 2;
            if (resourcing.Rows.Count <= headerRowIndex)
            {
                return new List<int>();
            }

            var weeklyColumns = new List<int>();
            var headerRow = resourcing.Rows[headerRowIndex];
            var weekRegex = new Regex(@"(?i)^(W\d{1,2}|Week\s*\d{1,2})$");

            for (var i = 0; i < headerRow.ItemArray.Length; i++)
            {
                var header = NormalizeWhitespace(Convert.ToString(headerRow[i], CultureInfo.InvariantCulture));
                if (weekRegex.IsMatch(header))
                {
                    weeklyColumns.Add(i);
                }
            }

            return weeklyColumns;
        }
    }
}