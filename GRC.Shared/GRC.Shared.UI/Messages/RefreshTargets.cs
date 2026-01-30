namespace GRC.Shared.UI.Messages;

/// <summary>
/// Provides identifiers for targeted refresh messages across applications.
/// Apps should define their own constants by inheriting or extending this class.
/// </summary>
public static class RefreshTargets
{
    /// <summary>
    /// Generic refresh target for data collection updates.
    /// </summary>
    public const string DataCollection = "RefreshTarget.DataCollection";

    /// <summary>
    /// Generic refresh target for grid or table layout updates.
    /// </summary>
    public const string GridLayout = "RefreshTarget.GridLayout";

    /// <summary>
    /// Generic refresh target for UI state updates.
    /// </summary>
    public const string UiState = "RefreshTarget.UiState";
}
