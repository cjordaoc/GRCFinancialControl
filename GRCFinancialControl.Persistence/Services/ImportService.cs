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
                    reader.Read(); // Skip header row
                    while (reader.Read())
                    {
                        var engagementId = reader.GetValue(0)?.ToString();
                        if (string.IsNullOrEmpty(engagementId)) continue;

                        if (!engagementsInFile.ContainsKey(engagementId))
                        {
                            engagementsInFile[engagementId] = new Engagement
                            {
                                EngagementId = engagementId,
                                Description = reader.GetValue(1)?.ToString() ?? string.Empty,
                                CustomerKey = reader.GetValue(2)?.ToString() ?? string.Empty,
                                TotalPlannedHours = 0
                            };
                        }

                        if (reader.GetValue(3) != null && double.TryParse(reader.GetValue(3).ToString(), out var hours))
                        {
                            engagementsInFile[engagementId].TotalPlannedHours += hours;
                        }
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

                var detailTable = dataSet.Tables.Cast<DataTable>()
                    .FirstOrDefault(t => t.Columns.Contains("Engagement ID") && t.Columns.Contains("Charged Hours / Quantity"));

                if (detailTable == null)
                {
                    return "The margin file does not contain a detail worksheet with the required columns.";
                }

                foreach (DataRow row in detailTable.Rows)
                {
                    var engagementId = row["Engagement ID"]?.ToString()?.Trim();
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
                            Description = row["Engagement Name"]?.ToString() ?? string.Empty,
                            CustomerKey = row.Table.Columns.Contains("Client ID") ? row["Client ID"]?.ToString() ?? string.Empty : string.Empty,
                            TotalPlannedHours = 0
                        };

                        await _context.Engagements.AddAsync(engagement);
                        await _context.SaveChangesAsync();
                        engagementsCreated++;
                    }
                    else
                    {
                        var updated = false;
                        var engagementName = row["Engagement Name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(engagementName) && !string.Equals(engagement.Description, engagementName, StringComparison.Ordinal))
                        {
                            engagement.Description = engagementName;
                            updated = true;
                        }

                        if (row.Table.Columns.Contains("Client ID"))
                        {
                            var clientId = row["Client ID"]?.ToString();
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

                    var activityDate = TryGetDate(row, "Week Ending Date") ?? TryGetDate(row, "Transaction Date") ?? DateTime.MinValue;
                    var hours = TryGetDouble(row, "Charged Hours / Quantity");
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
    }
}