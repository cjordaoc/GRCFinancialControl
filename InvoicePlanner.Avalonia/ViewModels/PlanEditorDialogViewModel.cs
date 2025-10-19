using System;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed class PlanEditorDialogViewModel : ViewModelBase
{
    public PlanEditorDialogViewModel(PlanEditorViewModel editor)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public PlanEditorViewModel Editor { get; }
}
