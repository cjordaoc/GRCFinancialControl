using Avalonia;
using Avalonia.Controls;

namespace GRC.Shared.UI.Controls;

public partial class StatusBar : UserControl
{
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<StatusBar, string?>(nameof(Message));

    public static readonly StyledProperty<bool> HasIconProperty =
        AvaloniaProperty.Register<StatusBar, bool>(nameof(HasIcon));

    public static readonly StyledProperty<object?> RightContentProperty =
        AvaloniaProperty.Register<StatusBar, object?>(nameof(RightContent));

    public StatusBar()
    {
        InitializeComponent();
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool HasIcon
    {
        get => GetValue(HasIconProperty);
        set => SetValue(HasIconProperty, value);
    }

    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }
}
