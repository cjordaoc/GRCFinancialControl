using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;
using GRCFinancialControl.Parsing;

namespace GRCFinancialControl.Uploads
{
    public sealed class ChargesUploadService
    {
        private readonly MySqlDbContext _db;
        private readonly IdResolver _ids;
        private readonly long _sourceId;

        public ChargesUploadService(MySqlDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ids = new IdResolver(db);
            _sourceId = _ids.EnsureSourceSystem("CHARGES", "Daily Timesheet Charges");
        }

        public OperationSummary Insert(long measurementPeriodId, IReadOnlyList<ChargeRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var summary = new OperationSummary("Timesheet Charge Insert");
            if (rows.Count == 0)
            {
                summary.AddInfo("No charge rows parsed; skipping load.");
                return summary;
            }

            var prepared = PrepareRows(rows, summary);
            if (prepared.Count == 0)
            {
                summary.AddInfo("No valid charge rows to insert.");
                return summary;
            }

            var loadUtc = DateTime.UtcNow;
            foreach (var group in prepared.GroupBy(r => r.EngagementId, StringComparer.OrdinalIgnoreCase))
            {
                var engagementId = _ids.EnsureEngagement(group.Key);
                var groupRows = group.ToList();
                var keys = groupRows.Select(r => (r.ChargeDate, r.EmployeeId)).ToList();
                var uniqueDates = keys.Select(k => k.ChargeDate).Distinct().ToList();
                var uniqueEmployees = keys.Select(k => k.EmployeeId).Distinct().ToList();

                var existing = _db.FactTimesheetCharges
                    .Where(c => c.EngagementId == engagementId && c.MeasurementPeriodId == measurementPeriodId && uniqueDates.Contains(c.ChargeDate) && uniqueEmployees.Contains(c.EmployeeId))
                    .ToDictionary(c => (c.ChargeDate, c.EmployeeId));

                var seen = new HashSet<(DateOnly ChargeDate, long EmployeeId)>();

                foreach (var row in groupRows)
                {
                    if (!seen.Add((row.ChargeDate, row.EmployeeId)))
                    {
                        summary.RegisterDuplicate($"Duplicate charge detected in input for {row.EmployeeName} on {row.ChargeDate:yyyy-MM-dd}.");
                        continue;
                    }

                    if (existing.ContainsKey((row.ChargeDate, row.EmployeeId)))
                    {
                        summary.RegisterDuplicate($"Charge already exists for {row.EmployeeName} on {row.ChargeDate:yyyy-MM-dd}. Skipped.");
                        continue;
                    }

                    _db.FactTimesheetCharges.Add(new FactTimesheetCharge
                    {
                        SourceSystemId = _sourceId,
                        MeasurementPeriodId = measurementPeriodId,
                        ChargeDate = row.ChargeDate,
                        EngagementId = engagementId,
                        EmployeeId = row.EmployeeId,
                        HoursCharged = row.Hours,
                        CostAmount = row.CostAmount,
                        LoadUtc = loadUtc,
                        CreatedUtc = DateTime.UtcNow
                    });

                    summary.IncrementInserted();
                }
            }

            return summary;
        }

        private List<PreparedChargeRow> PrepareRows(IReadOnlyList<ChargeRow> rows, OperationSummary summary)
        {
            var prepared = new List<PreparedChargeRow>(rows.Count);
            var cache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var engagementId = StringNormalizer.TrimToNull(row.EngagementId);
                if (engagementId == null)
                {
                    summary.RegisterSkip("Skipped charge row with missing engagement id.");
                    continue;
                }

                var employeeName = StringNormalizer.TrimToNull(row.EmployeeName);
                if (employeeName == null)
                {
                    summary.RegisterSkip("Skipped charge row with missing employee name.");
                    continue;
                }

                var normalized = StringNormalizer.NormalizeName(employeeName);
                if (!cache.TryGetValue(normalized, out var employeeId))
                {
                    employeeId = _ids.EnsureEmployee(_sourceId, employeeName);
                    cache[normalized] = employeeId;
                }

                prepared.Add(new PreparedChargeRow
                {
                    EngagementId = engagementId,
                    ChargeDate = row.ChargeDate,
                    EmployeeId = employeeId,
                    EmployeeName = employeeName,
                    Hours = row.Hours,
                    CostAmount = row.CostAmount
                });
            }

            return prepared;
        }

        private sealed class PreparedChargeRow
        {
            public string EngagementId { get; init; } = string.Empty;
            public DateOnly ChargeDate { get; init; }
            public long EmployeeId { get; init; }
            public string EmployeeName { get; init; } = string.Empty;
            public decimal Hours { get; init; }
            public decimal? CostAmount { get; init; }
        }
    }
}
