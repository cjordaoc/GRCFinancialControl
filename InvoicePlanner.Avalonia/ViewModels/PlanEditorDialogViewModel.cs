using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed partial class PlanEditorDialogViewModel : ViewModelBase
{
    public PlanEditorDialogViewModel(PlanEditorViewModel editor)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public PlanEditorViewModel Editor { get; }

    [ObservableProperty]
    private bool canSave;
}
