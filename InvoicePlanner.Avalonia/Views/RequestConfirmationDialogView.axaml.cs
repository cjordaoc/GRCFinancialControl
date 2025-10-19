using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InvoicePlanner.Avalonia.Views;

public partial class RequestConfirmationDialogView : UserControl
{
    public RequestConfirmationDialogView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
