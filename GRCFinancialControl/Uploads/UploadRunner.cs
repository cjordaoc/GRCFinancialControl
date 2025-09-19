using System;
using System.Collections.Generic;
using GRCFinancialControl.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GRCFinancialControl.Uploads
{
    public sealed class UploadRunner
    {
        private readonly Func<MySqlDbContext> _contextFactory;
        private readonly IUploadLogger _logger;

        public UploadRunner(Func<MySqlDbContext> contextFactory, IUploadLogger? logger = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? NullUploadLogger.Instance;
        }

        public UploadBatchSummary Run(IEnumerable<UploadFileWork> works)
        {
            ArgumentNullException.ThrowIfNull(works);

            var batch = new UploadBatchSummary();
            foreach (var work in works)
            {
                if (work == null)
                {
                    continue;
                }

                var summary = new UploadFileSummary(work.FilePath);
                summary.ApplyParseResult(work.RowsParsed, work.ParseWarnings, work.ParseErrors);

                if (work.RowsParsed == 0)
                {
                    summary.MarkSkipped("No parsed rows available.");
                    _logger.LogSkipped(summary);
                    batch.Add(summary);
                    continue;
                }

                _logger.LogStart(summary);

                var operationSummary = default(OperationSummary);
                IExecutionStrategy executionStrategy;
                using (var strategyContext = _contextFactory())
                {
                    executionStrategy = strategyContext.Database.CreateExecutionStrategy();
                }

                try
                {
                    executionStrategy.Execute(() =>
                    {
                        using var context = _contextFactory();
                        var originalDetectChanges = context.ChangeTracker.AutoDetectChangesEnabled;
                        var changeDetectionDisabled = work.DisableChangeDetection && originalDetectChanges;
                        if (changeDetectionDisabled)
                        {
                            context.ChangeTracker.AutoDetectChangesEnabled = false;
                        }

                        using var transaction = context.Database.BeginTransaction();
                        try
                        {
                            operationSummary = work.Execute(context);
                            if (changeDetectionDisabled)
                            {
                                context.ChangeTracker.DetectChanges();
                            }

                            context.SaveChanges();
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                        finally
                        {
                            context.ChangeTracker.AutoDetectChangesEnabled = originalDetectChanges;
                        }
                    });

                    if (operationSummary != null)
                    {
                        summary.Apply(operationSummary);
                    }
                    summary.MarkSucceeded();
                    _logger.LogSuccess(summary);
                }
                catch (Exception ex)
                {
                    summary.MarkFailed(ex);
                    _logger.LogFailure(summary, ex);
                }

                batch.Add(summary);
            }

            return batch;
        }
    }
}
