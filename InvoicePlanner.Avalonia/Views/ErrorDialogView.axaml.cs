using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InvoicePlanner.Avalonia.Views;

public partial class ErrorDialogView : UserControl
{
    public ErrorDialogView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
