using System;
using System.Collections.Generic;
using GRCFinancialControl.Data;

namespace GRCFinancialControl.Uploads
{
    public sealed class UploadFileWork
    {
        public UploadFileWork(
            string filePath,
            int rowsParsed,
            IReadOnlyList<string> parseWarnings,
            IReadOnlyList<string> parseErrors,
            Func<AppDbContext, OperationSummary> execute,
            bool disableChangeDetection = true)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            RowsParsed = rowsParsed;
            ParseWarnings = parseWarnings ?? Array.Empty<string>();
            ParseErrors = parseErrors ?? Array.Empty<string>();
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            DisableChangeDetection = disableChangeDetection;
        }

        public string FilePath { get; }
        public int RowsParsed { get; }
        public IReadOnlyList<string> ParseWarnings { get; }
        public IReadOnlyList<string> ParseErrors { get; }
        public Func<AppDbContext, OperationSummary> Execute { get; }
        public bool DisableChangeDetection { get; }
    }
}
