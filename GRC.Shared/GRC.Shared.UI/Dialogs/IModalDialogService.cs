using Avalonia.Controls;

namespace GRC.Shared.UI.Dialogs;

public interface IModalDialogService
{
    ModalDialogSession Create(Window owner, Control view, string? title = null, ModalDialogOptions? options = null);
}
