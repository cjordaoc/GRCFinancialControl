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

    [ObservableProperty]
    private int selectedTabIndex;

    public IRelayCommand EditLinesCommand => Editor.EditLinesCommand;
    public IRelayCommand DeletePlanCommand => Editor.DeletePlanCommand;
    public IRelayCommand CloseCommand => Editor.ClosePlanFormCommand;

    public void NavigateToInvoiceItems()
    {
        SelectedTabIndex = 1;
    }
}
