using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Models;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Services
{
    /// <summary>
    /// Validates and corrects fiscal calendar inconsistencies across fiscal years and closing periods.
    /// </summary>
    public class FiscalCalendarConsistencyService : IFiscalCalendarConsistencyService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<FiscalCalendarConsistencyService> _logger;

        public FiscalCalendarConsistencyService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FiscalCalendarConsistencyService> logger)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            ArgumentNullException.ThrowIfNull(logger);

            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<FiscalCalendarValidationSummary> EnsureConsistencyAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var fiscalYears = await context.FiscalYears
                .Include(fy => fy.ClosingPeriods)
                .OrderBy(fy => fy.StartDate)
                .ToListAsync()
                .ConfigureAwait(false);

            var issuesBefore = ValidateFiscalYears(fiscalYears);
            var correctionResult = ApplyCorrections(fiscalYears);

            if (correctionResult.CorrectionsApplied > 0)
            {
                await context.SaveChangesAsync().ConfigureAwait(false);
            }

            var issuesAfter = ValidateFiscalYears(fiscalYears);

            var summary = new FiscalCalendarValidationSummary(
                fiscalYears.Count,
                fiscalYears.Sum(fy => fy.ClosingPeriods.Count),
                correctionResult.CorrectionsApplied,
                issuesBefore,
                issuesAfter,
                correctionResult.Corrections);

            LogSummary(summary);

            return summary;
        }

        private static List<FiscalYearValidationReport> ValidateFiscalYears(IEnumerable<FiscalYear> fiscalYears)
        {
            var reports = new List<FiscalYearValidationReport>();

            foreach (var fiscalYear in fiscalYears)
            {
                var issues = ValidateFiscalYear(fiscalYear);
                if (issues.Count > 0)
                {
                    reports.Add(new FiscalYearValidationReport(fiscalYear.Id, fiscalYear.Name, issues));
                }
            }

            return reports;
        }

        private static List<string> ValidateFiscalYear(FiscalYear fiscalYear)
        {
            var issues = new List<string>();
            var fiscalStart = fiscalYear.StartDate.Date;
            var fiscalEnd = fiscalYear.EndDate.Date;

            var periods = fiscalYear.ClosingPeriods
                .OrderBy(cp => cp.PeriodStart)
                .ToList();

            if (periods.Count == 0)
            {
                issues.Add("No closing periods configured.");
                return issues;
            }

            var expectedStart = fiscalStart;

            foreach (var period in periods)
            {
                if (period.FiscalYearId != fiscalYear.Id)
                {
                    issues.Add($"Period '{period.Name}' references fiscal year Id {period.FiscalYearId}.");
                }

                var startDate = period.PeriodStart.Date;
                var endDate = period.PeriodEnd.Date;

                if (startDate < fiscalStart)
                {
                    issues.Add($"Period '{period.Name}' starts before fiscal year start ({startDate:yyyy-MM-dd} < {fiscalStart:yyyy-MM-dd}).");
                }

                if (endDate > fiscalEnd)
                {
                    issues.Add($"Period '{period.Name}' ends after fiscal year end ({endDate:yyyy-MM-dd} > {fiscalEnd:yyyy-MM-dd}).");
                }

                if (startDate > endDate)
                {
                    issues.Add($"Period '{period.Name}' has start after end ({startDate:yyyy-MM-dd} > {endDate:yyyy-MM-dd}).");
                }

                if (startDate != expectedStart)
                {
                    var relation = startDate > expectedStart ? "gap" : "overlap";
                    issues.Add($"Detected {relation} before '{period.Name}': expected {expectedStart:yyyy-MM-dd}, found {startDate:yyyy-MM-dd}.");
                }

                expectedStart = endDate.AddDays(1);
            }

            var lastPeriod = periods[^1];
            var lastEnd = lastPeriod.PeriodEnd.Date;
            if (lastEnd != fiscalEnd)
            {
                issues.Add($"Last period '{lastPeriod.Name}' ends on {lastEnd:yyyy-MM-dd}, fiscal year ends on {fiscalEnd:yyyy-MM-dd}.");
            }

            return issues;
        }

        private static FiscalCalendarCorrectionResult ApplyCorrections(IEnumerable<FiscalYear> fiscalYears)
        {
            var corrections = new List<string>();
            var correctionCount = 0;

            foreach (var fiscalYear in fiscalYears)
            {
                var periods = fiscalYear.ClosingPeriods
                    .OrderBy(cp => cp.PeriodStart)
                    .ToList();

                if (periods.Count == 0)
                {
                    continue;
                }

                var fiscalStart = fiscalYear.StartDate.Date;
                var fiscalEnd = fiscalYear.EndDate.Date;
                var nextExpectedStart = fiscalStart;

                foreach (var period in periods)
                {
                    if (period.FiscalYearId != fiscalYear.Id)
                    {
                        corrections.Add($"Reassigned period '{period.Name}' to fiscal year '{fiscalYear.Name}'.");
                        period.FiscalYearId = fiscalYear.Id;
                        correctionCount++;
                    }

                    var originalStart = period.PeriodStart.Date;
                    var originalEnd = period.PeriodEnd.Date;

                    var adjustedStart = nextExpectedStart;
                    if (adjustedStart < fiscalStart)
                    {
                        adjustedStart = fiscalStart;
                    }

                    if (adjustedStart > fiscalEnd)
                    {
                        adjustedStart = fiscalEnd;
                    }

                    var adjustedEnd = originalEnd;
                    if (adjustedEnd < adjustedStart)
                    {
                        adjustedEnd = adjustedStart;
                    }

                    if (adjustedEnd > fiscalEnd)
                    {
                        adjustedEnd = fiscalEnd;
                    }

                    var startChanged = originalStart != adjustedStart;
                    var endChanged = originalEnd != adjustedEnd;

                    if (startChanged || endChanged)
                    {
                        corrections.Add($"Adjusted period '{period.Name}': start {originalStart:yyyy-MM-dd} -> {adjustedStart:yyyy-MM-dd}, end {originalEnd:yyyy-MM-dd} -> {adjustedEnd:yyyy-MM-dd}.");
                        period.PeriodStart = adjustedStart;
                        period.PeriodEnd = adjustedEnd;
                        correctionCount++;
                    }
                    else
                    {
                        period.PeriodStart = adjustedStart;
                        period.PeriodEnd = adjustedEnd;
                    }

                    nextExpectedStart = period.PeriodEnd.Date.AddDays(1);
                    if (nextExpectedStart > fiscalEnd)
                    {
                        nextExpectedStart = fiscalEnd;
                    }
                }

                var finalPeriod = periods[^1];
                var finalEnd = finalPeriod.PeriodEnd.Date;
                if (finalEnd != fiscalEnd)
                {
                    corrections.Add($"Extended last period '{finalPeriod.Name}' to fiscal year end {fiscalEnd:yyyy-MM-dd} (was {finalEnd:yyyy-MM-dd}).");
                    finalPeriod.PeriodEnd = fiscalEnd;
                    if (finalPeriod.PeriodStart.Date > fiscalEnd)
                    {
                        finalPeriod.PeriodStart = fiscalEnd;
                    }

                    correctionCount++;
                }
            }

            return new FiscalCalendarCorrectionResult(correctionCount, corrections);
        }

        private void LogSummary(FiscalCalendarValidationSummary summary)
        {
            _logger.LogInformation(
                "Fiscal calendar consistency check processed {FiscalYears} fiscal years ({ClosingPeriods} closing periods). Corrections applied: {Corrections}. Remaining issues: {RemainingIssues}.",
                summary.FiscalYearsProcessed,
                summary.ClosingPeriodsProcessed,
                summary.CorrectionsApplied,
                summary.IssuesAfter.Count);

            if (summary.CorrectionsLog.Count > 0)
            {
                foreach (var correction in summary.CorrectionsLog)
                {
                    _logger.LogDebug("{Correction}", correction);
                }
            }

            foreach (var report in summary.IssuesAfter)
            {
                foreach (var issue in report.Issues)
                {
                    _logger.LogWarning(
                        "Fiscal year {FiscalYear} inconsistency: {Issue}",
                        report.FiscalYearName,
                        issue);
                }
            }
        }

        private sealed record FiscalCalendarCorrectionResult(int CorrectionsApplied, IReadOnlyList<string> Corrections);
    }
}
