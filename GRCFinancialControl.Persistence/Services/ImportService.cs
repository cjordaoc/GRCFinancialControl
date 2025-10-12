using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GRCFinancialControl.Persistence.Services
{
    public class ImportService : IImportService
    {
        private readonly IEngagementService _engagementService;
        private readonly ApplicationDbContext _context;

        public ImportService(IEngagementService engagementService, ApplicationDbContext context)
        {
            _engagementService = engagementService;
            _context = context;
        }

        public async Task<string> ImportBudgetAsync(string filePath)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var engagementsInFile = new Dictionary<string, Engagement>();

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var header = new List<string>();
                    if (reader.Read())
                    {
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            header.Add(reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty);
                        }
                    }

                    var engagementIdCol = FindColumn(header, new[] { "engagement", "engagementid" }, new[] { "id", "code", "#", "number", "num" });
                    var descriptionCol = FindColumn(header, new[] { "description", "engagement name" });
                    var totalHoursCol = FindColumn(header, new[] { "total" }, new[] { "hours" });
                    var weekCols = FindWeeklyColumns(header);

                    if (engagementIdCol == -1) return "Could not find Engagement ID column.";

                    while (reader.Read())
                    {
                        var engagementId = reader.GetValue(engagementIdCol)?.ToString();
                        if (string.IsNullOrEmpty(engagementId)) continue;

                        if (!engagementsInFile.ContainsKey(engagementId))
                        {
                            engagementsInFile[engagementId] = new Engagement
                            {
                                EngagementId = engagementId,
                                Description = descriptionCol != -1 ? reader.GetValue(descriptionCol)?.ToString() ?? string.Empty : string.Empty,
                                TotalPlannedHours = 0
                            };
                        }

                        double totalHours = 0;
                        if (totalHoursCol != -1)
                        {
                            double.TryParse(reader.GetValue(totalHoursCol)?.ToString(), out totalHours);
                        }
                        else if (weekCols.Any())
                        {
                            foreach (var col in weekCols)
                            {
                                if (double.TryParse(reader.GetValue(col)?.ToString(), out var hours))
                                {
                                    totalHours += hours;
                                }
                            }
                        }
                        engagementsInFile[engagementId].TotalPlannedHours += totalHours;
                    }
                }
            }

            foreach (var fileEngagement in engagementsInFile.Values)
            {
                var existingEngagement = await _context.Engagements.FirstOrDefaultAsync(e => e.EngagementId == fileEngagement.EngagementId);
                if (existingEngagement != null)
                {
                    existingEngagement.Description = fileEngagement.Description;
                    existingEngagement.CustomerKey = fileEngagement.CustomerKey;
                    existingEngagement.TotalPlannedHours = fileEngagement.TotalPlannedHours;
                }
                else
                {
                    await _context.Engagements.AddAsync(fileEngagement);
                }
            }

            await _context.SaveChangesAsync();

            return $"Budget import complete. {engagementsInFile.Count} engagements processed.";
        }

        public async Task<string> ImportActualsAsync(string filePath, int closingPeriodId)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var importBatchId = Guid.NewGuid().ToString();
            int rowsProcessed = 0;
            int engagementsCreated = 0;
            int engagementsUpdated = 0;

            var closingPeriod = await _context.ClosingPeriods.FindAsync(closingPeriodId);
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

                var detailTable = dataSet.Tables.Cast<DataTable>().FirstOrDefault(t => t.Columns.Contains("Engagement ID") || t.Columns.Contains("Engagement"));

                if (detailTable == null)
                {
                    return "The margin file does not contain a detail worksheet with the required columns.";
                }

                var header = detailTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName.Trim()).ToList();
                var engagementIdCol = FindColumn(header, new[] { "engagement" }, new[] { "id", "code", "#" });
                var engagementNameCol = FindColumn(header, new[] { "engagement name" });
                var clientIdCol = FindColumn(header, new[] { "client id" });
                var dateCol = FindColumn(header, new[] { "date", "posting date", "work date" });
                var hoursCol = FindColumn(header, new[] { "hours", "hrs", "qty" });

                if (engagementIdCol == -1) return "Could not find Engagement ID column.";
                if (dateCol == -1) return "Could not find Date column.";
                if (hoursCol == -1) return "Could not find Hours column.";

                foreach (DataRow row in detailTable.Rows)
                {
                    var engagementId = row[engagementIdCol]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(engagementId))
                    {
                        continue;
                    }

                    var engagement = await _context.Engagements.FirstOrDefaultAsync(e => e.EngagementId == engagementId);
                    if (engagement == null)
                    {
                        engagement = new Engagement
                        {
                            EngagementId = engagementId,
                            Description = engagementNameCol != -1 ? row[engagementNameCol]?.ToString() ?? string.Empty : string.Empty,
                            CustomerKey = clientIdCol != -1 ? row[clientIdCol]?.ToString() ?? string.Empty : string.Empty,
                            TotalPlannedHours = 0
                        };

                        await _context.Engagements.AddAsync(engagement);
                        await _context.SaveChangesAsync();
                        engagementsCreated++;
                    }
                    else
                    {
                        var updated = false;
                        if (engagementNameCol != -1)
                        {
                            var engagementName = row[engagementNameCol]?.ToString();
                            if (!string.IsNullOrWhiteSpace(engagementName) && !string.Equals(engagement.Description, engagementName, StringComparison.Ordinal))
                            {
                                engagement.Description = engagementName;
                                updated = true;
                            }
                        }

                        if (clientIdCol != -1)
                        {
                            var clientId = row[clientIdCol]?.ToString();
                            if (!string.IsNullOrWhiteSpace(clientId) && !string.Equals(engagement.CustomerKey, clientId, StringComparison.Ordinal))
                            {
                                engagement.CustomerKey = clientId;
                                updated = true;
                            }
                        }

                        if (updated)
                        {
                            engagementsUpdated++;
                        }
                    }

                    var activityDate = TryGetDate(row, detailTable.Columns[dateCol].ColumnName) ?? DateTime.MinValue;
                    var hours = TryGetDouble(row, detailTable.Columns[hoursCol].ColumnName);
                    var papd = engagement.Id == 0 ? null : await _engagementService.GetPapdForDateAsync(engagement.Id, activityDate);

                    var actualsEntry = new ActualsEntry
                    {
                        EngagementId = engagement.Id,
                        Date = activityDate,
                        Hours = hours,
                        ImportBatchId = importBatchId,
                        PapdId = papd?.Id,
                        ClosingPeriodId = closingPeriodId
                    };

                    await _context.ActualsEntries.AddAsync(actualsEntry);
                    rowsProcessed++;
                }
            }

            await _context.SaveChangesAsync();
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

        private int FindColumn(List<string> headers, string[] primaryKeywords, string[] secondaryKeywords = null)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                var header = headers[i].ToLower();
                if (primaryKeywords.Any(key => header.Contains(key)))
                {
                    if (secondaryKeywords == null || secondaryKeywords.Any(key => header.Contains(key)))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private List<int> FindWeeklyColumns(List<string> headers)
        {
            var weekCols = new List<int>();
            var regex = new System.Text.RegularExpressions.Regex(@"(?i)\b(week|wk|w\d{2})|(\d{4}-\d{2}-\d{2})|(\d{1,2}\/\d{1,2}\/\d{4})");
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