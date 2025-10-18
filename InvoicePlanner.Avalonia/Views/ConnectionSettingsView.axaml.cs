using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InvoicePlanner.Avalonia.Views;

public partial class ConnectionSettingsView : UserControl
{
    public ConnectionSettingsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
