using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;
using GRCFinancialControl.Parsing;

namespace GRCFinancialControl.Uploads
{
    public sealed class WeeklyDeclarationUploadService
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _sourceId;
        private readonly bool _isErp;
        private readonly string _operationName;

        public WeeklyDeclarationUploadService(AppDbContext db, bool isErp)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ids = new IdResolver(db);
            _isErp = isErp;
            _operationName = isErp ? "ERP Weekly Declaration" : "Retain Weekly Declaration";
            _sourceId = _ids.EnsureSourceSystem(isErp ? "ERP" : "RETAIN", isErp ? "ERP Weekly Allocation" : "Retain Weekly Declaration");
        }

        public OperationSummary Upsert(ushort measurementPeriodId, string engagementId, IReadOnlyList<WeeklyDeclarationRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var summary = new OperationSummary(_operationName);
            if (rows.Count == 0)
            {
                summary.AddInfo("No weekly declaration rows parsed; skipping load.");
                return summary;
            }

            _ids.EnsureEngagement(engagementId);
            var loadUtc = DateTime.UtcNow;
            var prepared = PrepareRows(rows);
            if (prepared.Count == 0)
            {
                summary.AddInfo("No valid weekly declaration rows to process.");
                return summary;
            }

            var uniqueKeys = prepared.Select(r => (r.WeekStart, r.EmployeeId)).ToHashSet();
            var weeks = uniqueKeys.Select(k => k.WeekStart).Distinct().ToList();
            var employees = uniqueKeys.Select(k => k.EmployeeId).Distinct().ToList();

            if (_isErp)
            {
                var existing = _db.FactDeclaredErpWeeks
                    .Where(x => x.EngagementId == engagementId && x.MeasurementPeriodId == measurementPeriodId && weeks.Contains(x.WeekStartDate) && employees.Contains(x.EmployeeId))
                    .ToDictionary(x => (x.WeekStartDate, x.EmployeeId));

                var seen = new HashSet<(DateOnly WeekStart, ulong EmployeeId)>();
                foreach (var row in prepared)
                {
                    if (!seen.Add((row.WeekStart, row.EmployeeId)))
                    {
                        summary.RegisterDuplicate($"Duplicate ERP weekly declaration detected for {row.EmployeeName} - {row.WeekStart:yyyy-MM-dd}.");
                        continue;
                    }

                    if (existing.TryGetValue((row.WeekStart, row.EmployeeId), out var entity))
                    {
                        if (entity.DeclaredHours != row.DeclaredHours)
                        {
                            entity.DeclaredHours = row.DeclaredHours;
                            entity.LoadUtc = loadUtc;
                            entity.CreatedUtc = DateTime.UtcNow;
                            summary.IncrementUpdated();
                        }
                        else
                        {
                            summary.RegisterSkip($"ERP weekly declaration unchanged for {row.EmployeeName} - {row.WeekStart:yyyy-MM-dd}.");
                        }

                        continue;
                    }

                    _db.FactDeclaredErpWeeks.Add(new FactDeclaredErpWeek
                    {
                        SourceSystemId = _sourceId,
                        MeasurementPeriodId = measurementPeriodId,
                        WeekStartDate = row.WeekStart,
                        EngagementId = engagementId,
                        EmployeeId = row.EmployeeId,
                        DeclaredHours = row.DeclaredHours,
                        LoadUtc = loadUtc,
                        CreatedUtc = DateTime.UtcNow
                    });
                    summary.IncrementInserted();
                }
            }
            else
            {
                var existing = _db.FactDeclaredRetainWeeks
                    .Where(x => x.EngagementId == engagementId && x.MeasurementPeriodId == measurementPeriodId && weeks.Contains(x.WeekStartDate) && employees.Contains(x.EmployeeId))
                    .ToDictionary(x => (x.WeekStartDate, x.EmployeeId));

                var seen = new HashSet<(DateOnly WeekStart, ulong EmployeeId)>();
                foreach (var row in prepared)
                {
                    if (!seen.Add((row.WeekStart, row.EmployeeId)))
                    {
                        summary.RegisterDuplicate($"Duplicate Retain declaration detected for {row.EmployeeName} - {row.WeekStart:yyyy-MM-dd}.");
                        continue;
                    }

                    if (existing.TryGetValue((row.WeekStart, row.EmployeeId), out var entity))
                    {
                        if (entity.DeclaredHours != row.DeclaredHours)
                        {
                            entity.DeclaredHours = row.DeclaredHours;
                            entity.LoadUtc = loadUtc;
                            entity.CreatedUtc = DateTime.UtcNow;
                            summary.IncrementUpdated();
                        }
                        else
                        {
                            summary.RegisterSkip($"Retain declaration unchanged for {row.EmployeeName} - {row.WeekStart:yyyy-MM-dd}.");
                        }

                        continue;
                    }

                    _db.FactDeclaredRetainWeeks.Add(new FactDeclaredRetainWeek
                    {
                        SourceSystemId = _sourceId,
                        MeasurementPeriodId = measurementPeriodId,
                        WeekStartDate = row.WeekStart,
                        EngagementId = engagementId,
                        EmployeeId = row.EmployeeId,
                        DeclaredHours = row.DeclaredHours,
                        LoadUtc = loadUtc,
                        CreatedUtc = DateTime.UtcNow
                    });
                    summary.IncrementInserted();
                }
            }

            return summary;
        }

        private List<PreparedWeeklyRow> PrepareRows(IReadOnlyList<WeeklyDeclarationRow> rows)
        {
            var prepared = new List<PreparedWeeklyRow>(rows.Count);
            var cache = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var employeeName = StringNormalizer.TrimToNull(row.EmployeeName);
                if (employeeName == null)
                {
                    continue;
                }

                var normalized = StringNormalizer.NormalizeName(employeeName);
                if (!cache.TryGetValue(normalized, out var employeeId))
                {
                    employeeId = _ids.EnsureEmployee(_sourceId, employeeName);
                    cache[normalized] = employeeId;
                }

                prepared.Add(new PreparedWeeklyRow
                {
                    WeekStart = WeekHelper.ToWeekStart(row.WeekStart),
                    EmployeeId = employeeId,
                    EmployeeName = employeeName,
                    DeclaredHours = row.DeclaredHours
                });
            }

            return prepared;
        }

        private sealed class PreparedWeeklyRow
        {
            public DateOnly WeekStart { get; init; }
            public ulong EmployeeId { get; init; }
            public string EmployeeName { get; init; } = string.Empty;
            public decimal DeclaredHours { get; init; }
        }
    }
}
