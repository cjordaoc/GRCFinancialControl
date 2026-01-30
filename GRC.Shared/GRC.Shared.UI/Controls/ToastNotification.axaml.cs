using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GRC.Shared.UI.Controls;

public partial class ToastNotification : UserControl
{
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ToastNotification, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<ToastNotification, Geometry?>(nameof(IconData));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ToastNotification, string?>(nameof(Message));

    public static readonly StyledProperty<string> CloseButtonTextProperty =
        AvaloniaProperty.Register<ToastNotification, string>(nameof(CloseButtonText), "✕");

    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<ToastNotification, Thickness>(nameof(ContentPadding), new Thickness(12));

    public static readonly StyledProperty<double> ContentCornerRadiusProperty =
        AvaloniaProperty.Register<ToastNotification, double>(nameof(ContentCornerRadius), 6.0);

    public static readonly StyledProperty<double> ContentOpacityProperty =
        AvaloniaProperty.Register<ToastNotification, double>(nameof(ContentOpacity), 0.95);

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<ToastNotification, double>(nameof(IconSize), 16.0);

    public static readonly StyledProperty<Thickness> IconMarginProperty =
        AvaloniaProperty.Register<ToastNotification, Thickness>(nameof(IconMargin), new Thickness(0, 0, 8, 0));

    public static readonly StyledProperty<Thickness> CloseButtonMarginProperty =
        AvaloniaProperty.Register<ToastNotification, Thickness>(nameof(CloseButtonMargin), new Thickness(8, 0, 0, 0));

    public ToastNotification()
    {
        InitializeComponent();
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string CloseButtonText
    {
        get => GetValue(CloseButtonTextProperty);
        set => SetValue(CloseButtonTextProperty, value);
    }

    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public double ContentCornerRadius
    {
        get => GetValue(ContentCornerRadiusProperty);
        set => SetValue(ContentCornerRadiusProperty, value);
    }

    public double ContentOpacity
    {
        get => GetValue(ContentOpacityProperty);
        set => SetValue(ContentOpacityProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public Thickness IconMargin
    {
        get => GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    public Thickness CloseButtonMargin
    {
        get => GetValue(CloseButtonMarginProperty);
        set => SetValue(CloseButtonMarginProperty, value);
    }
}
