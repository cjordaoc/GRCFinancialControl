using Avalonia;
using Avalonia.Controls;

namespace GRC.Shared.UI.Controls;

public partial class LoadingIndicator : UserControl
{
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<LoadingIndicator, string?>(nameof(Message));

    public LoadingIndicator()
    {
        InitializeComponent();
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}
