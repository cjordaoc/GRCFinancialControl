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
        private readonly MySqlDbContext _db;
        private readonly IdResolver _ids;
        private readonly long _sourceId;
        private readonly bool _isErp;
        private readonly string _operationName;

        public WeeklyDeclarationUploadService(MySqlDbContext db, bool isErp)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ids = new IdResolver(db);
            _isErp = isErp;
            _operationName = isErp ? "ERP Weekly Declaration" : "Retain Weekly Declaration";
            _sourceId = _ids.EnsureSourceSystem(isErp ? "ERP" : "RETAIN", isErp ? "ERP Weekly Allocation" : "Retain Weekly Declaration");
        }

        public OperationSummary Upsert(long measurementPeriodId, IReadOnlyList<WeeklyDeclarationRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var summary = new OperationSummary(_operationName);
            if (rows.Count == 0)
            {
                summary.AddInfo("No weekly declaration rows parsed; skipping load.");
                return summary;
            }

            var loadUtc = DateTime.UtcNow;
            foreach (var group in rows.GroupBy(r => StringNormalizer.TrimToNull(r.EngagementId), StringComparer.OrdinalIgnoreCase))
            {
                var engagementKey = group.Key;
                if (string.IsNullOrWhiteSpace(engagementKey))
                {
                    summary.RegisterSkip("Skipped weekly declaration rows without engagement id.");
                    continue;
                }

                var engagementId = _ids.EnsureEngagement(engagementKey!);
                var prepared = PrepareRows(group.ToList());
                if (prepared.Count == 0)
                {
                    summary.AddInfo($"No valid weekly declaration rows found for engagement {engagementId}.");
                    continue;
                }

                var uniqueKeys = prepared.Select(r => (r.WeekStart, r.EmployeeId)).ToHashSet();
                var weeks = uniqueKeys.Select(k => k.WeekStart).Distinct().ToList();
                var employees = uniqueKeys.Select(k => k.EmployeeId).Distinct().ToList();

                if (_isErp)
                {
                    ProcessWeeklyRows(summary, prepared, () => _db.FactDeclaredErpWeeks
                        .Where(x => x.EngagementId == engagementId && x.MeasurementPeriodId == measurementPeriodId && weeks.Contains(x.WeekStartDate) && employees.Contains(x.EmployeeId))
                        .ToDictionary(x => (x.WeekStartDate, x.EmployeeId)),
                        (row, entity) =>
                        {
                            if (entity != null)
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

                                return;
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
                        });
                }
                else
                {
                    ProcessWeeklyRows(summary, prepared, () => _db.FactDeclaredRetainWeeks
                        .Where(x => x.EngagementId == engagementId && x.MeasurementPeriodId == measurementPeriodId && weeks.Contains(x.WeekStartDate) && employees.Contains(x.EmployeeId))
                        .ToDictionary(x => (x.WeekStartDate, x.EmployeeId)),
                        (row, entity) =>
                        {
                            if (entity != null)
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

                                return;
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
                        });
                }
            }

            return summary;
        }

        private void ProcessWeeklyRows(
            OperationSummary summary,
            List<PreparedWeeklyRow> prepared,
            Func<Dictionary<(DateOnly WeekStart, long EmployeeId), FactDeclaredErpWeek>> erpFetcher,
            Action<PreparedWeeklyRow, FactDeclaredErpWeek?> apply)
        {
            var seen = new HashSet<(DateOnly WeekStart, long EmployeeId)>();
            var existing = erpFetcher();
            foreach (var row in prepared)
            {
                if (!seen.Add((row.WeekStart, row.EmployeeId)))
                {
                    summary.RegisterDuplicate($"Duplicate declaration detected for {row.EmployeeName} - {row.WeekStart:yyyy-MM-dd}.");
                    continue;
                }

                existing.TryGetValue((row.WeekStart, row.EmployeeId), out var entity);
                apply(row, entity);
            }
        }

        private void ProcessWeeklyRows(
            OperationSummary summary,
            List<PreparedWeeklyRow> prepared,
            Func<Dictionary<(DateOnly WeekStart, long EmployeeId), FactDeclaredRetainWeek>> retainFetcher,
            Action<PreparedWeeklyRow, FactDeclaredRetainWeek?> apply)
        {
            var seen = new HashSet<(DateOnly WeekStart, long EmployeeId)>();
            var existing = retainFetcher();
            foreach (var row in prepared)
            {
                if (!seen.Add((row.WeekStart, row.EmployeeId)))
                {
                    summary.RegisterDuplicate($"Duplicate declaration detected for {row.EmployeeName} - {row.WeekStart:yyyy-MM-dd}.");
                    continue;
                }

                existing.TryGetValue((row.WeekStart, row.EmployeeId), out var entity);
                apply(row, entity);
            }
        }

        private List<PreparedWeeklyRow> PrepareRows(IReadOnlyList<WeeklyDeclarationRow> rows)
        {
            var prepared = new List<PreparedWeeklyRow>(rows.Count);
            var cache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

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
            public long EmployeeId { get; init; }
            public string EmployeeName { get; init; } = string.Empty;
            public decimal DeclaredHours { get; init; }
        }
    }
}
