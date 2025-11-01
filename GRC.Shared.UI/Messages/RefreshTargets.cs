namespace GRC.Shared.UI.Messages;

/// <summary>
/// Provides shared refresh target identifiers consumed across desktop applications.
/// </summary>
public static class RefreshTargets
{
    /// <summary>
    /// Represents a refresh request scoped to invoice planner invoice lines grid layout.
    /// </summary>
    public const string InvoiceLinesGrid = "InvoicePlanner.InvoiceLinesGrid";

    /// <summary>
    /// Represents a refresh request for GRC Financial Control data collections.
    /// </summary>
    public const string FinancialData = "GRCFinancialControl.Data";
}
