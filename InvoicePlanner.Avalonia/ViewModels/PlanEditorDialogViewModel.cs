using System;
using System.Windows.Input;
using App.Presentation.Controls;
using App.Presentation.Localization;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed class PlanEditorDialogViewModel : ViewModelBase, IModalOverlayActionProvider
{
    public PlanEditorDialogViewModel(PlanEditorViewModel editor)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public PlanEditorViewModel Editor { get; }

    public bool IsPrimaryActionVisible => true;

    public string? PrimaryActionText => LocalizationRegistry.Get("InvoicePlan.Button.SaveDraft");

    public ICommand? PrimaryActionCommand => Editor.SavePlanCommand;
}
