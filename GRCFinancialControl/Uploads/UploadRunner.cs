using System;
using System.Collections.Generic;
using GRCFinancialControl.Data;

namespace GRCFinancialControl.Uploads
{
    public sealed class UploadRunner
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly IUploadLogger _logger;

        public UploadRunner(Func<AppDbContext> contextFactory, IUploadLogger? logger = null)
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
                    var operationSummary = work.Execute(context);
                    if (changeDetectionDisabled)
                    {
                        context.ChangeTracker.DetectChanges();
                    }
                    context.SaveChanges();
                    transaction.Commit();

                    summary.Apply(operationSummary);
                    summary.MarkSucceeded();
                    _logger.LogSuccess(summary);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    summary.MarkFailed(ex);
                    _logger.LogFailure(summary, ex);
                }
                finally
                {
                    context.ChangeTracker.AutoDetectChangesEnabled = originalDetectChanges;
                }

                batch.Add(summary);
            }

            return batch;
        }
    }
}
