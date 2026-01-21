namespace GRCFinancialControl.Avalonia.Constants;

/// <summary>
/// Defines constants for import file type identifiers used throughout the application.
/// </summary>
public static class ImportTypes
{
    /// <summary>
    /// Budget import type for budget-only workbooks.
    /// </summary>
    public const string Budget = nameof(Budget);

    /// <summary>
    /// Full Management Data import type for comprehensive engagement data.
    /// </summary>
    public const string FullManagement = nameof(FullManagement);

    /// <summary>
    /// Allocation Planning import type for staffing allocation sheets.
    /// </summary>
    public const string AllocationPlanning = nameof(AllocationPlanning);
}
