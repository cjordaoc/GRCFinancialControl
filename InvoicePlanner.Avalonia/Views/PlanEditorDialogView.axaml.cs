using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InvoicePlanner.Avalonia.Views;

public partial class PlanEditorDialogView : UserControl
{
    public PlanEditorDialogView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
