using System;
using System.Collections.Generic;
using System.Linq;
using GRCFinancialControl.Common;
using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GRCFinancialControl.Uploads
{
    public sealed class IdResolver
    {
        private readonly MySqlDbContext _db;

        private readonly Dictionary<string, long> _sourceSystemCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _engagementCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _levelAliasCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _levelCodeCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _employeeAliasCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _employeeCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _employeeCodeCache = new(StringComparer.OrdinalIgnoreCase);

        public IdResolver(MySqlDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public long EnsureSourceSystem(string systemCode, string systemName)
        {
            var trimmedCode = StringNormalizer.TrimToNull(systemCode) ?? throw new ArgumentException("System code is required.", nameof(systemCode));
            if (_sourceSystemCache.TryGetValue(trimmedCode, out var cached))
            {
                return cached;
            }

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

            _sourceSystemCache[trimmedCode] = source.SourceSystemId;
            return source.SourceSystemId;
        }

        public string EnsureEngagement(string engagementId, string? title = null)
        {
            var trimmedId = StringNormalizer.TrimToNull(engagementId) ?? throw new ArgumentException("Engagement ID is required.", nameof(engagementId));
            if (_engagementCache.TryGetValue(trimmedId, out var cached))
            {
                return cached;
            }

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

            _engagementCache[trimmedId] = engagement.EngagementId;
            return engagement.EngagementId;
        }

        public long EnsureLevel(long sourceSystemId, string rawLevel, string? levelCodeFallback = null)
        {
            var trimmedLevel = StringNormalizer.TrimToNull(rawLevel) ?? throw new ArgumentException("Level is required.", nameof(rawLevel));
            var normalized = StringNormalizer.NormalizeName(trimmedLevel);
            var aliasKey = BuildAliasKey(sourceSystemId, normalized);

            if (_levelAliasCache.TryGetValue(aliasKey, out var aliasLevelId))
            {
                return aliasLevelId;
            }

            if (_levelCodeCache.TryGetValue(normalized, out var cachedLevelId))
            {
                EnsureAlias(sourceSystemId, trimmedLevel, normalized, cachedLevelId);
                return cachedLevelId;
            }

            var alias = _db.MapLevelAliases.SingleOrDefault(a => a.SourceSystemId == sourceSystemId && a.NormalizedRaw == normalized);
            if (alias != null)
            {
                _levelAliasCache[aliasKey] = alias.LevelId;
                return alias.LevelId;
            }

            var levelCode = levelCodeFallback ?? normalized;
            if (_levelCodeCache.TryGetValue(levelCode, out cachedLevelId))
            {
                EnsureAlias(sourceSystemId, trimmedLevel, normalized, cachedLevelId);
                return cachedLevelId;
            }

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

            _levelCodeCache[levelCode] = level.LevelId;
            EnsureAlias(sourceSystemId, trimmedLevel, normalized, level.LevelId);
            return level.LevelId;
        }

        public long EnsureEmployee(long sourceSystemId, string rawName, string? employeeCode = null)
        {
            var trimmedName = StringNormalizer.TrimToNull(rawName) ?? throw new ArgumentException("Employee name is required.", nameof(rawName));
            var normalized = StringNormalizer.NormalizeName(trimmedName);
            var aliasKey = BuildAliasKey(sourceSystemId, normalized);

            if (_employeeAliasCache.TryGetValue(aliasKey, out var cachedEmployeeId))
            {
                return cachedEmployeeId;
            }

            if (_employeeCache.TryGetValue(normalized, out cachedEmployeeId))
            {
                EnsureEmployeeAlias(sourceSystemId, trimmedName, normalized, cachedEmployeeId);
                var resolvedFromCode = EnsureEmployeeCodeMapping(sourceSystemId, employeeCode, cachedEmployeeId);
                return resolvedFromCode;
            }

            var trimmedCode = StringNormalizer.TrimToNull(employeeCode);
            if (!string.IsNullOrEmpty(trimmedCode))
            {
                var resolvedByCode = TryResolveEmployeeByCode(sourceSystemId, trimmedCode);
                if (resolvedByCode.HasValue)
                {
                    _employeeCache[normalized] = resolvedByCode.Value;
                    EnsureEmployeeAlias(sourceSystemId, trimmedName, normalized, resolvedByCode.Value);
                    return resolvedByCode.Value;
                }
            }

            var alias = _db.MapEmployeeAliases.SingleOrDefault(a => a.SourceSystemId == sourceSystemId && a.NormalizedRaw == normalized);
            if (alias != null)
            {
                _employeeAliasCache[aliasKey] = alias.EmployeeId;
                var resolvedAliasId = EnsureEmployeeCodeMapping(sourceSystemId, trimmedCode, alias.EmployeeId);
                if (resolvedAliasId != alias.EmployeeId)
                {
                    _employeeCache[normalized] = resolvedAliasId;
                    EnsureEmployeeAlias(sourceSystemId, trimmedName, normalized, resolvedAliasId);
                    return resolvedAliasId;
                }

                return alias.EmployeeId;
            }

            var employee = _db.DimEmployees.SingleOrDefault(e => e.NormalizedName == normalized);
            if (employee == null)
            {
                employee = new DimEmployee
                {
                    EmployeeCode = trimmedCode,
                    FullName = trimmedName,
                    NormalizedName = normalized,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                };
                _db.DimEmployees.Add(employee);

                try
                {
                    _db.SaveChanges();
                }
                catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
                {
                    _db.Entry(employee).State = EntityState.Detached;
                    employee = _db.DimEmployees.Single(e => e.NormalizedName == normalized);
                }
            }
            else if (!string.IsNullOrEmpty(trimmedCode) && string.IsNullOrWhiteSpace(employee.EmployeeCode))
            {
                employee.EmployeeCode = trimmedCode;
                employee.UpdatedUtc = DateTime.UtcNow;
            }

            _employeeCache[normalized] = employee.EmployeeId;
            EnsureEmployeeAlias(sourceSystemId, trimmedName, normalized, employee.EmployeeId);
            var resolvedId = EnsureEmployeeCodeMapping(sourceSystemId, trimmedCode, employee.EmployeeId);
            if (resolvedId != employee.EmployeeId)
            {
                _employeeCache[normalized] = resolvedId;
                EnsureEmployeeAlias(sourceSystemId, trimmedName, normalized, resolvedId);
            }

            return resolvedId;
        }

        private long? TryResolveEmployeeByCode(long sourceSystemId, string employeeCode)
        {
            var key = BuildAliasKey(sourceSystemId, employeeCode);
            if (_employeeCodeCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var existing = _db.MapEmployeeCodes.SingleOrDefault(m => m.SourceSystemId == sourceSystemId && m.EmployeeCode == employeeCode);
            if (existing != null)
            {
                _employeeCodeCache[key] = existing.EmployeeId;
                return existing.EmployeeId;
            }

            return null;
        }

        private long EnsureEmployeeCodeMapping(long sourceSystemId, string? employeeCode, long employeeId)
        {
            var trimmed = StringNormalizer.TrimToNull(employeeCode);
            if (trimmed == null)
            {
                return employeeId;
            }

            var key = BuildAliasKey(sourceSystemId, trimmed);
            if (_employeeCodeCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var existing = _db.MapEmployeeCodes.SingleOrDefault(m => m.SourceSystemId == sourceSystemId && m.EmployeeCode == trimmed);
            if (existing != null)
            {
                _employeeCodeCache[key] = existing.EmployeeId;
                return existing.EmployeeId;
            }

            var mapping = new MapEmployeeCode
            {
                SourceSystemId = sourceSystemId,
                EmployeeCode = trimmed,
                EmployeeId = employeeId,
                CreatedUtc = DateTime.UtcNow
            };
            _db.MapEmployeeCodes.Add(mapping);

            try
            {
                _db.SaveChanges();
                _employeeCodeCache[key] = employeeId;
                return employeeId;
            }
            catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
            {
                _db.Entry(mapping).State = EntityState.Detached;
                existing = _db.MapEmployeeCodes.Single(m => m.SourceSystemId == sourceSystemId && m.EmployeeCode == trimmed);
                _employeeCodeCache[key] = existing.EmployeeId;
                return existing.EmployeeId;
            }
        }

        private void EnsureAlias(long sourceSystemId, string trimmedLevel, string normalized, long levelId)
        {
            var aliasKey = BuildAliasKey(sourceSystemId, normalized);
            if (_levelAliasCache.ContainsKey(aliasKey))
            {
                return;
            }

            var newAlias = new MapLevelAlias
            {
                SourceSystemId = sourceSystemId,
                RawLevel = trimmedLevel,
                NormalizedRaw = normalized,
                LevelId = levelId,
                CreatedUtc = DateTime.UtcNow
            };
            _db.MapLevelAliases.Add(newAlias);
            _levelAliasCache[aliasKey] = levelId;
        }

        private void EnsureEmployeeAlias(long sourceSystemId, string trimmedName, string normalized, long employeeId)
        {
            var aliasKey = BuildAliasKey(sourceSystemId, normalized);
            if (_employeeAliasCache.ContainsKey(aliasKey))
            {
                return;
            }

            var newAlias = new MapEmployeeAlias
            {
                SourceSystemId = sourceSystemId,
                RawName = trimmedName,
                NormalizedRaw = normalized,
                EmployeeId = employeeId,
                CreatedUtc = DateTime.UtcNow
            };
            _db.MapEmployeeAliases.Add(newAlias);
            _employeeAliasCache[aliasKey] = employeeId;
        }

        private static string BuildAliasKey(long sourceSystemId, string normalized)
            => $"{sourceSystemId}|{normalized}";

        private static bool IsDuplicateKeyException(DbUpdateException ex)
        {
            if (ex?.InnerException is MySqlException mysql && mysql.Number == 1062)
            {
                return true;
            }

            if (ex?.InnerException?.InnerException is MySqlException nested && nested.Number == 1062)
            {
                return true;
            }

            return false;
        }
    }
}
