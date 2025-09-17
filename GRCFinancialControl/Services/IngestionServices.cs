// ======================================================================
// Area Financial Control - Synchronous Ingestion & Normalization
// ======================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using GRCFinancialControl.Data;


namespace GRCFinancialControl.Services
{
    public static class StringNormalizer
    {
        public static string NormalizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var trimmed = input.Trim();
            var decomposed = trimmed.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            var cleaned = builder.ToString().Normalize(NormalizationForm.FormC);
            return cleaned.ToUpperInvariant();
        }

        public static string? TrimToNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }

    public sealed class OperationSummary
    {
        public OperationSummary(string operationName)
        {
            OperationName = operationName;
        }

        public string OperationName { get; }
        public int Inserted { get; private set; }
        public int Updated { get; private set; }
        public int Skipped { get; private set; }
        public int Removed { get; private set; }
        public int Duplicates { get; private set; }
        public bool HasErrors { get; private set; }
        public List<string> Messages { get; } = new();

        public void IncrementInserted() => Inserted++;
        public void IncrementUpdated() => Updated++;

        public void RegisterSkip(string message)
        {
            Skipped++;
            AddInfo(message);
        }

        public void RegisterDuplicate(string message)
        {
            Duplicates++;
            AddInfo(message);
        }

        public void RegisterRemoval(string message)
        {
            RegisterRemoval(1, message);
        }

        public void RegisterRemoval(int count, string? message = null)
        {
            if (count <= 0)
            {
                return;
            }

            Removed += count;
            AddInfo(message);
        }

        public void AddInfo(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Messages.Add(message);
            }
        }

        public void MarkError(string message)
        {
            HasErrors = true;
            AddInfo(message);
        }

        public override string ToString()
        {
            return $"{OperationName}: {Inserted} inserted, {Updated} updated, {Skipped} skipped, {Duplicates} duplicates, {Removed} removed.";
        }
    }

    internal static class TransactionHelper
    {
        public static void Finalize(OperationSummary summary, IDbContextTransaction transaction, bool dryRun)
        {
            if (dryRun)
            {
                transaction.Rollback();
                summary.AddInfo("Dry run enabled - transaction rolled back.");
            }
            else
            {
                transaction.Commit();
            }
        }
    }

    public class IdResolver
    {
        private readonly AppDbContext _db;

        public IdResolver(AppDbContext db)
        {
            _db = db;
        }

        public ushort EnsureSourceSystem(string systemCode, string systemName)
        {
            var trimmedCode = StringNormalizer.TrimToNull(systemCode) ?? throw new ArgumentException("System code is required.", nameof(systemCode));
            var source = _db.DimSourceSystems.SingleOrDefault(s => s.SystemCode == trimmedCode);
            if (source == null)
            {
                source = new DimSourceSystem
                {
                    SystemCode = trimmedCode,
                    SystemName = systemName
                };
                _db.DimSourceSystems.Add(source);
                _db.SaveChanges();
            }
            else if (!string.Equals(source.SystemName, systemName, StringComparison.Ordinal))
            {
                source.SystemName = systemName;
                _db.SaveChanges();
            }

            return source.SourceSystemId;
        }

        public string EnsureEngagement(string engagementId, string? title = null)
        {
            var trimmedId = StringNormalizer.TrimToNull(engagementId) ?? throw new ArgumentException("Engagement ID is required.", nameof(engagementId));
            var engagement = _db.DimEngagements.Find(trimmedId);
            if (engagement == null)
            {
                engagement = new DimEngagement
                {
                    EngagementId = trimmedId,
                    EngagementTitle = title ?? trimmedId,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                };
                _db.DimEngagements.Add(engagement);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(title) && !string.Equals(engagement.EngagementTitle, title, StringComparison.Ordinal))
                {
                    engagement.EngagementTitle = title;
                }

                engagement.UpdatedUtc = DateTime.UtcNow;
            }

            _db.SaveChanges();
            return engagement.EngagementId;
        }

        public uint EnsureLevel(ushort sourceSystemId, string rawLevel, string? levelCodeFallback = null)
        {
            var trimmedLevel = StringNormalizer.TrimToNull(rawLevel) ?? throw new ArgumentException("Level is required.", nameof(rawLevel));
            var normalized = StringNormalizer.NormalizeName(trimmedLevel);

            var alias = _db.MapLevelAliases.SingleOrDefault(a => a.SourceSystemId == sourceSystemId && a.NormalizedRaw == normalized);
            if (alias != null)
            {
                return alias.LevelId;
            }

            var levelCode = levelCodeFallback ?? normalized;
            var level = _db.DimLevels.SingleOrDefault(l => l.LevelCode == levelCode);
            if (level == null)
            {
                level = new DimLevel
                {
                    LevelCode = levelCode,
                    LevelName = trimmedLevel,
                    LevelOrder = 0,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                };
                _db.DimLevels.Add(level);
                _db.SaveChanges();
            }

            var newAlias = new MapLevelAlias
            {
                SourceSystemId = sourceSystemId,
                RawLevel = trimmedLevel,
                NormalizedRaw = normalized,
                LevelId = level.LevelId,
                CreatedUtc = DateTime.UtcNow
            };
            _db.MapLevelAliases.Add(newAlias);
            _db.SaveChanges();

            return level.LevelId;
        }

        public ulong EnsureEmployee(ushort sourceSystemId, string rawName, string? employeeCode = null)
        {
            var trimmedName = StringNormalizer.TrimToNull(rawName) ?? throw new ArgumentException("Employee name is required.", nameof(rawName));
            var normalized = StringNormalizer.NormalizeName(trimmedName);

            var alias = _db.MapEmployeeAliases.SingleOrDefault(a => a.SourceSystemId == sourceSystemId && a.NormalizedRaw == normalized);
            if (alias != null)
            {
                return alias.EmployeeId;
            }

            var employee = _db.DimEmployees.SingleOrDefault(e => e.NormalizedName == normalized);
            if (employee == null)
            {
                employee = new DimEmployee
                {
                    EmployeeCode = employeeCode,
                    FullName = trimmedName,
                    NormalizedName = normalized,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                };
                _db.DimEmployees.Add(employee);
                _db.SaveChanges();
            }
            else if (!string.IsNullOrWhiteSpace(employeeCode) && string.IsNullOrWhiteSpace(employee.EmployeeCode))
            {
                employee.EmployeeCode = employeeCode;
                employee.UpdatedUtc = DateTime.UtcNow;
                _db.SaveChanges();
            }

            var newAlias = new MapEmployeeAlias
            {
                SourceSystemId = sourceSystemId,
                RawName = trimmedName,
                NormalizedRaw = normalized,
                EmployeeId = employee.EmployeeId,
                CreatedUtc = DateTime.UtcNow
            };
            _db.MapEmployeeAliases.Add(newAlias);
            _db.SaveChanges();

            return employee.EmployeeId;
        }
    }

    public class PlanLoader
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _sourceId;

        public PlanLoader(AppDbContext db)
        {
            _db = db;
            _ids = new IdResolver(db);
            _sourceId = _ids.EnsureSourceSystem("WEEKLY_PRICING", "Initial Engagement Plan");
        }

        public OperationSummary Summary { get; } = new("Load Plan");

        public OperationSummary Load(string engagementId, IEnumerable<(string RawLevel, decimal PlannedHours, decimal? PlannedRate)> rows, bool dryRun)
        {
            _ids.EnsureEngagement(engagementId);
            using var transaction = _db.Database.BeginTransaction();
            try
            {
                var loadUtc = DateTime.UtcNow;
                foreach (var row in rows)
                {
                    var levelName = StringNormalizer.TrimToNull(row.RawLevel);
                    if (levelName == null)
                    {
                        Summary.RegisterSkip("Skipped row with missing level.");
                        continue;
                    }

                    var levelId = _ids.EnsureLevel(_sourceId, levelName);
                    _db.FactPlanByLevels.Add(new FactPlanByLevel
                    {
                        LoadUtc = loadUtc,
                        SourceSystemId = _sourceId,
                        EngagementId = engagementId,
                        LevelId = levelId,
                        PlannedHours = row.PlannedHours,
                        PlannedRate = row.PlannedRate,
                        CreatedUtc = DateTime.UtcNow
                    });
                    Summary.IncrementInserted();
                }

                _db.SaveChanges();
                TransactionHelper.Finalize(Summary, transaction, dryRun);
                return Summary;
            }
            catch (Exception ex)
            {
                Summary.MarkError(ex.Message);
                transaction.Rollback();
                throw;
            }
        }
    }

    public class EtcLoader
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _sourceId;

        public EtcLoader(AppDbContext db)
        {
            _db = db;
            _ids = new IdResolver(db);
            _sourceId = _ids.EnsureSourceSystem("ETC", "Engagement ETC Snapshot");
        }

        public OperationSummary Summary { get; } = new("Load ETC Snapshot");

        public OperationSummary Load(string snapshotLabel, string engagementId, IEnumerable<(string EmployeeName, string RawLevel, decimal HoursIncurred, decimal EtcRemaining)> rows, decimal? projectedMarginPct, bool dryRun)
        {
            var trimmedLabel = StringNormalizer.TrimToNull(snapshotLabel) ?? throw new ArgumentException("Snapshot label is required.", nameof(snapshotLabel));
            _ids.EnsureEngagement(engagementId);

            using var transaction = _db.Database.BeginTransaction();
            try
            {
                var loadUtc = DateTime.UtcNow;
                foreach (var row in rows)
                {
                    var employeeName = StringNormalizer.TrimToNull(row.EmployeeName);
                    if (employeeName == null)
                    {
                        Summary.RegisterSkip("Skipped row with missing employee name.");
                        continue;
                    }

                    var employeeId = _ids.EnsureEmployee(_sourceId, employeeName);
                    uint? levelId = null;
                    if (!string.IsNullOrWhiteSpace(row.RawLevel))
                    {
                        levelId = _ids.EnsureLevel(_sourceId, row.RawLevel);
                    }

                    _db.FactEtcSnapshots.Add(new FactEtcSnapshot
                    {
                        SnapshotLabel = trimmedLabel,
                        LoadUtc = loadUtc,
                        SourceSystemId = _sourceId,
                        EngagementId = engagementId,
                        EmployeeId = employeeId,
                        LevelId = levelId,
                        HoursIncurred = row.HoursIncurred,
                        EtcRemaining = row.EtcRemaining,
                        CreatedUtc = DateTime.UtcNow
                    });
                    Summary.IncrementInserted();
                }

                if (projectedMarginPct.HasValue)
                {
                    _db.FactEngagementMargins.Add(new FactEngagementMargin
                    {
                        SnapshotLabel = trimmedLabel,
                        LoadUtc = DateTime.UtcNow,
                        SourceSystemId = _sourceId,
                        EngagementId = engagementId,
                        ProjectedMarginPct = projectedMarginPct.Value,
                        CreatedUtc = DateTime.UtcNow
                    });
                    Summary.IncrementInserted();
                }

                _db.SaveChanges();
                TransactionHelper.Finalize(Summary, transaction, dryRun);
                return Summary;
            }
            catch (Exception ex)
            {
                Summary.MarkError(ex.Message);
                transaction.Rollback();
                throw;
            }
        }
    }

    public static class WeekHelper
    {
        public static DateOnly ToWeekStart(DateOnly date)
        {
            var day = (int)date.DayOfWeek;
            var offset = day == 0 ? 6 : day - 1;
            return date.AddDays(-offset);
        }

        public static DateOnly ToWeekEnd(DateOnly date)
        {
            return ToWeekStart(date).AddDays(6);
        }
    }

    public class WeeklyUpsertLoader
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _sourceId;
        private readonly bool _isErp;

        public WeeklyUpsertLoader(AppDbContext db, bool isErp)
        {
            _db = db;
            _ids = new IdResolver(db);
            _isErp = isErp;
            _sourceId = _ids.EnsureSourceSystem(isErp ? "ERP" : "RETAIN", isErp ? "ERP Weekly Allocation" : "Retain Weekly Declaration");
        }

        public OperationSummary Summary { get; } = new("Weekly Declaration Upsert");

        public OperationSummary Upsert(string engagementId, IEnumerable<(DateOnly WeekStart, string EmployeeName, decimal DeclaredHours)> rows, bool dryRun)
        {
            _ids.EnsureEngagement(engagementId);
            using var transaction = _db.Database.BeginTransaction();
            try
            {
                var loadUtc = DateTime.UtcNow;
                var prepared = PrepareRows(rows);
                if (prepared.Count == 0)
                {
                    Summary.AddInfo("No weekly declaration rows to process.");
                    TransactionHelper.Finalize(Summary, transaction, dryRun);
                    return Summary;
                }

                var uniqueKeys = prepared.Select(r => (r.WeekStart, r.EmployeeId)).ToHashSet();
                var weeks = uniqueKeys.Select(k => k.WeekStart).Distinct().ToList();
                var employees = uniqueKeys.Select(k => k.EmployeeId).Distinct().ToList();

                if (_isErp)
                {
                    var existing = _db.FactDeclaredErpWeeks
                        .Where(x => x.EngagementId == engagementId && weeks.Contains(x.WeekStartDate) && employees.Contains(x.EmployeeId))
                        .ToDictionary(x => (x.WeekStartDate, x.EmployeeId));

                    var seen = new HashSet<(DateOnly WeekStart, ulong EmployeeId)>();
                    foreach (var row in prepared)
                    {
                        if (!seen.Add((row.WeekStart, row.EmployeeId)))
                        {
                            Summary.RegisterDuplicate($"Duplicate ERP weekly declaration detected for {row.EmployeeName} - {row.WeekStart:yyyy-MM-dd}.");
                            continue;
                        }

                        if (existing.TryGetValue((row.WeekStart, row.EmployeeId), out var entity))
                        {
                            entity.DeclaredHours = row.DeclaredHours;
                            entity.LoadUtc = loadUtc;
                            Summary.IncrementUpdated();
                        }
                        else
                        {
                            _db.FactDeclaredErpWeeks.Add(new FactDeclaredErpWeek
                            {
                                SourceSystemId = _sourceId,
                                WeekStartDate = row.WeekStart,
                                EngagementId = engagementId,
                                EmployeeId = row.EmployeeId,
                                DeclaredHours = row.DeclaredHours,
                                LoadUtc = loadUtc,
                                CreatedUtc = DateTime.UtcNow
                            });
                            Summary.IncrementInserted();
                        }
                    }
                }
                else
                {
                    var existing = _db.FactDeclaredRetainWeeks
                        .Where(x => x.EngagementId == engagementId && weeks.Contains(x.WeekStartDate) && employees.Contains(x.EmployeeId))
                        .ToDictionary(x => (x.WeekStartDate, x.EmployeeId));

                    var seen = new HashSet<(DateOnly WeekStart, ulong EmployeeId)>();
                    foreach (var row in prepared)
                    {
                        if (!seen.Add((row.WeekStart, row.EmployeeId)))
                        {
                            Summary.RegisterDuplicate($"Duplicate Retain weekly declaration detected for {row.EmployeeName} - {row.WeekStart:yyyy-MM-dd}.");
                            continue;
                        }

                        if (existing.TryGetValue((row.WeekStart, row.EmployeeId), out var entity))
                        {
                            entity.DeclaredHours = row.DeclaredHours;
                            entity.LoadUtc = loadUtc;
                            Summary.IncrementUpdated();
                        }
                        else
                        {
                            _db.FactDeclaredRetainWeeks.Add(new FactDeclaredRetainWeek
                            {
                                SourceSystemId = _sourceId,
                                WeekStartDate = row.WeekStart,
                                EngagementId = engagementId,
                                EmployeeId = row.EmployeeId,
                                DeclaredHours = row.DeclaredHours,
                                LoadUtc = loadUtc,
                                CreatedUtc = DateTime.UtcNow
                            });
                            Summary.IncrementInserted();
                        }
                    }
                }

                _db.SaveChanges();
                TransactionHelper.Finalize(Summary, transaction, dryRun);
                return Summary;
            }
            catch (Exception ex)
            {
                Summary.MarkError(ex.Message);
                transaction.Rollback();
                throw;
            }
        }

        private List<PreparedWeeklyRow> PrepareRows(IEnumerable<(DateOnly WeekStart, string EmployeeName, decimal DeclaredHours)> rows)
        {
            var prepared = new List<PreparedWeeklyRow>();
            var cache = new Dictionary<string, ulong>();

            foreach (var row in rows)
            {
                var employeeName = StringNormalizer.TrimToNull(row.EmployeeName);
                if (employeeName == null)
                {
                    Summary.RegisterSkip("Skipped weekly row with missing employee name.");
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
            public DateOnly WeekStart { get; set; }
            public ulong EmployeeId { get; set; }
            public string EmployeeName { get; set; } = string.Empty;
            public decimal DeclaredHours { get; set; }
        }
    }

    public class ChargesLoader
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _sourceId;

        public ChargesLoader(AppDbContext db)
        {
            _db = db;
            _ids = new IdResolver(db);
            _sourceId = _ids.EnsureSourceSystem("CHARGES", "Daily Timesheet Charges");
        }

        public OperationSummary Summary { get; } = new("Timesheet Charge Insert");

        public OperationSummary Insert(string engagementId, IEnumerable<(DateOnly ChargeDate, string EmployeeName, decimal Hours, decimal? CostAmount)> rows, bool dryRun)
        {
            _ids.EnsureEngagement(engagementId);
            using var transaction = _db.Database.BeginTransaction();
            try
            {
                var prepared = PrepareRows(rows);
                if (prepared.Count == 0)
                {
                    Summary.AddInfo("No charge rows to process.");
                    TransactionHelper.Finalize(Summary, transaction, dryRun);
                    return Summary;
                }

                var keys = prepared.Select(r => (r.ChargeDate, r.EmployeeId)).ToList();
                var uniqueDates = keys.Select(k => k.ChargeDate).Distinct().ToList();
                var uniqueEmployees = keys.Select(k => k.EmployeeId).Distinct().ToList();

                var existing = _db.FactTimesheetCharges
                    .Where(c => c.EngagementId == engagementId && uniqueDates.Contains(c.ChargeDate) && uniqueEmployees.Contains(c.EmployeeId))
                    .ToDictionary(c => (c.ChargeDate, c.EmployeeId));

                var seen = new HashSet<(DateOnly ChargeDate, ulong EmployeeId)>();
                var loadUtc = DateTime.UtcNow;

                foreach (var row in prepared)
                {
                    if (!seen.Add((row.ChargeDate, row.EmployeeId)))
                    {
                        Summary.RegisterDuplicate($"Duplicate charge detected in input for {row.EmployeeName} on {row.ChargeDate:yyyy-MM-dd}.");
                        continue;
                    }

                    if (existing.ContainsKey((row.ChargeDate, row.EmployeeId)))
                    {
                        Summary.RegisterDuplicate($"Charge already exists for {row.EmployeeName} on {row.ChargeDate:yyyy-MM-dd}. Skipped.");
                        continue;
                    }

                    _db.FactTimesheetCharges.Add(new FactTimesheetCharge
                    {
                        SourceSystemId = _sourceId,
                        ChargeDate = row.ChargeDate,
                        EngagementId = engagementId,
                        EmployeeId = row.EmployeeId,
                        HoursCharged = row.Hours,
                        CostAmount = row.CostAmount,
                        LoadUtc = loadUtc,
                        CreatedUtc = DateTime.UtcNow
                    });

                    Summary.IncrementInserted();
                }

                _db.SaveChanges();
                TransactionHelper.Finalize(Summary, transaction, dryRun);
                return Summary;
            }
            catch (Exception ex)
            {
                Summary.MarkError(ex.Message);
                transaction.Rollback();
                throw;
            }
        }

        private List<PreparedChargeRow> PrepareRows(IEnumerable<(DateOnly ChargeDate, string EmployeeName, decimal Hours, decimal? CostAmount)> rows)
        {
            var prepared = new List<PreparedChargeRow>();
            var cache = new Dictionary<string, ulong>();

            foreach (var row in rows)
            {
                var employeeName = StringNormalizer.TrimToNull(row.EmployeeName);
                if (employeeName == null)
                {
                    Summary.RegisterSkip("Skipped charge row with missing employee name.");
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
            public DateOnly ChargeDate { get; set; }
            public ulong EmployeeId { get; set; }
            public string EmployeeName { get; set; } = string.Empty;
            public decimal Hours { get; set; }
            public decimal? CostAmount { get; set; }
        }
    }
    public class ReconciliationService
    {
        private readonly AppDbContext _db;

        public ReconciliationService(AppDbContext db)
        {
            _db = db;
        }

        public OperationSummary Summary { get; } = new("ETC vs Charges Reconciliation");

        public OperationSummary Reconcile(string snapshotLabel, DateOnly lastWeekEnd, bool dryRun)
        {
            var trimmedLabel = StringNormalizer.TrimToNull(snapshotLabel) ?? throw new ArgumentException("Snapshot label is required.", nameof(snapshotLabel));
            var normalizedWeekEnd = WeekHelper.ToWeekEnd(lastWeekEnd);

            using var transaction = _db.Database.BeginTransaction();
            try
            {
                var latestLoads = _db.FactEtcSnapshots
                    .Where(e => e.SnapshotLabel == trimmedLabel)
                    .GroupBy(e => new { e.EngagementId, e.EmployeeId })
                    .Select(g => new { g.Key.EngagementId, g.Key.EmployeeId, LatestLoadUtc = g.Max(x => x.LoadUtc) })
                    .ToList();

                if (latestLoads.Count == 0)
                {
                    Summary.AddInfo($"No ETC snapshots found for label '{trimmedLabel}'.");
                    TransactionHelper.Finalize(Summary, transaction, dryRun);
                    return Summary;
                }

                var etcLatest = (from latest in latestLoads
                                 join snapshot in _db.FactEtcSnapshots on new { latest.EngagementId, latest.EmployeeId, latest.LatestLoadUtc }
                                     equals new { snapshot.EngagementId, snapshot.EmployeeId, LatestLoadUtc = snapshot.LoadUtc }
                                 select new
                                 {
                                     snapshot.EngagementId,
                                     snapshot.EmployeeId,
                                     snapshot.HoursIncurred
                                 }).ToList();

                if (etcLatest.Count == 0)
                {
                    Summary.AddInfo("No ETC detail rows found for the latest loads.");
                    TransactionHelper.Finalize(Summary, transaction, dryRun);
                    return Summary;
                }

                var engagementIds = etcLatest.Select(x => x.EngagementId).Distinct().ToList();
                var employeeIds = etcLatest.Select(x => x.EmployeeId).Distinct().ToList();

                var chargesLookup = _db.FactTimesheetCharges
                    .Where(c => c.ChargeDate <= normalizedWeekEnd && engagementIds.Contains(c.EngagementId) && employeeIds.Contains(c.EmployeeId))
                    .GroupBy(c => new { c.EngagementId, c.EmployeeId })
                    .Select(g => new { g.Key.EngagementId, g.Key.EmployeeId, Hours = g.Sum(x => x.HoursCharged) })
                    .ToDictionary(x => (x.EngagementId, x.EmployeeId), x => x.Hours);

                var existingAudits = _db.AuditEtcVsCharges
                    .Where(a => a.SnapshotLabel == trimmedLabel && a.LastWeekEndDate == normalizedWeekEnd)
                    .ToList();

                var removedCount = 0;
                foreach (var audit in existingAudits)
                {
                    _db.AuditEtcVsCharges.Remove(audit);
                    removedCount++;
                }

                if (removedCount > 0)
                {
                    Summary.RegisterRemoval(removedCount, $"Removed {removedCount} prior audit row(s) for snapshot '{trimmedLabel}'.");
                }

                foreach (var entry in etcLatest)
                {
                    chargesLookup.TryGetValue((entry.EngagementId, entry.EmployeeId), out var chargeHours);
                    var diff = entry.HoursIncurred - chargeHours;

                    if (diff == 0m)
                    {
                        continue;
                    }

                    _db.AuditEtcVsCharges.Add(new AuditEtcVsCharges
                    {
                        SnapshotLabel = trimmedLabel,
                        EngagementId = entry.EngagementId,
                        EmployeeId = entry.EmployeeId,
                        LastWeekEndDate = normalizedWeekEnd,
                        EtcHoursIncurred = entry.HoursIncurred,
                        ChargesSumHours = chargeHours,
                        DiffHours = diff,
                        CreatedUtc = DateTime.UtcNow
                    });

                    Summary.IncrementInserted();
                }

                if (Summary.Inserted == 0)
                {
                    Summary.AddInfo("No variances detected between ETC and charges up to the selected week end.");
                }

                _db.SaveChanges();
                TransactionHelper.Finalize(Summary, transaction, dryRun);
                return Summary;
            }
            catch (Exception ex)
            {
                Summary.MarkError(ex.Message);
                transaction.Rollback();
                throw;
            }
        }
    }

    public class IngestionOrchestrator
    {
        private readonly AppDbContext _db;

        public IngestionOrchestrator(AppDbContext db)
        {
            _db = db;
        }

        public bool DryRun { get; set; }
        public OperationSummary? LastResult { get; private set; }

        public void LoadPlan(string engagementId, IEnumerable<(string RawLevel, decimal PlannedHours, decimal? PlannedRate)> planRows)
        {
            var loader = new PlanLoader(_db);
            try
            {
                LastResult = loader.Load(engagementId, planRows, DryRun);
            }
            catch
            {
                LastResult = loader.Summary;
                throw;
            }
        }

        public void LoadEtc(string snapshotLabel, string engagementId, IEnumerable<(string EmployeeName, string RawLevel, decimal HoursIncurred, decimal EtcRemaining)> etcRows, decimal? projectedMarginPct)
        {
            var loader = new EtcLoader(_db);
            try
            {
                LastResult = loader.Load(snapshotLabel, engagementId, etcRows, projectedMarginPct, DryRun);
            }
            catch
            {
                LastResult = loader.Summary;
                throw;
            }
        }

        public void UpsertErp(string engagementId, IEnumerable<(DateOnly WeekStart, string EmployeeName, decimal DeclaredHours)> rows)
        {
            var loader = new WeeklyUpsertLoader(_db, isErp: true);
            try
            {
                LastResult = loader.Upsert(engagementId, rows, DryRun);
            }
            catch
            {
                LastResult = loader.Summary;
                throw;
            }
        }

        public void UpsertRetain(string engagementId, IEnumerable<(DateOnly WeekStart, string EmployeeName, decimal DeclaredHours)> rows)
        {
            var loader = new WeeklyUpsertLoader(_db, isErp: false);
            try
            {
                LastResult = loader.Upsert(engagementId, rows, DryRun);
            }
            catch
            {
                LastResult = loader.Summary;
                throw;
            }
        }

        public void InsertCharges(string engagementId, IEnumerable<(DateOnly ChargeDate, string EmployeeName, decimal Hours, decimal? CostAmount)> rows)
        {
            var loader = new ChargesLoader(_db);
            try
            {
                LastResult = loader.Insert(engagementId, rows, DryRun);
            }
            catch
            {
                LastResult = loader.Summary;
                throw;
            }
        }

        public void ReconcileEtcVsCharges(string snapshotLabel, DateOnly lastWeekEnd)
        {
            var service = new ReconciliationService(_db);
            try
            {
                LastResult = service.Reconcile(snapshotLabel, lastWeekEnd, DryRun);
            }
            catch
            {
                LastResult = service.Summary;
                throw;
            }
        }
    }
}
