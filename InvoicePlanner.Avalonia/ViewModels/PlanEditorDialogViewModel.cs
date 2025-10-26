using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed partial class PlanEditorDialogViewModel : ViewModelBase
{
    public PlanEditorDialogViewModel(PlanEditorViewModel editor)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public PlanEditorViewModel Editor { get; }

    public IRelayCommand EditLinesCommand => Editor.EditLinesCommand;
    public IRelayCommand CloseCommand => Editor.ClosePlanFormCommand;

    [ObservableProperty]
    private bool canEditLines;
}
