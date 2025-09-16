// ======================================================================
// Area Financial Control - Synchronous Ingestion Services
// No async/await. ClosedXML or ExcelDataReader can be used for parsing.
// ======================================================================
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using AreaFinancialControl.Data;

namespace AreaFinancialControl.Ingestion
{
    public static class StringNormalizer
    {
        public static string NormalizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var trimmed = input.Trim();
            var deAccented = RemoveDiacritics(trimmed);
            return deAccented.ToUpperInvariant();
        }

        public static string RemoveDiacritics(string text)
        {
            var formD = text.Normalize(System.Text.NormalizationForm.FormD);
            var filtered = new System.Text.StringBuilder();
            foreach (var ch in formD)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    filtered.Append(ch);
            }
            return filtered.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        public static string? TrimToNull(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim();
            return t.Length == 0 ? null : t;
        }
    }

    public class IdResolver
    {
        private readonly AppDbContext _db;
        public IdResolver(AppDbContext db) { _db = db; }

        public ushort EnsureSourceSystem(string systemCode, string systemName)
        {
            var sys = _db.DimSourceSystems.SingleOrDefault(s => s.SystemCode == systemCode);
            if (sys == null)
            {
                sys = new DimSourceSystem { SystemCode = systemCode, SystemName = systemName };
                _db.DimSourceSystems.Add(sys);
                _db.SaveChanges();
            }
            return sys.SourceSystemId;
        }

        public string EnsureEngagement(string engagementId, string? title = null)
        {
            if (string.IsNullOrWhiteSpace(engagementId)) throw new ArgumentException("Engagement ID is required.");
            var e = _db.DimEngagements.Find(engagementId);
            if (e == null)
            {
                e = new DimEngagement { EngagementId = engagementId, EngagementTitle = title ?? engagementId };
                _db.DimEngagements.Add(e);
                _db.SaveChanges();
            }
            return e.EngagementId;
        }

        public uint EnsureLevel(ushort sourceSystemId, string rawLevel, string? levelCodeFallback = null)
        {
            var normalized = StringNormalizer.NormalizeName(rawLevel);
            var alias = _db.MapLevelAliases.SingleOrDefault(a => a.SourceSystemId == sourceSystemId && a.NormalizedRaw == normalized);
            if (alias != null) return alias.LevelId;

            // Try level by code fallback (e.g., "ANALYST")
            uint levelId;
            var code = levelCodeFallback ?? normalized;
            var level = _db.DimLevels.SingleOrDefault(l => l.LevelCode == code);
            if (level == null)
            {
                level = new DimLevel { LevelCode = code, LevelName = rawLevel, LevelOrder = 0 };
                _db.DimLevels.Add(level);
                _db.SaveChanges();
            }
            levelId = level.LevelId;

            _db.MapLevelAliases.Add(new MapLevelAlias
            {
                SourceSystemId = sourceSystemId,
                RawLevel = rawLevel,
                NormalizedRaw = normalized,
                LevelId = levelId
            });
            _db.SaveChanges();
            return levelId;
        }

        public ulong EnsureEmployee(ushort sourceSystemId, string rawName, string? employeeCode = null)
        {
            var normalizedRaw = StringNormalizer.NormalizeName(rawName);
            var alias = _db.MapEmployeeAliases.SingleOrDefault(a => a.SourceSystemId == sourceSystemId && a.NormalizedRaw == normalizedRaw);
            if (alias != null) return alias.EmployeeId;

            // Find or create employee by normalized name
            var emp = _db.DimEmployees.SingleOrDefault(e => e.NormalizedName == normalizedRaw);
            if (emp == null)
            {
                emp = new DimEmployee
                {
                    EmployeeCode = employeeCode,
                    FullName = rawName.Trim(),
                    NormalizedName = normalizedRaw
                };
                _db.DimEmployees.Add(emp);
                _db.SaveChanges();
            }

            // Create alias
            _db.MapEmployeeAliases.Add(new MapEmployeeAlias
            {
                SourceSystemId = sourceSystemId,
                RawName = rawName,
                NormalizedRaw = normalizedRaw,
                EmployeeId = emp.EmployeeId
            });
            _db.SaveChanges();

            return emp.EmployeeId;
        }
    }

    public class PlanLoader
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _srcId;

        public PlanLoader(AppDbContext db)
        {
            _db = db;
            _ids = new IdResolver(db);
            _srcId = _ids.EnsureSourceSystem("WEEKLY_PRICING", "Initial Engagement Plan");
        }

        public void Load(string engagementId, IEnumerable<(string RawLevel, decimal PlannedHours, decimal? PlannedRate)> rows)
        {
            if (string.IsNullOrWhiteSpace(engagementId)) return;
            _ids.EnsureEngagement(engagementId);

            var loadUtc = DateTime.UtcNow;
            foreach (var r in rows)
            {
                var levelId = _ids.EnsureLevel(_srcId, r.RawLevel);
                var fact = new FactPlanByLevel
                {
                    LoadUtc = loadUtc,
                    SourceSystemId = _srcId,
                    EngagementId = engagementId,
                    LevelId = levelId,
                    PlannedHours = r.PlannedHours,
                    PlannedRate = r.PlannedRate
                };
                _db.FactPlanByLevels.Add(fact);
            }
            _db.SaveChanges();
        }
    }

    public class EtcLoader
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _srcId;

        public EtcLoader(AppDbContext db)
        {
            _db = db;
            _ids = new IdResolver(db);
            _srcId = _ids.EnsureSourceSystem("ETC", "Engagement ETC Snapshot");
        }

        public void Load(string snapshotLabel, string engagementId,
                         IEnumerable<(string EmployeeName, string RawLevel, decimal HoursIncurred, decimal EtcRemaining)> rows,
                         decimal? projectedMarginPct)
        {
            if (string.IsNullOrWhiteSpace(engagementId)) return;
            _ids.EnsureEngagement(engagementId);

            var loadUtc = DateTime.UtcNow;

            foreach (var r in rows)
            {
                var empId = _ids.EnsureEmployee(_srcId, r.EmployeeName);
                uint? levelId = null;
                if (!string.IsNullOrWhiteSpace(r.RawLevel))
                {
                    levelId = _ids.EnsureLevel(_srcId, r.RawLevel);
                }

                var fact = new FactEtcSnapshot
                {
                    SnapshotLabel = snapshotLabel,
                    LoadUtc = loadUtc,
                    SourceSystemId = _srcId,
                    EngagementId = engagementId,
                    EmployeeId = empId,
                    LevelId = levelId,
                    HoursIncurred = r.HoursIncurred,
                    EtcRemaining = r.EtcRemaining
                };
                _db.FactEtcSnapshots.Add(fact);
            }

            if (projectedMarginPct.HasValue)
            {
                _db.FactEngagementMargins.Add(new FactEngagementMargin
                {
                    SnapshotLabel = snapshotLabel,
                    LoadUtc = loadUtc,
                    SourceSystemId = _srcId,
                    EngagementId = engagementId,
                    ProjectedMarginPct = projectedMarginPct.Value
                });
            }

            _db.SaveChanges();
        }
    }

    public static class WeekHelper
    {
        // Return Monday for a given date
        public static DateOnly ToWeekStart(DateOnly d)
        {
            var dayOfWeek = (int)d.DayOfWeek; // Monday=1 ... Sunday=0 in .NET? Actually: Sunday=0
            var offset = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
            return d.AddDays(-offset);
        }
    }

    public class WeeklyUpsertLoader
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _srcId;
        private readonly bool _isErp;

        public WeeklyUpsertLoader(AppDbContext db, bool isErp)
        {
            _db = db;
            _ids = new IdResolver(db);
            _isErp = isErp;
            _srcId = _ids.EnsureSourceSystem(isErp ? "ERP" : "RETAIN", isErp ? "ERP Weekly Allocation" : "Retain Weekly Declaration");
        }

        public void Upsert(string engagementId, IEnumerable<(DateOnly WeekStart, string EmployeeName, decimal DeclaredHours)> rows)
        {
            _ids.EnsureEngagement(engagementId);
            var loadUtc = DateTime.UtcNow;

            foreach (var r in rows)
            {
                var empId = _ids.EnsureEmployee(_srcId, r.EmployeeName);
                var w = WeekHelper.ToWeekStart(r.WeekStart);

                if (_isErp)
                {
                    var existing = _db.FactDeclaredErpWeeks
                        .SingleOrDefault(x => x.WeekStartDate == w && x.EngagementId == engagementId && x.EmployeeId == empId);
                    if (existing == null)
                    {
                        _db.FactDeclaredErpWeeks.Add(new FactDeclaredErpWeek
                        {
                            SourceSystemId = _srcId,
                            WeekStartDate = w,
                            EngagementId = engagementId,
                            EmployeeId = empId,
                            DeclaredHours = r.DeclaredHours,
                            LoadUtc = loadUtc
                        });
                    }
                    else
                    {
                        existing.DeclaredHours = r.DeclaredHours;
                        existing.LoadUtc = loadUtc;
                    }
                }
                else
                {
                    var existing = _db.FactDeclaredRetainWeeks
                        .SingleOrDefault(x => x.WeekStartDate == w && x.EngagementId == engagementId && x.EmployeeId == empId);
                    if (existing == null)
                    {
                        _db.FactDeclaredRetainWeeks.Add(new FactDeclaredRetainWeek
                        {
                            SourceSystemId = _srcId,
                            WeekStartDate = w,
                            EngagementId = engagementId,
                            EmployeeId = empId,
                            DeclaredHours = r.DeclaredHours,
                            LoadUtc = loadUtc
                        });
                    }
                    else
                    {
                        existing.DeclaredHours = r.DeclaredHours;
                        existing.LoadUtc = loadUtc;
                    }
                }
            }
            _db.SaveChanges();
        }
    }

    public class ChargesLoader
    {
        private readonly AppDbContext _db;
        private readonly IdResolver _ids;
        private readonly ushort _srcId;

        public ChargesLoader(AppDbContext db)
        {
            _db = db;
            _ids = new IdResolver(db);
            _srcId = _ids.EnsureSourceSystem("CHARGES", "Daily Timesheet Charges");
        }

        // Idempotent insert by (date, engagement, employee)
        public void Insert(string engagementId, IEnumerable<(DateOnly ChargeDate, string EmployeeName, decimal Hours, decimal? CostAmount)> rows)
        {
            _ids.EnsureEngagement(engagementId);
            var loadUtc = DateTime.UtcNow;

            foreach (var r in rows)
            {
                var empId = _ids.EnsureEmployee(_srcId, r.EmployeeName);
                var exists = _db.FactTimesheetCharges
                    .SingleOrDefault(x => x.ChargeDate == r.ChargeDate && x.EngagementId == engagementId && x.EmployeeId == empId);
                if (exists == null)
                {
                    _db.FactTimesheetCharges.Add(new FactTimesheetCharge
                    {
                        SourceSystemId = _srcId,
                        ChargeDate = r.ChargeDate,
                        EngagementId = engagementId,
                        EmployeeId = empId,
                        HoursCharged = r.Hours,
                        CostAmount = r.CostAmount,
                        LoadUtc = loadUtc
                    });
                }
                else
                {
                    // If duplicate found, keep the first (idempotent). Optionally, reconcile hours differences here.
                }
            }
            _db.SaveChanges();
        }
    }

    public class ReconciliationService
    {
        private readonly AppDbContext _db;
        public ReconciliationService(AppDbContext db) { _db = db; }

        // Compare latest ETC snapshot for a label vs. sum of charges up to lastWeekEnd (inclusive)
        public void Reconcile(string snapshotLabel, DateOnly lastWeekEnd)
        {
            // Gather latest ETC per emp for label
            var etcLatest = _db.FactEtcSnapshots
                .Where(e => e.SnapshotLabel == snapshotLabel)
                .GroupBy(e => new { e.EngagementId, e.EmployeeId })
                .Select(g => new
                {
                    g.Key.EngagementId,
                    g.Key.EmployeeId,
                    HoursIncurred = g.OrderByDescending(x => x.LoadUtc).First().HoursIncurred
                })
                .ToList();

            foreach (var e in etcLatest)
            {
                var chargesSum = _db.FactTimesheetCharges
                    .Where(c => c.EngagementId == e.EngagementId
                             && c.EmployeeId == e.EmployeeId
                             && c.ChargeDate <= lastWeekEnd)
                    .Sum(c => (decimal?)c.HoursCharged) ?? 0m;

                var diff = e.HoursIncurred - chargesSum;
                if (diff != 0m)
                {
                    _db.AuditEtcVsCharges.Add(new AuditEtcVsCharges
                    {
                        SnapshotLabel = snapshotLabel,
                        EngagementId = e.EngagementId,
                        EmployeeId = e.EmployeeId,
                        LastWeekEndDate = lastWeekEnd,
                        EtcHoursIncurred = e.HoursIncurred,
                        ChargesSumHours = chargesSum,
                        DiffHours = diff
                    });
                }
            }
            _db.SaveChanges();
        }
    }

    // Example orchestrator you can call from WinForms button handlers
    public class IngestionOrchestrator
    {
        private readonly AppDbContext _db;
        public IngestionOrchestrator(AppDbContext db) { _db = db; }

        public void LoadPlan(string engagementId, IEnumerable<(string RawLevel, decimal PlannedHours, decimal? PlannedRate)> planRows)
        {
            new PlanLoader(_db).Load(engagementId, planRows);
        }

        public void LoadEtc(string snapshotLabel, string engagementId,
                            IEnumerable<(string EmployeeName, string RawLevel, decimal HoursIncurred, decimal EtcRemaining)> etcRows,
                            decimal? projectedMarginPct)
        {
            new EtcLoader(_db).Load(snapshotLabel, engagementId, etcRows, projectedMarginPct);
        }

        public void UpsertErp(string engagementId, IEnumerable<(DateOnly WeekStart, string EmployeeName, decimal DeclaredHours)> rows)
        {
            new WeeklyUpsertLoader(_db, isErp: true).Upsert(engagementId, rows);
        }

        public void UpsertRetain(string engagementId, IEnumerable<(DateOnly WeekStart, string EmployeeName, decimal DeclaredHours)> rows)
        {
            new WeeklyUpsertLoader(_db, isErp: false).Upsert(engagementId, rows);
        }

        public void InsertCharges(string engagementId, IEnumerable<(DateOnly ChargeDate, string EmployeeName, decimal Hours, decimal? CostAmount)> rows)
        {
            new ChargesLoader(_db).Insert(engagementId, rows);
        }

        public void ReconcileEtcVsCharges(string snapshotLabel, DateOnly lastWeekEnd)
        {
            new ReconciliationService(_db).Reconcile(snapshotLabel, lastWeekEnd);
        }
    }
}
