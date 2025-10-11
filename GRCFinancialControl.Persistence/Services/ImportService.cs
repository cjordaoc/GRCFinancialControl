using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;

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
                var existingEngagement = _context.Engagements.FirstOrDefault(e => e.EngagementId == fileEngagement.EngagementId);
                if (existingEngagement != null)
                {
                    existingEngagement.TotalPlannedHours = fileEngagement.TotalPlannedHours;
                    await _engagementService.UpdateAsync(existingEngagement);
                }
                else
                {
                    await _engagementService.AddAsync(fileEngagement);
                }
            }

            return $"Budget import complete. {engagementsInFile.Count} engagements processed.";
        }

        public async Task<string> ImportActualsAsync(string filePath)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var importBatchId = Guid.NewGuid().ToString();
            int rowsProcessed = 0;
            int exceptions = 0;

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    reader.Read(); // Skip header row
                    while (reader.Read())
                    {
                        var engagementId = reader.GetValue(0)?.ToString();
                        if (string.IsNullOrEmpty(engagementId)) continue;

                        var engagement = _context.Engagements.FirstOrDefault(e => e.EngagementId == engagementId);
                        if (engagement == null)
                        {
                            await _context.Exceptions.AddAsync(new ExceptionEntry
                            {
                                SourceFile = Path.GetFileName(filePath),
                                RowData = $"{reader.GetValue(0)}, {reader.GetValue(1)}, {reader.GetValue(2)}",
                                Reason = $"Engagement ID '{engagementId}' not found."
                            });
                            exceptions++;
                            continue;
                        }

                        var date = reader.GetValue(1) != null ? reader.GetDateTime(1) : DateTime.MinValue;
                        var papd = await _engagementService.GetPapdForDateAsync(engagement.Id, date);

                        var actualsEntry = new ActualsEntry
                        {
                            EngagementId = engagement.Id,
                            Date = date,
                            Hours = reader.GetValue(2) != null ? Convert.ToDouble(reader.GetValue(2)) : 0.0,
                            ImportBatchId = importBatchId,
                            PapdId = papd?.Id
                        };

                        await _context.ActualsEntries.AddAsync(actualsEntry);
                        rowsProcessed++;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return $"Actuals import complete. {rowsProcessed} rows processed, {exceptions} exceptions.";
        }
    }
}