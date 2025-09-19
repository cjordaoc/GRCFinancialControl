using System;
using System.Collections.Generic;

namespace GRCFinancialControl.Uploads
{
    public sealed class OperationSummary
    {
        public OperationSummary(string operationName)
        {
            OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
        }

        public string OperationName { get; }
        public int Inserted { get; private set; }
        public int Updated { get; private set; }
        public int Skipped { get; private set; }
        public int Removed { get; private set; }
        public int Duplicates { get; private set; }
        public bool HasErrors { get; private set; }

        public List<string> InfoMessages { get; } = new();
        public List<string> WarningMessages { get; } = new();
        public List<string> ErrorMessages { get; } = new();

        public void IncrementInserted() => Inserted++;
        public void IncrementUpdated() => Updated++;

        public void RegisterSkip(string? message = null)
        {
            Skipped++;
            AddWarning(message);
        }

        public void RegisterDuplicate(string? message = null)
        {
            Duplicates++;
            AddWarning(message);
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

        public void RegisterRemoval(string? message)
        {
            RegisterRemoval(1, message);
        }

        public void AddInfo(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                InfoMessages.Add(message);
            }
        }

        public void AddWarning(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                WarningMessages.Add(message);
            }
        }

        public void AddError(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                ErrorMessages.Add(message);
            }
        }

        public void MarkError(string message)
        {
            HasErrors = true;
            AddError(message);
        }

        public override string ToString()
        {
            return $"{OperationName}: {Inserted} inserted, {Updated} updated, {Skipped} skipped, {Duplicates} duplicates, {Removed} removed.";
        }
    }
}
