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

    public double ContentSizeRatio { get; init; } = 0.85d;

    public Thickness? ContainerMargin { get; init; }
}
