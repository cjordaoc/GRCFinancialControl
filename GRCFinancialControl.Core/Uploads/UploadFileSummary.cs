using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GRCFinancialControl.Uploads
{
    public sealed class UploadFileSummary
    {
        public UploadFileSummary(string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            FileName = Path.GetFileName(filePath);
        }

        public string FilePath { get; }
        public string FileName { get; }
        public UploadOutcome Outcome { get; private set; } = UploadOutcome.Skipped;
        public string StatusText => Outcome switch
        {
            UploadOutcome.Succeeded => "Success",
            UploadOutcome.Failed => "Failed",
            _ => "Skipped"
        };

        public int RowsParsed { get; private set; }
        public int Inserted { get; private set; }
        public int Updated { get; private set; }
        public int Skipped { get; private set; }
        public int Removed { get; private set; }
        public int Duplicates { get; private set; }
        public int WarningCount { get; private set; }
        public int ErrorCount { get; private set; }

        public List<string> Infos { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public string Details
        {
            get
            {
                var parts = new List<string>
                {
                    $"rows={RowsParsed}",
                    $"ins={Inserted}",
                    $"upd={Updated}",
                    $"skip={Skipped}",
                    $"dup={Duplicates}",
                    $"rem={Removed}",
                    $"warn={WarningCount}",
                    $"err={ErrorCount}"
                };

                var message = Infos.Concat(Warnings).Concat(Errors).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    parts.Add(message!);
                }

                return string.Join(" | ", parts);
            }
        }

        public void ApplyParseResult(int rowsParsed, IReadOnlyList<string> warnings, IReadOnlyList<string> errors)
        {
            RowsParsed = rowsParsed;
            WarningCount += warnings?.Count ?? 0;
            ErrorCount += errors?.Count ?? 0;
            if (warnings != null)
            {
                Warnings.AddRange(warnings.Where(w => !string.IsNullOrWhiteSpace(w))!);
            }

            if (errors != null)
            {
                Errors.AddRange(errors.Where(e => !string.IsNullOrWhiteSpace(e))!);
            }
        }

        public void Apply(OperationSummary summary)
        {
            if (summary == null)
            {
                return;
            }

            Inserted = summary.Inserted;
            Updated = summary.Updated;
            Skipped = summary.Skipped;
            Removed = summary.Removed;
            Duplicates = summary.Duplicates;
            WarningCount += summary.WarningMessages.Count;
            ErrorCount += summary.ErrorMessages.Count;

            Infos.AddRange(summary.InfoMessages);
            Warnings.AddRange(summary.WarningMessages);
            Errors.AddRange(summary.ErrorMessages);
        }

        public void MarkSucceeded()
        {
            Outcome = UploadOutcome.Succeeded;
        }

        public void MarkSkipped(string? reason)
        {
            Outcome = UploadOutcome.Skipped;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                Infos.Add(reason);
            }
        }

        public void MarkFailed(Exception exception)
        {
            Outcome = UploadOutcome.Failed;
            var message = exception?.Message ?? "Unknown error";
            Errors.Add(message);
            ErrorCount++;
        }
    }
}
