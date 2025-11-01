using System;
using CommunityToolkit.Mvvm.Input;

namespace InvoicePlanner.Avalonia.ViewModels;

public sealed class PlanEditorDialogViewModel : ViewModelBase
{
    private int _selectedTabIndex;

    public PlanEditorDialogViewModel(PlanEditorViewModel editor)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public PlanEditorViewModel Editor { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public IRelayCommand EditLinesCommand => Editor.EditLinesCommand;
    public IRelayCommand DeletePlanCommand => Editor.DeletePlanCommand;
    public IRelayCommand CloseCommand => Editor.ClosePlanFormCommand;

    public void NavigateToInvoiceItems()
    {
        SelectedTabIndex = 1;
    }
}
