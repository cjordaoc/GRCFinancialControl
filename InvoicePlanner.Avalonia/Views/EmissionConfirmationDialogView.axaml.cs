using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InvoicePlanner.Avalonia.Views;

public partial class EmissionConfirmationDialogView : UserControl
{
    public EmissionConfirmationDialogView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
