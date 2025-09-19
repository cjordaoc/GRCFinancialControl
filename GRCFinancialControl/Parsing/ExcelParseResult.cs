using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Parsing
{
    public class ExcelParseResult<TRow>
    {
        private readonly List<TRow> _rows = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        public ExcelParseResult(string operationName)
        {
            OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
        }

        public string OperationName { get; }
        public IReadOnlyList<TRow> Rows => _rows;
        public IReadOnlyList<string> Errors => _errors;
        public IReadOnlyList<string> Warnings => _warnings;
        public int Skipped { get; private set; }

        public void AddRow(TRow row) => _rows.Add(row);

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _errors.Add(message);
            }
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _warnings.Add(message);
            }
        }

        public void IncrementSkipped(string message)
        {
            Skipped++;
            AddWarning(message);
        }

        public string BuildSummary()
        {
            return $"{OperationName}: {Rows.Count} rows parsed, {Skipped} skipped, {Warnings.Count} warnings, {Errors.Count} errors.";
        }
    }
}
