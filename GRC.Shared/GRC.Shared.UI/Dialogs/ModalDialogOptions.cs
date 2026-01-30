using Avalonia;

namespace GRC.Shared.UI.Dialogs;

public enum ModalDialogLayout
{
    CenteredOverlay,
    OwnerAligned
}

public sealed class ModalDialogOptions
{
    public ModalDialogLayout Layout { get; init; } = ModalDialogLayout.CenteredOverlay;

    public string? Title { get; init; }

    public bool ShowWindowControls { get; init; } = false;

    public bool DimBackground { get; init; } = true;

    public bool FreezeOwner { get; init; } = true;

    public string? SizeRatioResourceKey { get; init; } = "DialogContentRatioStandard";

    public double ContentSizeRatio { get; init; } = 0.85d;

    public Thickness? ContainerMargin { get; init; }
}
